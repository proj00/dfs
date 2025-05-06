using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using node;
using Node;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using Tracker;
using Ui;

namespace node
{
    using UpdateCallback = Func<FileChunk, CancellationToken, Task<FileChunk>>;

    public class DownloadManager : IDownloadManager
    {
        private sealed class StateChange
        {
            public ByteString Hash { get; set; } = ByteString.Empty;
            public DownloadStatus NewStatus { get; set; } = DownloadStatus.Pending;
            public FileChunk[] Chunk { get; set; } = [];
        }

        public sealed class FileContext
        {
            public CancellationTokenSource Source { get; } = new();
            public int WaitingForPause;
            public AsyncManualResetEvent WaitingForStart { get; }
            public AsyncManualResetEvent PauseEvent { get; }

            public FileContext(int size)
            {
                WaitingForPause = 0;
                WaitingForStart = new(true);
                WaitingForStart.Reset();
                PauseEvent = new(true);
                PauseEvent.Reset();
            }
        }

        private readonly IPersistentCache<ByteString, Ui.Progress> FileProgress;
        private UpdateCallback? updateCallback = null;
        private bool disposedValue;
        private readonly CancellationTokenSource tokenSource = new();
        private readonly ConcurrentDictionary<ByteString, FileContext> fileTokens;
        private readonly ConcurrentDictionary<ByteString, ByteString> completedFiles;
        private readonly TaskProcessor downloadProcessor;
        private readonly TaskProcessor stateProcessor;
        private readonly IPersistentCache<ByteString, FileChunk> chunkTasks;
        private readonly AsyncManualResetEvent shutdownEvent;
        private int taskCount = 0;
        private bool done = false;
        private readonly ILogger logger;

        public DownloadManager(string dbPath, int taskCapacity, IPersistentCache<ByteString, Progress> fileProgress, IPersistentCache<ByteString, FileChunk> chunkTasks,
            ILogger logger)
        {
            this.logger = logger;
            ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
            ArgumentOutOfRangeException.ThrowIfLessThan(taskCapacity, 0);

            downloadProcessor = new TaskProcessor(1024, taskCapacity);
            stateProcessor = new TaskProcessor(1, taskCapacity);
            fileTokens = new(new ByteStringComparer());
            completedFiles = new(new ByteStringComparer());

            ArgumentNullException.ThrowIfNull(fileProgress);
            ArgumentNullException.ThrowIfNull(chunkTasks);
            FileProgress = fileProgress;
            this.chunkTasks = chunkTasks;

            shutdownEvent = new(true);
            shutdownEvent.Reset();
        }

        public DownloadManager(ILoggerFactory factory, string dbPath, int taskCapacity = 100000)
        :
#pragma warning disable CA2000 // Dispose objects before losing scope
            this(dbPath, taskCapacity, new PersistentCache<ByteString, Ui.Progress>(
                System.IO.Path.Combine(dbPath, "FileProgress"),
                new ByteStringSerializer(),
                new Serializer<Ui.Progress>()
            ),
            new PersistentCache<ByteString, FileChunk>(
                System.IO.Path.Combine(dbPath, "IncompleteChunks"),
                new ByteStringSerializer(),
                new Serializer<FileChunk>()
            ),
            factory.CreateLogger("DownloadManager"))
#pragma warning restore CA2000 // Dispose objects before losing scope
        { }

        private async Task HandleCommandAsync(StateChange message)
        {
            switch (message.NewStatus)
            {
                case DownloadStatus.Stop:
                    {
                        logger.LogInformation($"everything stopped");
                        done = true;
                        break;
                    }
                case DownloadStatus.Complete:
                    {
                        await HandleCompleteAsync(message);
                        break;
                    }
                case DownloadStatus.Pending:
                    {
                        await HandlePendingAsync(message);

                        break;
                    }
                case DownloadStatus.Paused:
                    {
                        await HandlePausedAsync(message);

                        break;
                    }
                default:
                    break;
            }
        }

        private async Task HandleCompleteAsync(StateChange message)
        {
            try
            {
                if (message.Chunk == null || message.Chunk.Length == 0)
                {
                    throw new ArgumentException("no");
                }
                var chunk = message.Chunk[0];
                logger.LogInformation($"chunk of file {chunk.DestinationDir} done");
                if (Interlocked.Decrement(ref fileTokens[chunk.FileHash].WaitingForPause) <= 0)
                {
                    if (fileTokens.TryRemove(chunk.FileHash, out FileContext? context))
                    {
                        context.Source.Dispose();
                        context.PauseEvent.Set();
                    }
                    completedFiles[chunk.FileHash] = chunk.FileHash;
                }
                await chunkTasks.Remove(HashUtils.GetChunkHash(chunk));
            }
            catch (Exception e)
            {
                logger.LogError(e, "HandleCompleteAsync");
                throw;
            }
        }

        private async Task HandlePausedAsync(StateChange message)
        {
            try
            {
                if (message.Chunk != null && message.Chunk.Length > 0)
                {
                    var chunk = message.Chunk[0];
                    logger.LogInformation($"chunk of file {chunk.DestinationDir} paused");

                    await chunkTasks.SetAsync(HashUtils.GetChunkHash(chunk), chunk);

                    if (Interlocked.Decrement(ref fileTokens[chunk.FileHash].WaitingForPause) <= 0)
                    {
                        if (fileTokens.TryRemove(chunk.FileHash, out FileContext? context))
                        {
                            context.Source.Dispose();
                            context.PauseEvent.Set();
                        }
                    }
                    return;
                }

                var hash = message.Hash;
                if (fileTokens.TryGetValue(hash, out var fileToken))
                {
                    await fileToken.Source.CancelAsync();
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "HandlePausedAsync");
                throw;
            }
        }

        private async Task HandlePendingAsync(StateChange message)
        {
            try
            {
                var chunks = await GetChildChunksAsync(chunkTasks, message.Hash);
                if (chunks.Count == 0)
                    chunks.AddRange(message.Chunk ?? []);
                logger.LogInformation($"chunk of file {chunks[0].DestinationDir} pending");

                if (!fileTokens.ContainsKey(message.Hash))
                {
                    fileTokens[message.Hash] = new FileContext(chunks.Count);
                }
                var context = fileTokens[message.Hash];
                foreach (var chunk in chunks)
                {
                    Interlocked.Increment(ref taskCount);
                    shutdownEvent.Reset();
                    bool b = await downloadProcessor.AddAsync(async () =>
                    {
                        try
                        {
                            Interlocked.Increment(ref context.WaitingForPause);
                            context.WaitingForStart.Set();
                            chunk.Status = DownloadStatus.Pending;
                            await chunkTasks.SetAsync(HashUtils.GetChunkHash(chunk), chunk);

                            logger.LogInformation($"chunk of file {chunks[0].DestinationDir} updating");

                            await UpdateAsync(chunk, context.Source.Token);
                        }
                        finally
                        {
                            if (Interlocked.Decrement(ref taskCount) <= 0)
                            {
                                shutdownEvent.Set();
                            }
                        }
                    });
                    if (!b)
                    {
                        if (Interlocked.Decrement(ref taskCount) <= 0)
                        {
                            shutdownEvent.Set();
                        }
                        throw new OperationCanceledException();
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "HandlePendingAsync");
                throw;
            }
        }

        static async Task<List<FileChunk>> GetChildChunksAsync(IPersistentCache<ByteString, FileChunk> chunkTasks, ByteString hash)
        {
            List<FileChunk> chunks = [];
            await chunkTasks.ForEach((k, v) =>
            {
                if (!k.Take(hash.Length).SequenceEqual(hash) || v.Status != DownloadStatus.Paused)
                {
                    return true;
                }
                chunks.Add(v);
                return true;
            });
            return chunks;
        }

        private async Task UpdateAsync(FileChunk chunk, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(updateCallback);

            chunk.Status = DownloadStatus.Active;
            try
            {
                chunk = await updateCallback(chunk, token);
            }
            catch (OperationCanceledException)
            {
                chunk.Status = DownloadStatus.Paused;
            }

            var newState = new StateChange()
            {
                Hash = HashUtils.GetChunkHash(chunk),
                NewStatus = chunk.Status,
                Chunk = [chunk]
            };
            await EnqueueStateInternalAsync(newState, true);
        }

        private async Task EnqueueStateAsync(StateChange state)
        {
            await EnqueueStateInternalAsync(state, false);
        }

        private async Task EnqueueStateInternalAsync(StateChange state, bool internalUse)
        {
            if (done && !internalUse)
            {
                return;
            }

            Interlocked.Increment(ref taskCount);
            shutdownEvent.Reset();
            bool b = await stateProcessor.AddAsync(async () =>
            {
                try
                {
                    await HandleCommandAsync(
                        state
                    );
                }
                finally
                {
                    if (Interlocked.Decrement(ref taskCount) <= 0)
                    {
                        shutdownEvent.Set();
                    }
                }
            }
            );
            if (!b)
            {
                if (Interlocked.Decrement(ref taskCount) <= 0)
                {
                    shutdownEvent.Set();
                }
                throw new InvalidOperationException("enqueue failed");
            }
        }

        public void AddChunkUpdateCallback(UpdateCallback callback)
        {
            updateCallback = callback;
        }

        public async Task UpdateFileProgressAsync(ByteString hash, long newProgress)
        {
            await FileProgress.MutateAsync(hash, (v) =>
            {
                if (v == null)
                {
                    throw new InvalidOperationException("this should never happen");
                }
                v.Current += newProgress;
                return v;
            });
        }

        public async Task<Ui.Progress> GetFileProgressAsync(ByteString hash)
        {
            var p = await FileProgress.TryGetValue(hash);
            if (p != null)
            {
                return p;
            }
            ArgumentNullException.ThrowIfNull(p);

            // :-)
            throw new InvalidOperationException("this should never happen");
        }

        public async Task AddNewFileAsync(ObjectWithHash obj, Uri trackerUri, string destinationDir)
        {
            if (completedFiles.ContainsKey(obj.Hash))
            {
                return;
            }
            (FileChunk[] chunks, IncompleteFile file) = GetIncompleteFile(obj, trackerUri, destinationDir);
            fileTokens[obj.Hash] = new FileContext(chunks.Length);
            await FileProgress.SetAsync(chunks[0].FileHash, new() { Current = 0, Total = file.Size });
            await EnqueueStateAsync(new StateChange { Hash = chunks[0].FileHash, NewStatus = DownloadStatus.Pending, Chunk = chunks });
        }

        public async Task<ByteString[]> GetIncompleteFilesAsync()
        {
            List<ByteString> hashes = [];

            await FileProgress.ForEach((k, v) =>
            {
                if (v.Current != v.Total)
                {
                    hashes.Add(k);
                }
                return true;
            });

            return [.. hashes];
        }

        public async Task PauseDownloadAsync(ObjectWithHash file, CancellationToken token)
        {
            try
            {
                if (completedFiles.ContainsKey(file.Hash) || !fileTokens.ContainsKey(file.Hash))
                {
                    return;
                }

                await fileTokens[file.Hash].WaitingForStart.WaitAsync(token);
            }
            catch (OperationCanceledException e)
            {
                logger.LogError($"pause cancelled : {e}");
                throw;
            }

            await EnqueueStateAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Paused });
        }
        public async Task ResumeDownloadAsync(ObjectWithHash file, CancellationToken token)
        {
            try
            {
                if (completedFiles.ContainsKey(file.Hash))
                {
                    return;
                }

                if (fileTokens.ContainsKey(file.Hash))
                {
                    var e = fileTokens[file.Hash].PauseEvent;
                    await e.WaitAsync(token);
                }
                else
                {
                    return;
                }
            }
            catch (OperationCanceledException e)
            {
                logger.LogError($"resume cancelled : {e}");
                throw;
            }
            await EnqueueStateAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Pending });
        }

        private static (FileChunk[] chunks, IncompleteFile file) GetIncompleteFile(ObjectWithHash obj, Uri trackerUri, string destinationDir)
        {
            destinationDir = destinationDir + "\\" + obj.Object.Name;

            List<FileChunk> chunks = [];
            var i = 0;
            foreach (var hash in obj.Object.File.Hashes.Hash)
            {
                chunks.Add(new()
                {
                    Hash = hash,
                    Offset = obj.Object.File.Hashes.ChunkSize * i,
                    FileHash = obj.Hash,
                    Size = Math.Min(obj.Object.File.Hashes.ChunkSize, obj.Object.File.Size - obj.Object.File.Hashes.ChunkSize * i),
                    TrackerUri = trackerUri.ToString(),
                    DestinationDir = destinationDir,
                    CurrentCount = 0,
                    Status = DownloadStatus.Pending,
                });
                i++;
            }
            var file = new IncompleteFile()
            {
                Status = DownloadStatus.Pending,
                Size = obj.Object.File.Size,
            };
            return (chunks.ToArray(), file);
        }

        async ValueTask System.IAsyncDisposable.DisposeAsync()
        {
            if (!disposedValue)
            {
                await EnqueueStateAsync(new() { NewStatus = DownloadStatus.Stop });
                // this is screwed up but it works
                await shutdownEvent.WaitAsync();
                stateProcessor.Dispose();
                downloadProcessor.Dispose();
                chunkTasks.Dispose();
                await tokenSource.CancelAsync();
                tokenSource.Dispose();
                disposedValue = true;
            }
        }
    }
}
