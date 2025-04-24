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

namespace node
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class UiService : Ui.Ui.UiBase
    {
        private NodeState state;
        private string nodeURI;
        private readonly ConcurrentDictionary<Guid, AsyncManualResetEvent> pauseEvents = new();
        private readonly ConcurrentDictionary<string, System.Threading.Lock> fileLocks = new();
        private readonly ConcurrentDictionary<ByteString, System.Threading.Lock> chunkParentLocks = new();
        public AsyncManualResetEvent ShutdownEvent { get; private set; }

        public T ExceptionWrap<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.Aborted, "69"), e.ToString());
            }
        }

        public UiService(NodeState state, string nodeURI)
        {
            ShutdownEvent = new AsyncManualResetEvent(true);
            ShutdownEvent.Reset();
            this.state = state;
            this.nodeURI = nodeURI;
        }
        public override async Task<Ui.Path> GetObjectPath(RpcCommon.Hash request, ServerCallContext context)
        {
            return new Ui.Path { Path_ = state.PathByHash[request.Data] };
        }

        public override async Task<RpcCommon.Empty> RevealObjectInExplorer(RpcCommon.Hash request, ServerCallContext context)
        {
            var path = state.PathByHash[request.Data];
            Process.Start("explorer.exe", path);

            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.GuidList> GetAllContainers(RpcCommon.Empty request, ServerCallContext context)
        {
            return ExceptionWrap(() =>
            {
                var list = new RpcCommon.GuidList();

                state.Manager.Container.ForEach((guid, bs) =>
                {
                    list.Guid.Add(guid.ToString());
                    return true;
                });

                return list;
            });
        }

        public override async Task<Ui.Progress> GetDownloadProgress(RpcCommon.Hash request, ServerCallContext context)
        {
            (long current, long total) = state.FileProgress[request.Data];
            return new Ui.Progress { Current = current, Total = total };
        }

        public override async Task<ObjectList> GetContainerObjects(RpcCommon.Guid request, ServerCallContext context)
        {
            var guid = Guid.Parse(request.Guid_);
            var contents = new ObjectList();
            contents.Data.AddRange(state.Manager.GetContainerTree(guid));

            return contents;
        }

        public override async Task<Ui.SearchResponseList> SearchForObjects(Ui.SearchRequest request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(request.TrackerUri, state);
            var list = new Ui.SearchResponseList();
            list.Results.AddRange(await tracker.SearchForObjects(request.Query));
            return list;
        }

        public override async Task<RpcCommon.Hash> GetContainerRootHash(RpcCommon.Guid request, ServerCallContext context)
        {
            return new RpcCommon.Hash { Data = state.Manager.Container[Guid.Parse(request.Guid_)] };
        }

        public override async Task<RpcCommon.Guid> ImportObjectFromDisk(Ui.ObjectFromDiskOptions request, ServerCallContext context)
        {
            (string path, int chunkSize) = (request.Path, request.ChunkSize);
            if (chunkSize <= 0 || chunkSize > Constants.maxChunkSize)
            {
                throw new Exception("Invalid chunk size");
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
                rootHash = FilesystemUtils.GetRecursiveDirectoryObject(path, chunkSize, (hash, path, obj) =>
                {
                    dirObjects.Add(new ObjectWithHash { Hash = hash, Object = obj });
                    state.PathByHash[hash] = path;
                });

                objects = dirObjects.ToArray();
            }

            if (rootHash == ByteString.Empty)
            {
                throw new Exception("Invalid path");
            }

            return new RpcCommon.Guid { Guid_ = state.Manager.CreateObjectContainer(objects, rootHash).ToString() };
        }

        public override async Task<RpcCommon.Empty> PublishToTracker(Ui.PublishingOptions request, ServerCallContext context)
        {
            await PublishToTrackerAsync(Guid.Parse(request.ContainerGuid), new TrackerWrapper(request.TrackerUri, state));
            return new RpcCommon.Empty();
        }

        private async Task PublishToTrackerAsync(Guid container, ITrackerWrapper tracker)
        {
            if (!state.Manager.Container.ContainsKey(container))
            {
                throw new ArgumentException("Container not found");
            }

            var objects = state.Manager.GetContainerTree(container);

            var response = await tracker.Publish(objects
                .Select(o => new PublishedObject { Object = o, TransactionGuid = /*tbd*/"" })
                .ToList());

            _ = await tracker.SetContainerRootHash(container, state.Manager.Container[container]);
            foreach (var file in objects
                .Select(o => o.Object)
                .Where(obj => obj.TypeCase == FileSystemObject.TypeOneofCase.File))
            {
                foreach (var chunk in file.File.Hashes.Hash)
                {
                    await tracker.MarkReachable(chunk, nodeURI);
                }
            }
        }

        public override async Task<RpcCommon.Empty> PauseContainerDownload(RpcCommon.Guid request, ServerCallContext context)
        {
            var guid = Guid.Parse(request.Guid_);
            var pauseEvent = pauseEvents.GetOrAdd(guid, _ => new AsyncManualResetEvent(true));
            pauseEvent.Reset();
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> ResumeContainerDownload(RpcCommon.Guid request, ServerCallContext context)
        {
            var guid = Guid.Parse(request.Guid_);
            if (pauseEvents.TryGetValue(guid, out var pauseEvent))
            {
                pauseEvent.Set();
            }
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> DownloadContainer(Ui.DownloadContainerOptions request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(request.TrackerUri, state);
            var guid = Guid.Parse(request.ContainerGuid);
            var hash = await tracker.GetContainerRootHash(guid);
            await pauseEvents.GetOrAdd(guid, _ => new AsyncManualResetEvent(true));
            await DownloadObjectByHash(hash, guid, tracker, request.DestinationDir, request.MaxConcurrentChunks);

            return new RpcCommon.Empty();
        }

        private async Task DownloadObjectByHash(ByteString hash, Guid? guid, ITrackerWrapper tracker, string destinationDir, int maxConcurrentChunks)
        {
            List<ObjectWithHash> objects = await tracker.GetObjectTree(hash);
            guid = state.Manager.CreateObjectContainer(objects.ToArray(), hash, guid);

            var semaphore = new SemaphoreSlim(maxConcurrentChunks);
            var fileTasks = objects
                .Where(obj => obj.Object.TypeCase == FileSystemObject.TypeOneofCase.File)
                .Select(obj => GetIncompleteFile(obj, tracker.GetUri(), destinationDir));

            await Task.WhenAll(fileTasks);
        }

        private IncompleteFile GetIncompleteFile(ObjectWithHash obj, string trackerUri, string destinationDir)
        {
            var dir = @"\\?\" + destinationDir + "\\" + Hex.ToHexString(obj.Hash.ToByteArray());
            System.IO.Directory.CreateDirectory(dir);
            dir = dir + "\\" + obj.Object.Name;

            state.FileProgress[obj.Hash] = (0, obj.Object.File.Size);
            var i = 0;
            IncompleteFile file = new() { DestinationDir = dir, File = obj, TrackerUri = trackerUri };
            foreach (var hash in obj.Object.File.Hashes.Hash)
            {
                var chunk = new FileChunk()
                {
                    Hash = obj.Object.File.Hashes.Hash[i],
                    Offset = obj.Object.File.Hashes.ChunkSize * i,
                    FileHash = obj.Hash
                };

                file.RemainingChunks.Add(chunk);
                i++;
            }

            state.PathByHash[obj.Hash] = dir;
            return file;
        }

        private async Task DownloadChunkAsync(FileChunk chunk, int chunkSize, string trackerUri, CancellationToken token)
        {
            var tracker = new TrackerWrapper(trackerUri, state);
            List<string> peers = (await tracker.GetPeerList(new PeerRequest() { ChunkHash = chunk.Hash, MaxPeerCount = 256 }, token))
                .ToList();

            if (peers.Count == 0)
            {
                state.Logger.LogWarning($"No peers found for chunk {chunk.Hash.ToBase64()}");
                return;
            }

            // for now, pick a random peer (chunk tasks are persistent and we can just add simple retries later)
            var index = new Random((int)(DateTime.Now.Ticks % int.MaxValue)).Next() % peers.Count;
            var peerClient = state.GetNodeClient(new Uri(peers[index]));
            var peerCall = peerClient.GetChunk(new ChunkRequest()
            {
                Hash = chunk.FileHash,
                TrackerUri = tracker.GetUri(),
                Offset = chunk.CurrentCount
            }, null, null, token);

            try
            {
                await foreach (var message in peerCall.ResponseStream.ReadAllAsync(token))
                {
                    chunk.Contents.Add(message.Response);
                    chunk.CurrentCount += message.Response.Length;
                    state.UpdateFileProgress(chunk.FileHash, message.Response.Length);
                }
            }
            catch (OperationCanceledException)
            {
                state.Logger.LogWarning("Download stoped for chunk {0}, will attempt retries later", chunk.Hash);
            }

            if (chunk.CurrentCount == chunkSize)
            {
                state.CompleteChunks[chunk.Hash] = chunk;
            }
        }

        private void HandleCompleteChunk(FileChunk chunk)
        {
            var parentLock = chunkParentLocks.GetOrAdd(chunk.FileHash, _ => new Lock());

            IncompleteFile? parentFile = null;
            lock (parentLock)
            {
                parentFile = state.IncompleteFiles[chunk.FileHash];
                parentFile.RemainingChunks.Remove(chunk);
                if (parentFile.RemainingChunks.Count > 0)
                {
                    state.IncompleteFiles[chunk.FileHash] = parentFile;
                }
                else
                {
                    state.IncompleteFiles.Remove(chunk.FileHash);
                }
            }
            if (parentFile == null)
            {
                throw new ArgumentNullException();
            }

            var fileLock = fileLocks.GetOrAdd(parentFile.DestinationDir, _ => new Lock());
            lock (fileLock)
            {
                using var stream = new FileStream(parentFile.DestinationDir, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                stream.Seek(chunk.Offset, SeekOrigin.Begin);
                foreach (var data in chunk.Contents)
                {
                    stream.Write(data.ToByteArray());
                }
            }
        }

        public override async Task<RpcCommon.DataUsage> GetDataUsage(Ui.UsageRequest request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(request.TrackerUri, state);
            return await tracker.GetDataUsage();
        }

        public override async Task<RpcCommon.Empty> Shutdown(RpcCommon.Empty request, ServerCallContext context)
        {
            ShutdownEvent.Set();
            return new RpcCommon.Empty();
        }

        public override Task<RpcCommon.Empty> CancelContainerDownload(RpcCommon.Guid request, ServerCallContext context)
        {
            return base.CancelContainerDownload(request, context);
        }

        public override async Task<RpcCommon.Empty> ModifyBlockListEntry(BlockListRequest request, ServerCallContext context)
        {
            state.FixBlockList(request);
            return new RpcCommon.Empty();
        }

        public override async Task<BlockListResponse> GetBlockList(RpcCommon.Empty request, ServerCallContext context)
        {
            return state.GetBlockList();
        }

        public override async Task<RpcCommon.Empty> LogMessage(LogRequest request, ServerCallContext context)
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
        }

        public override async Task<RpcCommon.Empty> RevealLogFile(RpcCommon.Empty request, ServerCallContext context)
        {
            Process.Start("explorer.exe", state.LogPath);

            return new RpcCommon.Empty();
        }

        public override Task<RpcCommon.Empty> ApplyFsOperation(FsOperation request, ServerCallContext context)
        {
            return base.ApplyFsOperation(request, context);
        }
    }
}
