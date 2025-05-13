using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Node;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;
using Tracker;

namespace node
{
    public class ObjectDownloadHandler
    {
        private readonly IFileSystem fs;
        private ILogger Logger { get; }
        private IFilePathHandler PathHandler { get; }
        public GrpcClientHandler ClientHandler { get; }
        private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
        private readonly ConcurrentDictionary<string, AsyncLock> peerLocks = new();
        public IDownloadManager Downloads { get; }
        public IAsyncIOWrapper AsyncIO { get; }
        public IFilesystemManager Manager { get; }
        private readonly INativeMethods nativeMethods;

        public ObjectDownloadHandler(IFileSystem fs,
            ILogger logger,
            IFilePathHandler pathHandler,
            GrpcClientHandler clientHandler,
            IDownloadManager downloads,
            IAsyncIOWrapper asyncIO,
            IFilesystemManager manager, INativeMethods nativeMethods)
        {
            this.fs = fs;
            Logger = logger;
            PathHandler = pathHandler;
            ClientHandler = clientHandler;
            Downloads = downloads;
            AsyncIO = asyncIO;
            Manager = manager;
            this.nativeMethods = nativeMethods;
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
                rootHash = FilesystemUtils.GetRecursiveDirectoryObject(
                    fs,
                    nativeMethods,
                    path,
                    chunkSize,
                    (hash, path, obj) =>
                        {
                            dirObjects.Add(new ObjectWithHash { Hash = hash, Object = obj });
                            paths.Add((hash, path));
                        }
                );

                foreach (var (hash, p) in paths)
                {
                    await PathHandler.SetPathAsync(hash, p);
                }

                objects = dirObjects.ToArray();
            }

            return (objects, rootHash);
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
                using (await peerLock.LockAsync(CancellationToken.None))
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
                Logger.LogWarning(e, $"Download stopped for chunk, will attempt retries later: {e.Message}");
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
    }
}
