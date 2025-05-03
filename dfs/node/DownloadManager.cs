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

    public class DownloadManager : IDisposable
    {
        private sealed class StateChange
        {
            public ByteString Hash { get; set; } = ByteString.Empty;
            public DownloadStatus NewStatus { get; set; } = DownloadStatus.Pending;
            public FileChunk[] Chunk { get; set; } = [];
        }

        private readonly IPersistentCache<ByteString, Ui.Progress> FileProgress;
        private UpdateCallback? updateCallback = null;
        private bool disposedValue;
        private readonly CancellationTokenSource tokenSource = new();
        private readonly ConcurrentDictionary<ByteString, CancellationTokenSource> fileTokens;
        private readonly TaskProcessor downloadProcessor;
        private readonly IPersistentCache<ByteString, FileChunk> chunkTasks;
        private readonly AsyncLock taskLock = new();

        public DownloadManager(string dbPath, int taskCapacity, IPersistentCache<ByteString, Progress> fileProgress, IPersistentCache<ByteString, FileChunk> chunkTasks)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dbPath, nameof(dbPath));
            ArgumentOutOfRangeException.ThrowIfLessThan(taskCapacity, 0, nameof(taskCapacity));

            downloadProcessor = new TaskProcessor(20, taskCapacity);
            fileTokens = new(new ByteStringComparer());

            ArgumentNullException.ThrowIfNull(fileProgress);
            ArgumentNullException.ThrowIfNull(chunkTasks);
            FileProgress = fileProgress;
            this.chunkTasks = chunkTasks;
        }

        public DownloadManager(string dbPath, int taskCapacity = 100000)
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
            ))
#pragma warning restore CA2000 // Dispose objects before losing scope
        { }


        private async Task HandleCommandAsync(StateChange message)
        {
            using var l = await taskLock.LockAsync();
            switch (message.NewStatus)
            {
                case DownloadStatus.Complete:
                    {
                        if (message.Chunk == null || message.Chunk.Length == 0)
                        {
                            break;
                        }

                        await chunkTasks.Remove(message.Chunk[0].Hash);
                        break;
                    }
                case DownloadStatus.Pending:
                    {
                        fileTokens[message.Hash] = new CancellationTokenSource();
                        var chunks = await GetChildChunksAsync(chunkTasks, message.Hash);
                        if (chunks.Count == 0)
                            chunks.AddRange(message.Chunk ?? []);
                        foreach (var chunk in chunks)
                        {
                            chunk.Status = DownloadStatus.Pending;
                            await chunkTasks.SetAsync(HashUtils.GetChunkHash(chunk), chunk);
                            await downloadProcessor.AddAsync((token) => UpdateAsync(chunk, fileTokens[message.Hash].Token));
                        }

                        break;
                    }
                case DownloadStatus.Paused:
                    {
                        if (message.Chunk != null && message.Chunk.Length > 0)
                        {
                            await chunkTasks.SetAsync(HashUtils.GetChunkHash(message.Chunk[0]), message.Chunk[0]);
                            break;
                        }

                        var hash = message.Hash;
                        if (fileTokens.TryRemove(hash, out var fileToken))
                        {
                            await fileToken.CancelAsync();
                            fileToken.Dispose();
                        }

                        break;
                    }
                default:
                    break;
            }
        }

        static async Task<List<FileChunk>> GetChildChunksAsync(IPersistentCache<ByteString, FileChunk> chunkTasks, ByteString hash)
        {
            List<FileChunk> chunks = [];
            await chunkTasks.ForEach((k, v) =>
            {
                if (!k.Take(hash.Length).SequenceEqual(hash))
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
            if (updateCallback == null)
            {
                return;
            }

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
            await HandleCommandAsync(
                newState
            );
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
            (FileChunk[] chunks, IncompleteFile file) = GetIncompleteFile(obj, trackerUri, destinationDir);
            await FileProgress.SetAsync(chunks[0].FileHash, new() { Current = 0, Total = file.Size });
            await HandleCommandAsync(new StateChange { Hash = chunks[0].FileHash, NewStatus = DownloadStatus.Pending, Chunk = chunks });
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

        public async Task PauseDownloadAsync(ObjectWithHash file)
        {
            await HandleCommandAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Paused });
        }
        public async Task ResumeDownloadAsync(ObjectWithHash file)
        {
            await HandleCommandAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Pending });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    downloadProcessor.Dispose();
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                    chunkTasks.Dispose();
                    taskLock.Dispose();
                }
                disposedValue = true;
            }
        }

        ~DownloadManager()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
    }
}
