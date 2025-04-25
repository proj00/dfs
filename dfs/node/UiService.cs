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
        private NodeState state;
        private string nodeURI;
        private readonly ConcurrentDictionary<Guid, AsyncManualResetEvent> pauseEvents = new();
        private readonly ConcurrentDictionary<ByteString, Lock> fileLocks;
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
            fileLocks = new(new HashUtils.ByteStringComparer());
            ShutdownEvent = new AsyncManualResetEvent(true);
            ShutdownEvent.Reset();
            this.state = state;
            this.nodeURI = nodeURI;
            this.state.Downloads.AddChunkCompletionCallback(HandleCompleteChunkAsync);
            this.state.Downloads.AddChunkUpdateCallback(DownloadChunkAsync);
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
            (long current, long total) = state.Downloads.GetFileProgress(request.Data);
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
            var hashes = objects
                .Select(o => o.Object)
                .Where(obj => obj.TypeCase == FileSystemObject.TypeOneofCase.File)
                .SelectMany(obj => obj.File.Hashes.Hash)
                .ToArray();
            await tracker.MarkReachable(hashes, nodeURI);
        }

        public override async Task<RpcCommon.Empty> PauseFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.Downloads.PauseDownloadAsync(state.Manager.ObjectByHash[request.Data]);
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> ResumeFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.Downloads.ResumeDownloadAsync(state.Manager.ObjectByHash[request.Data]);
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> DownloadContainer(Ui.DownloadContainerOptions request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(request.TrackerUri, state);
            var guid = Guid.Parse(request.ContainerGuid);
            var hash = await tracker.GetContainerRootHash(guid);
            await pauseEvents.GetOrAdd(guid, _ => new AsyncManualResetEvent(true));
            if (!System.IO.Directory.Exists(request.DestinationDir))
            {
                throw new ArgumentException("Invalid destination directory");
            }
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

            foreach (var (chunks, file) in fileTasks)
            {
                await state.Downloads.AddNewFileAsync(file,
                    chunks,
                    chunks[0].FileHash);
            }
        }

        private (FileChunk[] chunks, IncompleteFile file) GetIncompleteFile(ObjectWithHash obj, string trackerUri, string destinationDir)
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
                    TrackerUri = trackerUri,
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

            var tracker = new TrackerWrapper(chunk.TrackerUri, state);
            Debug.Assert(chunk.Hash.Length == 128);
            var hash = ByteString.CopyFrom(chunk.Hash.Span.Slice(chunk.Hash.Length / 2));
            Debug.Assert(state.Manager.ChunkParents.ContainsKey(hash));

            List<string> peers = (await tracker.GetPeerList(new PeerRequest() { ChunkHash = hash, MaxPeerCount = 256 }, token))
                .ToList();

            if (peers.Count == 0)
            {
                chunk.Status = DownloadStatus.Pending;
                state.Logger.LogWarning($"No peers found for chunk {hash.ToBase64()}");
                return chunk;
            }

            // for now, pick a random peer (chunk tasks are persistent and we can just add simple retries later)
            var index = new Random((int)(DateTime.Now.Ticks % int.MaxValue)).Next() % peers.Count;
            var peerClient = state.GetNodeClient(new Uri(peers[index]));
            var peerCall = peerClient.GetChunk(new ChunkRequest()
            {
                Hash = hash,
                TrackerUri = tracker.GetUri(),
                Offset = chunk.CurrentCount
            }, null, null, token);

            try
            {
                await foreach (var message in peerCall.ResponseStream.ReadAllAsync(token))
                {
                    chunk.Contents.Add(message.Response);
                    chunk.CurrentCount += message.Response.Length;
                }
            }
            catch
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

                if (hash != HashUtils.GetHash(thing))
                {
                    state.Logger.LogError($"Hash mismatch for chunk {hash.ToBase64()}");
                    chunk.Contents.Clear();
                    chunk.CurrentCount = 0;
                }
                else
                {
                    chunk.Status = DownloadStatus.Complete;
                }
            }
            else
            {
                chunk.Status = DownloadStatus.Paused;
            }

            return chunk;
        }

        private async Task HandleCompleteChunkAsync(FileChunk chunk)
        {
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
            await (new TrackerWrapper(chunk.TrackerUri, state)).MarkReachable([chunk.FileHash], nodeURI);
        }

        public override async Task<RpcCommon.DataUsage> GetDataUsage(Ui.UsageRequest request, ServerCallContext context)
        {
            //var tracker = new TrackerWrapper(request.TrackerUri, state);
            //return await tracker.GetDataUsage();
            return new RpcCommon.DataUsage();
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
