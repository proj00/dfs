using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Node;
using Org.BouncyCastle.Utilities.Encoders;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
        public IPersistentCache<ByteString, string> PathByHash { get; }
        public IFilesystemManager Manager { get; }
        private ChannelCache NodeChannel { get; }
        private ChannelCache TrackerChannel { get; }
        public IDownloadManager Downloads { get; }

        public BlockListHandler BlockList { get; }

        private readonly ILoggerFactory loggerFactory;

        public ILogger Logger { get; private set; }
        public string LogPath { get; private set; }
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private bool disposedValue;
        public TransactionManager TransactionManager { get; } = new();
        private readonly IFileSystem fs;
        public IAsyncIOWrapper AsyncIO { get; }

        public NodeState(IFileSystem fs, TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath,
            IFilesystemManager manager, IDownloadManager downloads,
            IPersistentCache<ByteString, string> pathByHash,
            IPersistentCache<string, string> whitelist,
            IPersistentCache<string, string> blacklist,
            GrpcChannelFactory grpcChannelFactory, IAsyncIOWrapper io)
        {
            this.fs = fs;
            this.AsyncIO = io;
            this.loggerFactory = loggerFactory;
            Logger = this.loggerFactory.CreateLogger("Node");
            LogPath = logPath;
            Manager = manager;
            Downloads = downloads;

            NodeChannel = new ChannelCache(channelTtl, grpcChannelFactory);
            TrackerChannel = new ChannelCache(channelTtl, grpcChannelFactory);
            this.PathByHash = pathByHash;
            BlockList = new BlockListHandler(whitelist, blacklist);
        }

        public NodeState(TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath, string dbPath)
            : this(new FileSystem(), channelTtl, loggerFactory, logPath, new FilesystemManager(dbPath), new DownloadManager(dbPath),
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
            ), GrpcChannel.ForAddress, new AsyncIOWrapper())
        { }

        private NodeClient GetNodeClient(Uri uri, GrpcChannelOptions? options = null)
        {
            if (options == null)
            {
                options = new GrpcChannelOptions { LoggerFactory = loggerFactory };
            }
            else
            {
                options.LoggerFactory = loggerFactory;
            }
            var channel = NodeChannel.GetOrCreate(uri, options);
            return new NodeClient(channel);
        }

        public async Task RevealHashAsync(ByteString hash)
        {
            var path = await PathByHash.GetAsync(hash);
            if (!System.IO.Path.IsPathFullyQualified(path) || System.IO.Path.GetFullPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories");
            }
            Process.Start("explorer.exe", path);
        }

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

            var tracker = GetTrackerWrapper(new Uri(chunk.TrackerUri));
            Debug.Assert(chunk.Hash.Length == 64);

            List<string> peers = (await tracker.GetPeerList(new PeerRequest() { ChunkHash = chunk.Hash, MaxPeerCount = 256 }, token))
                .ToList();

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
            var peerClient = GetNodeClient(new Uri(peers[index]));
            var peerCall = peerClient.GetChunk(new ChunkRequest()
            {
                Hash = chunk.Hash,
                TrackerUri = tracker.GetUri().ToString(),
                Offset = chunk.CurrentCount
            }, null, null, token);

            try
            {
                await foreach (var message in peerCall.ResponseStream.ReadAllAsync(token))
                {
                    chunk.Contents.Add(message.Response);
                    chunk.CurrentCount += message.Response.Length;
                    Logger.LogInformation($"Received {message.Response.Length} bytes");
                    await Downloads.UpdateFileProgressAsync(chunk.FileHash, message.Response.Length);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("Download stopped for chunk {0}, will attempt retries later", e.StackTrace);
            }

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

                    await GetTrackerWrapper(new Uri(chunk.TrackerUri))
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
                    await PathByHash.SetAsync(hash, p);
                }

                objects = dirObjects.ToArray();
            }

            return (objects, rootHash);
        }

        public void RevealFile(string path)
        {
            if (System.IO.Path.GetFullPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories");
            }
            Process.Start("explorer.exe", path);
        }

        public ITrackerWrapper GetTrackerWrapper(Uri trackerUri)
        {
            ArgumentNullException.ThrowIfNull(trackerUri);
            var client = GetTrackerClient(trackerUri);
            return new TrackerWrapper(client, trackerUri);
        }

        public TrackerClient GetTrackerClient(Uri uri, GrpcChannelOptions? options = null)
        {
            if (options == null)
            {
                options = new GrpcChannelOptions { LoggerFactory = loggerFactory };
            }
            else
            {
                options.LoggerFactory = loggerFactory;
            }
            var channel = TrackerChannel.GetOrCreate(uri, options);
            return new TrackerClient(channel);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    cts.Dispose();
                    NodeChannel.Dispose();
                    TrackerChannel.Dispose();
                    Manager.Dispose();
                    PathByHash.Dispose();
                    BlockList.Dispose();
                    TransactionManager.Dispose();
                    LogPath = string.Empty;
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
