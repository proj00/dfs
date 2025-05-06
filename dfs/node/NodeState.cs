using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Node;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Navigation;
using Tracker;
using Ui;
using static Node.Node;
using static Tracker.Tracker;
using static Ui.Ui;

namespace node
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, GrpcChannel>;

    public partial class NodeState : IDisposable
    {
        private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
        public IFilesystemManager Manager { get; }
        public IDownloadManager Downloads { get; }
        public BlockListHandler BlockList { get; }

        private readonly ILoggerFactory loggerFactory;
        public FilePathHandler PathHandler { get; }
        public ILogger Logger { get; private set; }
        public string LogPath { get; private set; }
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private bool disposedValue;
        public TransactionManager Transactions { get; }
        private readonly IFileSystem fs;
        public IAsyncIOWrapper AsyncIO { get; }
        public GrpcClientHandler ClientHandler { get; }
        private readonly ConcurrentDictionary<string, AsyncLock> peerLocks;

        public NodeState(IFileSystem fs, TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath,
            IFilesystemManager manager, IDownloadManager downloads,
            IPersistentCache<ByteString, string> pathByHash,
            IPersistentCache<string, string> whitelist,
            IPersistentCache<string, string> blacklist,
            GrpcChannelFactory grpcChannelFactory, IAsyncIOWrapper io, Action<string, string> startProcess)
        {
            this.fs = fs;
            this.AsyncIO = io;
            this.loggerFactory = loggerFactory;
            Logger = this.loggerFactory.CreateLogger("Node");
            LogPath = logPath;
            Manager = manager;
            Downloads = downloads;

            this.ClientHandler = new(channelTtl, grpcChannelFactory, loggerFactory);
            this.PathHandler = new(pathByHash, startProcess);
            BlockList = new BlockListHandler(whitelist, blacklist);
            Transactions = new(Logger);
            peerLocks = new();
        }

        public NodeState(TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath, string dbPath)
            : this(new FileSystem(), channelTtl, loggerFactory, logPath,
#pragma warning disable CA2000 // Dispose objects before losing scope
                  new FilesystemManager(dbPath),
                  new DownloadManager(loggerFactory, dbPath),
                new PersistentCache<ByteString, string>(
                System.IO.Path.Combine(dbPath, "PathByHash"),
                new ByteStringSerializer(),
                new StringSerializer()
            ), new PersistentCache<string, string>(
                System.IO.Path.Combine(dbPath, "Whitelist"),
                new StringSerializer(),
                new StringSerializer()
            ), new PersistentCache<string, string>(
                System.IO.Path.Combine(dbPath, "Blacklist"),
                new StringSerializer(),
                new StringSerializer()
            ),
#pragma warning restore CA2000 // Dispose objects before losing scope
                GrpcChannel.ForAddress,
                new AsyncIOWrapper(),
                (string name, string args) => Process.Start(name, args))
        { }

        public async Task DownloadObjectByHashAsync(ByteString hash, Guid? guid, ITrackerWrapper tracker, string destinationDir)
        {
            if (!fs.Directory.Exists(destinationDir))
            {
                throw new ArgumentException($"Invalid destination directory: path {destinationDir} doesn't exist");
            }

            List<ObjectWithHash> objects = await tracker.GetObjectTree(hash, CancellationToken.None);
            var fileTasks = await objects.ToAsyncEnumerable()
                .WhereAwait(async (obj) => obj.Object.TypeCase == FileSystemObject.TypeOneofCase.File
                        && !(await Manager.ObjectByHash.ContainsKey(obj.Hash))).ToArrayAsync();

            guid = await Manager.CreateObjectContainer(objects.ToArray(), hash, guid ?? Guid.NewGuid());

            foreach (var file in fileTasks)
            {
                var dir = @"\\?\" + destinationDir + "\\" + Hex.ToHexString(file.Hash.ToByteArray());
                fs.Directory.CreateDirectory(dir);
                await Downloads.AddNewFileAsync(file, tracker.GetUri(), dir);
            }
        }

        public async Task<FileChunk> DownloadChunkAsync(FileChunk chunk, Uri nodeURI, CancellationToken token)
        {
            if (chunk.Status == DownloadStatus.Complete)
            {
                throw new ArgumentException("Already downloaded");
            }

            var tracker = ClientHandler.GetTrackerWrapper(new Uri(chunk.TrackerUri));
            Debug.Assert(chunk.Hash.Length == 64);

            List<string> peers = (await tracker.GetPeerList(new PeerRequest() { ChunkHash = chunk.Hash, MaxPeerCount = 256 }, token))
                .ToList();
            Logger.LogInformation($"peers: {peers.Count}");
            if (peers.Count == 0)
            {
                chunk.Status = DownloadStatus.Pending;
                Debug.Assert(false, "no peers");
                Logger.LogWarning($"No peers found for chunk {chunk.Hash.ToBase64()}");
                return chunk;
            }

            // for now, pick a random peer (chunk tasks are persistent and we can just add simple retries later)
            byte[] ok = new byte[4];
            rng.GetBytes(ok);
            var index = BitConverter.ToInt32(ok) % peers.Count;
            var peerClient = ClientHandler.GetNodeClient(new Uri(peers[index]));
            var peerCall = peerClient.GetChunk(new ChunkRequest()
            {
                Hash = chunk.Hash,
                TrackerUri = tracker.GetUri().ToString(),
                Offset = chunk.CurrentCount
            }, null, null, token);

            long start = chunk.CurrentCount;
            try
            {
                var peerLock = peerLocks.GetOrAdd(peers[index], new AsyncLock());
                Logger.LogInformation($"pending critical for {peers[index]}");
                using (await peerLock.LockAsync())
                {
                    await foreach (var message in peerCall.ResponseStream.ReadAllAsync(token))
                    {
                        chunk.Contents.Add(message.Response);
                        chunk.CurrentCount += message.Response.Length;
                        Logger.LogInformation($"Received {message.Response.Length} bytes");
                    }
                }
                Logger.LogInformation("chunk stream finished");
            }
            catch (Exception e)
            {
                Logger.LogWarning("Download stopped for chunk {0}, will attempt retries later", e.StackTrace);
            }
            Logger.LogInformation($"reporting progress: {chunk.CurrentCount - start}");
            await Downloads.UpdateFileProgressAsync(chunk.FileHash, chunk.CurrentCount - start);

            if (chunk.CurrentCount == chunk.Size)
            {
                byte[] thing = chunk.Contents.SelectMany(chunk => chunk.ToArray()).ToArray();
                var testHash = HashUtils.GetHash(thing.ToArray());

                if (chunk.Hash != testHash)
                {
                    Logger.LogError($"Hash mismatch for chunk {chunk.Hash.ToBase64()}");
                    chunk.Contents.Clear();
                    await Downloads.UpdateFileProgressAsync(chunk.FileHash, -chunk.CurrentCount);
                    chunk.CurrentCount = 0;
                }
                else
                {
                    await AsyncIO.WriteBufferAsync(chunk.DestinationDir, thing, chunk.Offset);

                    await ClientHandler.GetTrackerWrapper(new Uri(chunk.TrackerUri))
                        .MarkReachable([chunk.Hash], nodeURI, CancellationToken.None);

                    chunk.Status = DownloadStatus.Complete;
                }
            }
            else
            {
                chunk.Status = DownloadStatus.Paused;
            }

            return chunk;
        }

        public async Task<(ObjectWithHash[] objects, ByteString rootHash)> AddObjectFromDiskAsync(string path, int chunkSize)
        {
            ObjectWithHash[] objects = [];
            ByteString rootHash = ByteString.Empty;
            if (fs.File.Exists(path))
            {
                var obj = FilesystemUtils.GetFileObject(fs, path, chunkSize);
                rootHash = HashUtils.GetHash(obj);
                objects = [new ObjectWithHash { Hash = rootHash, Object = obj }];
            }

            if (fs.Directory.Exists(path))
            {
                List<ObjectWithHash> dirObjects = [];
                List<(ByteString, string)> paths = [];
                rootHash = FilesystemUtils.GetRecursiveDirectoryObject(fs, path, chunkSize, (hash, path, obj) =>
                {
                    dirObjects.Add(new ObjectWithHash { Hash = hash, Object = obj });
                    paths.Add((hash, path));
                });

                foreach (var (hash, p) in paths)
                {
                    await PathHandler.SetPathAsync(hash, p);
                }

                objects = dirObjects.ToArray();
            }

            return (objects, rootHash);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    cts.Dispose();
                    ClientHandler.Dispose();
                    Manager.Dispose();
                    PathHandler.Dispose();
                    BlockList.Dispose();
                    LogPath = string.Empty;
                    loggerFactory.Dispose();
                }

                disposedValue = true;
            }
        }

        ~NodeState()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
