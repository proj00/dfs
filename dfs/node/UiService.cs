using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tracker;
using Microsoft.VisualStudio.Threading;
using Grpc.Core.Utils;
using Ui;
using Microsoft.Extensions.Logging;
using Node;
using System.Threading.Channels;
using System.Security.Cryptography;

namespace node
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class UiService : Ui.Ui.UiBase
    {
        private readonly NodeState state;
        private readonly TransactionManager transactionManager = new();
        private readonly Uri nodeURI;
        private readonly ConcurrentDictionary<Guid, AsyncManualResetEvent> pauseEvents = new();
        private readonly ConcurrentDictionary<ByteString, AsyncLock> fileLocks;
        private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
        public AsyncManualResetEvent ShutdownEvent { get; private set; }

        public UiService(NodeState state, Uri nodeURI)
        {
            fileLocks = new(new ByteStringComparer());
            ShutdownEvent = new AsyncManualResetEvent(true);
            ShutdownEvent.Reset();
            this.state = state;
            this.nodeURI = nodeURI;
            this.state.Downloads.AddChunkUpdateCallback(DownloadChunkAsync);
        }
        public override async Task<Ui.Path> GetObjectPath(RpcCommon.Hash request, ServerCallContext context)
        {
            return new Ui.Path { Path_ = await state.PathByHash.GetAsync(request.Data) };
        }

        public override async Task<RpcCommon.Empty> RevealObjectInExplorer(RpcCommon.Hash request, ServerCallContext context)
        {
            var path = await state.PathByHash.GetAsync(request.Data);
            Process.Start("explorer.exe", path);

            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.GuidList> GetAllContainers(RpcCommon.Empty request, ServerCallContext context)
        {
            var list = new RpcCommon.GuidList();

            await state.Manager.Container.ForEach((guid, bs) =>
            {
                list.Guid.Add(guid.ToString());
                return true;
            });

            return list;
        }

        public override async Task<Ui.Progress> GetDownloadProgress(RpcCommon.Hash request, ServerCallContext context)
        {
            return await state.Downloads.GetFileProgressAsync(request.Data);
        }

        public override async Task<ObjectList> GetContainerObjects(RpcCommon.Guid request, ServerCallContext context)
        {
            var guid = Guid.Parse(request.Guid_);
            var contents = new ObjectList();
            contents.Data.AddRange(await state.Manager.GetContainerTree(guid));

            return contents;
        }

        public override async Task<Ui.SearchResponseList> SearchForObjects(Ui.SearchRequest request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(new Uri(request.TrackerUri), state, CancellationToken.None);
            var list = new Ui.SearchResponseList();
            list.Results.AddRange(await tracker.SearchForObjects(request.Query, context.CancellationToken));
            return list;
        }

        public override async Task<RpcCommon.Hash> GetContainerRootHash(RpcCommon.Guid request, ServerCallContext context)
        {
            return new RpcCommon.Hash { Data = await state.Manager.Container.GetAsync(Guid.Parse(request.Guid_)) };
        }

        public override async Task<RpcCommon.Guid> ImportObjectFromDisk(Ui.ObjectFromDiskOptions request, ServerCallContext context)
        {
            (string path, int chunkSize) = (request.Path, request.ChunkSize);
            if (chunkSize <= 0 || chunkSize > Constants.maxChunkSize)
            {
                throw new RpcException(Grpc.Core.Status.DefaultCancelled, "Invalid chunk size");
            }

            ObjectWithHash[] objects = [];
            ByteString rootHash = ByteString.Empty;
            if (System.IO.File.Exists(path))
            {
                var obj = FilesystemUtils.GetFileObject(path, chunkSize);
                rootHash = HashUtils.GetHash(obj);
                objects = [new ObjectWithHash { Hash = rootHash, Object = obj }];
            }

            if (System.IO.Directory.Exists(path))
            {
                List<ObjectWithHash> dirObjects = [];
                List<(ByteString, string)> paths = [];
                rootHash = FilesystemUtils.GetRecursiveDirectoryObject(path, chunkSize, (hash, path, obj) =>
                {
                    dirObjects.Add(new ObjectWithHash { Hash = hash, Object = obj });
                    paths.Add((hash, path));
                });

                foreach (var (hash, p) in paths)
                {
                    await state.PathByHash.SetAsync(hash, p);
                }

                objects = dirObjects.ToArray();
            }

            if (rootHash == ByteString.Empty)
            {
                throw new ArgumentException("Invalid path");
            }

            return new RpcCommon.Guid { Guid_ = (await state.Manager.CreateObjectContainer(objects, rootHash, Guid.NewGuid())).ToString() };
        }

        public override async Task<RpcCommon.Empty> PublishToTracker(Ui.PublishingOptions request, ServerCallContext context)
        {
            await PublishToTrackerAsync(Guid.Parse(request.ContainerGuid), new TrackerWrapper(new Uri(request.TrackerUri), state, context.CancellationToken));
            return new RpcCommon.Empty();
        }

        private async Task PublishToTrackerAsync(Guid container, ITrackerWrapper tracker)
        {
            if (!await state.Manager.Container.ContainsKey(container))
            {
                throw new ArgumentException("Container not found");
            }

            var objects = await state.Manager.GetContainerTree(container);
            var rootHash = await state.Manager.Container.GetAsync(container);
            Guid newGuid = await transactionManager.PublishObjectsAsync(tracker, container, objects,
                rootHash);

            await state.Manager.Container.SetAsync(newGuid, rootHash);
            await state.Manager.Container.Remove(newGuid);

            var hashes = objects
                .Select(o => o.Object)
                .Where(obj => obj.TypeCase == FileSystemObject.TypeOneofCase.File)
                .SelectMany(obj => obj.File.Hashes.Hash)
                .ToArray();
            await tracker.MarkReachable(hashes, nodeURI, CancellationToken.None);
        }

        public override async Task<RpcCommon.Empty> PauseFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.Downloads.PauseDownloadAsync(await state.Manager.ObjectByHash.GetAsync(request.Data));
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> ResumeFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.Downloads.ResumeDownloadAsync(await state.Manager.ObjectByHash.GetAsync(request.Data));
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> DownloadContainer(Ui.DownloadContainerOptions request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(new Uri(request.TrackerUri), state, context.CancellationToken);
            var guid = Guid.Parse(request.ContainerGuid);
            var hash = await tracker.GetContainerRootHash(guid, CancellationToken.None);
            await pauseEvents.GetOrAdd(guid, _ => new AsyncManualResetEvent(true));
            if (!System.IO.Directory.Exists(request.DestinationDir))
            {
                throw new ArgumentException($"Invalid destination directory: path {request.DestinationDir} doesn't exist");
            }
            await DownloadObjectByHashAsync(hash, guid, tracker, request.DestinationDir);

            return new RpcCommon.Empty();
        }

        private async Task DownloadObjectByHashAsync(ByteString hash, Guid? guid, ITrackerWrapper tracker, string destinationDir)
        {
            List<ObjectWithHash> objects = await tracker.GetObjectTree(hash, CancellationToken.None);
            guid = await state.Manager.CreateObjectContainer(objects.ToArray(), hash, guid ?? Guid.NewGuid());

            var fileTasks = objects
                .Where(obj => obj.Object.TypeCase == FileSystemObject.TypeOneofCase.File)
                .Select(obj => GetIncompleteFile(obj, tracker.GetUri(), destinationDir));

            foreach (var (chunks, file) in fileTasks)
            {
                await state.Downloads.AddNewFileAsync(file,
                    chunks);
            }
        }

        private static (FileChunk[] chunks, IncompleteFile file) GetIncompleteFile(ObjectWithHash obj, Uri trackerUri, string destinationDir)
        {
            var dir = @"\\?\" + destinationDir + "\\" + Hex.ToHexString(obj.Hash.ToByteArray());
            System.IO.Directory.CreateDirectory(dir);
            dir = dir + "\\" + obj.Object.Name;

            List<FileChunk> chunks = [];
            var i = 0;
            foreach (var hash in obj.Object.File.Hashes.Hash)
            {
                chunks.Add(new()
                {
                    Hash = HashUtils.ConcatHashes([obj.Hash, hash]),
                    Offset = obj.Object.File.Hashes.ChunkSize * i,
                    FileHash = obj.Hash,
                    Size = Math.Min(obj.Object.File.Hashes.ChunkSize, obj.Object.File.Size - obj.Object.File.Hashes.ChunkSize * i),
                    TrackerUri = trackerUri.ToString(),
                    DestinationDir = dir,
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

        private async Task<FileChunk> DownloadChunkAsync(FileChunk chunk, CancellationToken token)
        {
            if (chunk.Status == DownloadStatus.Complete)
            {
                throw new ArgumentException("Already downloaded");
            }

            var tracker = new TrackerWrapper(new Uri(chunk.TrackerUri), state, CancellationToken.None);
            Debug.Assert(chunk.Hash.Length == 128);
            var hash = ByteString.CopyFrom(chunk.Hash.Span.Slice(chunk.Hash.Length / 2));

            List<string> peers = (await tracker.GetPeerList(new PeerRequest() { ChunkHash = hash, MaxPeerCount = 256 }, token))
                .ToList();

            if (peers.Count == 0)
            {
                chunk.Status = DownloadStatus.Pending;
                Debug.Assert(false, "no peers");
                state.Logger.LogWarning($"No peers found for chunk {hash.ToBase64()}");
                return chunk;
            }

            // for now, pick a random peer (chunk tasks are persistent and we can just add simple retries later)
            byte[] ok = new byte[4];
            rng.GetBytes(ok);
            var index = BitConverter.ToInt32(ok) % peers.Count;
            var peerClient = state.GetNodeClient(new Uri(peers[index]));
            var peerCall = peerClient.GetChunk(new ChunkRequest()
            {
                Hash = hash,
                TrackerUri = tracker.GetUri().ToString(),
                Offset = chunk.CurrentCount
            }, null, null, token);

            try
            {
                await foreach (var message in peerCall.ResponseStream.ReadAllAsync(token))
                {
                    chunk.Contents.Add(message.Response);
                    chunk.CurrentCount += message.Response.Length;
                    state.Logger.LogInformation($"Received {message.Response.Length} bytes");
                    await state.Downloads.UpdateFileProgressAsync(chunk.FileHash, message.Response.Length);
                }
            }
            catch (Exception e)
            {
                state.Logger.LogWarning("Download stopped for chunk {0}, will attempt retries later", e.StackTrace);
            }

            if (chunk.CurrentCount == chunk.Size)
            {
                byte[] thing = chunk.Contents.SelectMany(chunk => chunk.ToArray()).ToArray();
                var testHash = HashUtils.GetHash(thing.ToArray());

                if (hash != testHash)
                {
                    state.Logger.LogError($"Hash mismatch for chunk {hash.ToBase64()}");
                    chunk.Contents.Clear();
                    await state.Downloads.UpdateFileProgressAsync(chunk.FileHash, -chunk.CurrentCount);
                    chunk.CurrentCount = 0;
                }
                else
                {
                    var fileLock = fileLocks.GetOrAdd(chunk.FileHash, _ => new AsyncLock());
                    using (await fileLock.LockAsync())
                    {
                        using var stream = new FileStream(chunk.DestinationDir, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                        stream.Seek(chunk.Offset, SeekOrigin.Begin);
                        await stream.WriteAsync(thing, CancellationToken.None);
                    }
                    await (new TrackerWrapper(new Uri(chunk.TrackerUri), state, CancellationToken.None)).MarkReachable([chunk.FileHash], nodeURI, CancellationToken.None);
                    chunk.Status = DownloadStatus.Complete;
                }
            }
            else
            {
                chunk.Status = DownloadStatus.Paused;
            }

            return chunk;
        }

        public override async Task<RpcCommon.DataUsage> GetDataUsage(Ui.UsageRequest request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(new Uri(request.TrackerUri), state, context.CancellationToken);
            return await tracker.GetDataUsage(context.CancellationToken);
        }

        public override async Task<RpcCommon.Empty> Shutdown(RpcCommon.Empty request, ServerCallContext context)
        {
            return await Task.Run(() =>
            {
                ShutdownEvent.Set();
                return new RpcCommon.Empty();
            });
        }

        public override async Task<RpcCommon.Empty> ModifyBlockListEntry(BlockListRequest request, ServerCallContext context)
        {
            await state.FixBlockListAsync(request);
            return new RpcCommon.Empty();
        }

        public override async Task<BlockListResponse> GetBlockList(RpcCommon.Empty request, ServerCallContext context)
        {
            return await state.GetBlockListAsync();
        }

        public override async Task<RpcCommon.Empty> LogMessage(LogRequest request, ServerCallContext context)
        {
            return await Task.Run(() =>
            {
                switch (request.Category)
                {
                    case LogCategory.Error:
                        state.Logger.LogError(request.Message);
                        break;
                    case LogCategory.Warning:
                        state.Logger.LogWarning(request.Message);
                        break;
                    case LogCategory.Info:
                        state.Logger.LogInformation(request.Message);
                        break;
                    case LogCategory.Debug:
                        state.Logger.LogDebug(request.Message);
                        break;
                    case LogCategory.Trace:
                        state.Logger.LogTrace(request.Message);
                        break;
                    default:
                        state.Logger.LogInformation(request.Message);
                        break;
                }
                return new RpcCommon.Empty();
            });
        }

        public override async Task<RpcCommon.Empty> RevealLogFile(RpcCommon.Empty request, ServerCallContext context)
        {
            return await Task.Run(() =>
            {
                Process.Start("explorer.exe", state.LogPath);
                return new RpcCommon.Empty();
            });
        }

        public override Task<RpcCommon.Empty> ApplyFsOperation(FsOperation request, ServerCallContext context)
        {
            state.Logger.LogInformation($"ApplyFsOperation: {request.Operation} {request.Source} {request.Destination}");
            return Task.Run(() => new RpcCommon.Empty());
            //return base.ApplyFsOperation(request, context);
        }
    }
}
