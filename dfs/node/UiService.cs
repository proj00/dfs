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

namespace node
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class UiService : Ui.Ui.UiBase
    {
        private class FileStateChange
        {
            public ByteString fileHash;
            public FileStatus newStatus;
        }

        private NodeState state;
        private string nodeURI;
        private readonly ConcurrentDictionary<Guid, AsyncManualResetEvent> pauseEvents = new();
        private readonly ConcurrentDictionary<ByteString, System.Threading.Lock> fileLocks;
        private readonly ConcurrentDictionary<ByteString, System.Threading.Lock> chunkLocks;
        private readonly Channel<FileStateChange> stateChannel = Channel.CreateUnbounded<FileStateChange>();
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
            chunkLocks = new(new HashUtils.ByteStringComparer());
            fileLocks = new(new HashUtils.ByteStringComparer());
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

        public override async Task<RpcCommon.Empty> PauseFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            var fileLock = fileLocks.GetOrAdd(request.Data, _ => new Lock());
            lock (fileLock)
            {
                var file = state.IncompleteFiles[request.Data];
                if (file.Status == FileStatus.Active)
                {
                    file.Status = FileStatus.Paused;
                }
                state.IncompleteFiles[request.Data] = file;
            }
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> ResumeFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            var fileLock = fileLocks.GetOrAdd(request.Data, _ => new Lock());
            lock (fileLock)
            {
                var file = state.IncompleteFiles[request.Data];
                if (file.Status == FileStatus.Paused)
                {
                    file.Status = FileStatus.Pending;
                }
                state.IncompleteFiles[request.Data] = file;

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

            var fileTasks = objects
                .Where(obj => obj.Object.TypeCase == FileSystemObject.TypeOneofCase.File)
                .Select(obj => GetIncompleteFile(obj, tracker.GetUri(), destinationDir));

            foreach (var incompleteFile in fileTasks)
            {
                state.IncompleteFiles[incompleteFile.File.Hash] = incompleteFile;
                state.FileProgress[incompleteFile.File.Hash] = (0, incompleteFile.File.Object.File.Size);
                state.PathByHash[incompleteFile.File.Hash] = incompleteFile.DestinationDir;
            }
        }

        private IncompleteFile GetIncompleteFile(ObjectWithHash obj, string trackerUri, string destinationDir)
        {
            var dir = @"\\?\" + destinationDir + "\\" + Hex.ToHexString(obj.Hash.ToByteArray());
            System.IO.Directory.CreateDirectory(dir);
            dir = dir + "\\" + obj.Object.Name;

            var i = 0;
            IncompleteFile file = new() { DestinationDir = dir, File = obj, TrackerUri = trackerUri };
            foreach (var hash in obj.Object.File.Hashes.Hash)
            {
                var chunk = new FileChunk()
                {
                    Hash = obj.Object.File.Hashes.Hash[i],
                    Offset = obj.Object.File.Hashes.ChunkSize * i,
                    FileHash = obj.Hash,
                    Size = Math.Min(obj.Object.File.Hashes.ChunkSize, obj.Object.File.Size - obj.Object.File.Hashes.ChunkSize * i)
                };

                state.IncompleteChunks[HashUtils.ConcatHashes([chunk.FileHash, chunk.Hash])] = chunk;
                i++;
            }

            return file;
        }

        private async Task UpdateChunkAsync(FileChunk chunk, CancellationToken token)
        {
            chunk = await DownloadChunkAsync(chunk, token);
            state.IncompleteChunks[HashUtils.GetChunkHash(chunk)] = chunk;
        }

        private async Task<FileChunk> DownloadChunkAsync(FileChunk chunk, CancellationToken token)
        {
            if (chunk.CurrentCount == chunk.Size)
            {
                throw new ArgumentException("Already downloaded");
            }

            var tracker = new TrackerWrapper(chunk.TrackerUri, state);
            List<string> peers = (await tracker.GetPeerList(new PeerRequest() { ChunkHash = chunk.Hash, MaxPeerCount = 256 }, token))
                .ToList();

            if (peers.Count == 0)
            {
                state.Logger.LogWarning($"No peers found for chunk {chunk.Hash.ToBase64()}");
                return chunk;
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
                state.Logger.LogWarning("Download stopped for chunk {0}, will attempt retries later", chunk.Hash);
            }

            if (chunk.CurrentCount == chunk.Size)
            {
                byte[] thing = new byte[chunk.Size];
                int offset = 0;
                foreach (var data in chunk.Contents)
                {
                    Array.Copy(data.ToByteArray(), 0, thing, offset, data.Length);
                    offset += data.Length;
                }

                if (chunk.Hash != HashUtils.GetHash(thing))
                {
                    state.Logger.LogError($"Hash mismatch for chunk {chunk.Hash.ToBase64()}");
                    chunk.Contents.Clear();
                    state.UpdateFileProgress(chunk.FileHash, -chunk.Size);
                    chunk.CurrentCount = 0;
                }
            }

            return chunk;
        }

        private async Task HandleCompleteChunkAsync(FileChunk chunk)
        {
            var chunkLock = chunkLocks.GetOrAdd(HashUtils.GetChunkHash(chunk), _ => new Lock());

            bool isComplete = false;
            lock (chunkLock)
            {
                state.IncompleteChunks.Remove(HashUtils.GetChunkHash(chunk));
                int remaining = 0;
                state.IncompleteChunks.PrefixScan(chunk.FileHash, (k, v) =>
                {
                    remaining++;
                });

                if (remaining == 0)
                {
                    isComplete = true;
                }
            }

            var fileLock = fileLocks.GetOrAdd(chunk.FileHash, _ => new Lock());
            lock (fileLock)
            {
                using var stream = new FileStream(chunk.DestinationDir, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                stream.Seek(chunk.Offset, SeekOrigin.Begin);
                foreach (var data in chunk.Contents)
                {
                    stream.Write(data.Span);
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
