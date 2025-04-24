using common;
using Fs;
using Google.Protobuf;
using Node;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace node
{
    using UpdateCallback = Func<FileChunk, CancellationToken, Task<FileChunk>>;
    using CompletionCallback = Func<FileChunk, Task>;

    public class DownloadManager : IDisposable
    {
        public class StateChange
        {
            public ByteString Hash { get; set; } = ByteString.Empty;
            public DownloadStatus NewStatus { get; set; } = DownloadStatus.Pending;
            public FileChunk? Chunk { get; set; } = null;
        }

        private readonly System.Threading.Lock fileProgressLock = new();
        private PersistentDictionary<ByteString, (long, long)> FileProgress;
        private readonly Channel<StateChange> channel;
        private UpdateCallback? updateCallback = null;
        private CompletionCallback? completionCallback = null;
        private readonly Task readLoop;
        private readonly CancellationTokenSource tokenSource = new();
        private readonly ConcurrentDictionary<ByteString, CancellationTokenSource> fileTokens;
        private readonly TaskProcessor downloadProcessor;
        private readonly TaskProcessor completionProcessor;

        public DownloadManager(string dbPath, int taskCapacity = 100000)
        {
            downloadProcessor = new TaskProcessor(20, taskCapacity);
            completionProcessor = new TaskProcessor(20, taskCapacity);
            fileTokens = new(new HashUtils.ByteStringComparer());
            channel = Channel.CreateBounded<StateChange>(new BoundedChannelOptions(taskCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
            });

            PersistentDictionary<ByteString, FileChunk> incompleteChunks
            = new(System.IO.Path.Combine(dbPath, "IncompleteChunks"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: FileChunk.Parser.ParseFrom
            );

            FileProgress = new(
                System.IO.Path.Combine(dbPath, "FileProgress"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: (a) => Encoding.UTF8.GetBytes($"{a.Item1} {a.Item2}"),
                valueDeserializer: str =>
                {
                    var parts = Encoding.UTF8.GetString(str).Split(' ');
                    if (parts.Length != 2)
                    {
                        throw new ArgumentException("Invalid format for FileProgress");
                    }
                    return (long.Parse(parts[0]), long.Parse(parts[1]));
                }
            );

            readLoop = Task.Run(() => ProcessCommandsAsync(tokenSource.Token,
            new(
                System.IO.Path.Combine(dbPath, "IncompleteChunks"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: FileChunk.Parser.ParseFrom
            )));
        }

        private async Task ProcessCommandsAsync(CancellationToken token,
            PersistentDictionary<ByteString, FileChunk> chunkTasks)
        {
            try
            {
                await foreach (var message in channel.Reader.ReadAllAsync(token))
                {
                    switch (message.NewStatus)
                    {
                        case DownloadStatus.Complete:
                            {
                                if (message.Chunk == null)
                                {
                                    break;
                                }

                                await completionProcessor.AddAsync(() => CompleteAsync(message.Chunk));

                                break;
                            }
                        case DownloadStatus.Pending:
                            {
                                if (message.Chunk != null)
                                {
                                    throw new Exception("Chunk should be null");
                                }

                                fileTokens[message.Hash] = new CancellationTokenSource();
                                foreach (var chunk in GetChildChunks(chunkTasks, message.Hash))
                                {
                                    chunk.Status = DownloadStatus.Pending;
                                    chunkTasks[chunk.Hash] = chunk;
                                    await downloadProcessor.AddAsync(() => UpdateAsync(chunk, fileTokens[message.Hash].Token));
                                }

                                break;
                            }
                        case DownloadStatus.Paused:
                            {
                                if (message.Chunk != null)
                                {
                                    chunkTasks[message.Chunk.Hash] = message.Chunk;
                                    break;
                                }

                                var hash = message.Hash;
                                if (fileTokens.TryRemove(hash, out var tokenSource))
                                {
                                    await tokenSource.CancelAsync();
                                }

                                break;
                            }
                        default:
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                List<FileChunk> chunks = [];
                chunkTasks.ForEach((k, v) =>
                {
                    if (v.Status == DownloadStatus.Complete)
                    {
                        throw new Exception("????");
                    }
                    v.Status = DownloadStatus.Paused;
                    chunks.Add(v);
                    return true;
                });

                foreach (var chunk in chunks)
                {
                    chunkTasks[chunk.Hash] = chunk;
                }
            }

            static List<FileChunk> GetChildChunks(PersistentDictionary<ByteString, FileChunk> chunkTasks, ByteString hash)
            {
                List<FileChunk> chunks = [];
                chunkTasks.PrefixScan(hash, (k, v) =>
                {
                    chunks.Add(v);
                    return;
                });
                return chunks;
            }
        }

        public async Task UpdateAsync(FileChunk chunk, CancellationToken token)
        {
            if (updateCallback == null)
            {
                return;
            }

            long change = chunk.CurrentCount;
            chunk.Status = DownloadStatus.Active;
            chunk = await updateCallback(chunk, token);
            change = chunk.CurrentCount - change;
            if (change != 0)
            {
                UpdateFileProgress(chunk.Hash, change);
            }

            await channel.Writer.WriteAsync(
                new()
                {
                    Hash = HashUtils.GetChunkHash(chunk),
                    NewStatus = chunk.Status,
                    Chunk = chunk
                },
                CancellationToken.None
            );
        }

        public async Task CompleteAsync(FileChunk chunk)
        {
            if (completionCallback == null)
            {
                return;
            }
            await completionCallback(chunk);
        }

        public void AddChunkUpdateCallback(UpdateCallback callback)
        {
            updateCallback = callback;
        }
        public void AddChunkCompletionCallback(CompletionCallback callback)
        {
            completionCallback = callback;
        }

        private void UpdateFileProgress(ByteString hash, long newProgress)
        {
            lock (fileProgressLock)
            {
                var progress = FileProgress[hash];
                progress.Item1 += newProgress;
                FileProgress[hash] = progress;
            }
        }

        public (long, long) GetFileProgress(ByteString hash)
        {
            lock (fileProgressLock)
            {
                if (FileProgress.TryGetValue(hash, out var p))
                {
                    return p;
                }
            }
            throw new NullReferenceException();
        }

        public async Task AddNewFileAsync(IncompleteFile file, FileChunk[] chunks, ByteString fileHash)
        {
            await channel.Writer.WriteAsync(new StateChange { Hash = fileHash, NewStatus = DownloadStatus.Pending });
        }

        public ByteString[] GetIncompleteFiles()
        {
            List<ByteString> hashes = [];
            lock (fileProgressLock)
            {
                FileProgress.ForEach((k, v) =>
                {
                    if (v.Item1 != v.Item2)
                    {
                        hashes.Add(k);
                    }
                    return true;
                });
            }

            return [.. hashes];
        }

        public async Task PauseDownloadAsync(ObjectWithHash file)
        {
            await channel.Writer.WriteAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Paused });
        }
        public async Task ResumeDownloadAsync(ObjectWithHash file)
        {
            await channel.Writer.WriteAsync(new StateChange { Hash = file.Hash, NewStatus = DownloadStatus.Pending });
        }
        public async Task CompleteDownloadAsync(ByteString hash)
        {
            await channel.Writer.WriteAsync(
                new StateChange
                {
                    Hash = hash,
                    NewStatus = DownloadStatus.Complete
                }
            );
        }

        public void Dispose()
        {
            tokenSource.Cancel();
        }
    }
}
