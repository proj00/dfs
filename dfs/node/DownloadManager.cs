using common;
using Fs;
using Google.Protobuf;
using Node;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

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

        private readonly PersistentCache<ByteString, Ui.Progress> FileProgress;
        private UpdateCallback? updateCallback = null;
        private bool disposedValue;
        private readonly CancellationTokenSource tokenSource = new();
        private readonly ConcurrentDictionary<ByteString, CancellationTokenSource> fileTokens;
        private readonly TaskProcessor downloadProcessor;
        private readonly TaskProcessor stateProcessor;
        private readonly PersistentCache<ByteString, FileChunk> chunkTasks;

        public DownloadManager(string dbPath, int taskCapacity = 100000)
        {
            downloadProcessor = new TaskProcessor(20, taskCapacity);
            stateProcessor = new TaskProcessor(1, taskCapacity);
            fileTokens = new(new ByteStringComparer());

            FileProgress = new(
                System.IO.Path.Combine(dbPath, "FileProgress"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: (a) => a.ToByteArray(),
                valueDeserializer: Ui.Progress.Parser.ParseFrom
            );

            chunkTasks = new(
                System.IO.Path.Combine(dbPath, "IncompleteChunks"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: FileChunk.Parser.ParseFrom
            );
        }

        private async Task HandleCommandAsync(StateChange message)
        {
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
                            await chunkTasks.SetAsync(chunk.Hash, chunk);
                            await downloadProcessor.AddAsync(() => UpdateAsync(chunk, fileTokens[message.Hash].Token));
                        }

                        break;
                    }
                case DownloadStatus.Paused:
                    {
                        if (message.Chunk != null)
                        {
                            await chunkTasks.SetAsync(message.Chunk[0].Hash, message.Chunk[0]);
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

        static async Task<List<FileChunk>> GetChildChunksAsync(PersistentCache<ByteString, FileChunk> chunkTasks, ByteString hash)
        {
            List<FileChunk> chunks = [];
            await chunkTasks.PrefixScan(hash, (k, v) =>
            {
                chunks.Add(v);
            });
            return chunks;
        }

        public async Task UpdateAsync(FileChunk chunk, CancellationToken token)
        {
            if (updateCallback == null)
            {
                return;
            }

            chunk.Status = DownloadStatus.Active;
            chunk = await updateCallback(chunk, token);

            await stateProcessor.AddAsync(() => HandleCommandAsync(
                new()
                {
                    Hash = HashUtils.GetChunkHash(chunk),
                    NewStatus = chunk.Status,
                    Chunk = [chunk]
                }
            ));
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
                Debug.Assert(newProgress != 0, $"{newProgress}");
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

        public async Task AddNewFileAsync(IncompleteFile file, FileChunk[] chunks)
        {
            await FileProgress.SetAsync(chunks[0].FileHash, new() { Current = 0, Total = file.Size });
            await stateProcessor.AddAsync(() => HandleCommandAsync(new StateChange { Hash = chunks[0].FileHash, NewStatus = DownloadStatus.Pending, Chunk = chunks }));
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
            await stateProcessor.AddAsync(() => HandleCommandAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Paused }));
        }
        public async Task ResumeDownloadAsync(ObjectWithHash file)
        {
            await stateProcessor.AddAsync(() => HandleCommandAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Pending }));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                    chunkTasks.Dispose();
                    downloadProcessor.Dispose();
                    stateProcessor.Dispose();
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
    }
}
