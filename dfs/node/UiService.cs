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
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.Ocsp;

namespace node
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class UiService : Ui.Ui.UiBase
    {
        private readonly NodeState state;
        private readonly Uri nodeURI;
        private readonly ConcurrentDictionary<Guid, AsyncManualResetEvent> pauseEvents = new();
        public AsyncManualResetEvent ShutdownEvent { get; private set; }

        public UiService(NodeState state, Uri nodeURI)
        {
            ShutdownEvent = new AsyncManualResetEvent(true);
            ShutdownEvent.Reset();
            this.state = state;
            this.nodeURI = nodeURI;
            this.state.Downloads.AddChunkUpdateCallback((chunk, token) => state.DownloadChunkAsync(chunk, nodeURI, token));
        }
        public override async Task<Ui.Path> GetObjectPath(RpcCommon.Hash request, ServerCallContext context)
        {
            return new Ui.Path { Path_ = await state.PathByHash.GetAsync(request.Data) };
        }

        public override async Task<RpcCommon.Empty> RevealObjectInExplorer(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.RevealHashAsync(request.Data);

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
            var tracker = state.GetTrackerWrapper(new Uri(request.TrackerUri));
            var list = new Ui.SearchResponseList();
            list.Results.AddRange(await tracker.SearchForObjects(request.Query, context.CancellationToken));
            return list;
        }

        public override async Task<RpcCommon.Hash> GetContainerRootHash(RpcCommon.Guid request, ServerCallContext context)
        {
            return new RpcCommon.Hash { Data = await state.Manager.Container.GetAsync(Guid.Parse(request.Guid_)) };
        }

        public override async Task<RpcCommon.Guid> ImportObjectToContainer(Ui.ObjectFromDiskOptions request, ServerCallContext context)
        {
            (ObjectWithHash[] objects, ByteString rootHash) = await ImportObjectAsync(request);

            return new RpcCommon.Guid { Guid_ = (await state.Manager.CreateObjectContainer(objects, rootHash, Guid.NewGuid())).ToString() };
        }

        private async Task<(ObjectWithHash[] objects, ByteString rootHash)> ImportObjectAsync(ObjectFromDiskOptions request)
        {
            (string path, int chunkSize) = (request.Path, request.ChunkSize);
            if (chunkSize <= 0 || chunkSize > Constants.maxChunkSize)
            {
                throw new RpcException(Grpc.Core.Status.DefaultCancelled, "Invalid chunk size");
            }

            (ObjectWithHash[] objects, ByteString rootHash) = await state.AddObjectFromDiskAsync(path, chunkSize);

            if (rootHash == ByteString.Empty)
            {
                throw new ArgumentException("Invalid path");
            }

            return (objects, rootHash);
        }

        public override async Task<ObjectList> ImportObjectFromDisk(Ui.ObjectFromDiskOptions request, ServerCallContext context)
        {
            (ObjectWithHash[] objects, _) = await ImportObjectAsync(request);

            return new() { Data = { objects } };
        }

        public override async Task<RpcCommon.Empty> PublishToTracker(Ui.PublishingOptions request, ServerCallContext context)
        {
            await PublishToTrackerAsync(Guid.Parse(request.ContainerGuid), state.GetTrackerWrapper(new Uri(request.TrackerUri)));
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
            await PublishContainerUpdateAsync(container, tracker, objects, rootHash);
        }

        private async Task PublishContainerUpdateAsync(Guid container, ITrackerWrapper tracker, List<ObjectWithHash> objects, ByteString rootHash)
        {
            Guid newGuid = await state.TransactionManager.PublishObjectsAsync(tracker, container, objects,
                            rootHash);

            await state.Manager.Container.SetAsync(newGuid, rootHash);

            var hashes = objects
                .Select(o => o.Object)
                .Where(obj => obj.TypeCase == FileSystemObject.TypeOneofCase.File)
                .SelectMany(obj => obj.File.Hashes.Hash)
                .ToArray();
            await tracker.MarkReachable(hashes, nodeURI, CancellationToken.None);
        }

        public override async Task<RpcCommon.Empty> PauseFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.Downloads.PauseDownloadAsync(await state.Manager.ObjectByHash.GetAsync(request.Data), context.CancellationToken);
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> ResumeFileDownload(RpcCommon.Hash request, ServerCallContext context)
        {
            await state.Downloads.ResumeDownloadAsync(await state.Manager.ObjectByHash.GetAsync(request.Data), context.CancellationToken);
            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.Empty> DownloadContainer(Ui.DownloadContainerOptions request, ServerCallContext context)
        {
            var tracker = state.GetTrackerWrapper(new Uri(request.TrackerUri));
            var guid = Guid.Parse(request.ContainerGuid);
            var hash = await tracker.GetContainerRootHash(guid, CancellationToken.None);
            await pauseEvents.GetOrAdd(guid, _ => new AsyncManualResetEvent(true));

            await state.DownloadObjectByHashAsync(hash, guid, tracker, request.DestinationDir);

            return new RpcCommon.Empty();
        }

        public override async Task<RpcCommon.DataUsage> GetDataUsage(Ui.UsageRequest request, ServerCallContext context)
        {
            var tracker = state.GetTrackerWrapper(new Uri(request.TrackerUri));
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
            await state.BlockList.FixBlockListAsync(request);
            return new RpcCommon.Empty();
        }

        public override async Task<BlockListResponse> GetBlockList(RpcCommon.Empty request, ServerCallContext context)
        {
            return await state.BlockList.GetBlockListAsync();
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
            state.RevealFile(state.LogPath);
            return await Task.FromResult(new RpcCommon.Empty());
        }

        public override async Task<RpcCommon.Empty> ApplyFsOperation(FsOperation request, ServerCallContext context)
        {
            (ByteString newRoot, List<ObjectWithHash> newObjects) = await state.Manager.ModifyContainer(request);
            var guid = Guid.Parse(request.ContainerGuid);
            if (request.HasTrackerUri)
            {
                var tracker = state.GetTrackerWrapper(new Uri(request.TrackerUri));
                await PublishContainerUpdateAsync(guid, tracker, newObjects, newRoot);
            }
            else
            {
                state.Logger.LogWarning("state isn't synced with tracker");
                await state.Manager.CreateObjectContainer([.. newObjects], newRoot, guid);
            }

            return new();
        }
    }
}
