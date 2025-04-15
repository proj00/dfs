using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Org.BouncyCastle.Utilities.Encoders;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tracker;

namespace node.IpcService
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class UiService : Ui.Ui.UiBase
    {
        private NodeState state;
        private NodeRpc rpc;
        private Func<UI?> getUI;
        private string nodeURI;

        public UiService(NodeState state, NodeRpc rpc, Func<UI?> getUI, string nodeURI)
        {
            this.state = state;
            this.rpc = rpc;
            this.getUI = getUI;
            this.nodeURI = nodeURI;
        }

        public override async Task<Ui.Path> PickObjectPath(Ui.ObjectOptions request, ServerCallContext context)
        {
            var ui = getUI();
            if (ui == null)
            {
                throw new NullReferenceException();
            }

            string result = "";
            ui.Invoke(() =>
            {
                result = PickObjectPathInternal(request.PickFolder);
            });

            return new Ui.Path { Path_ = result };
        }

        private static string PickObjectPathInternal(bool folder)
        {
            if (folder)
            {
                using var dialog = new FolderBrowserDialog();
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedPath) && System.IO.Directory.Exists(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
            }
            else
            {
                using var dialog = new OpenFileDialog();
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.FileName) && System.IO.File.Exists(dialog.FileName))
                {
                    return dialog.FileName;
                }
            }

            throw new Exception("Dialog failed");
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
            var list = new RpcCommon.GuidList();
            list.Guid.AddRange(state.Manager.Container.Select(guid => guid.Key.ToString()));
            return list;
        }

        public override async Task<RpcCommon.Empty> CopyToClipboard(Ui.String request, ServerCallContext context)
        {
            var ui = getUI();
            if (ui == null)
            {
                throw new NullReferenceException();
            }

            ui.Invoke(() =>
            {
                Clipboard.SetText(request.Value);
            });

            return new RpcCommon.Empty();
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
            await PublishToTracker(Guid.Parse(request.ContainerGuid), new TrackerWrapper(request.TrackerUri, state));
            return new RpcCommon.Empty();
        }

        private async Task PublishToTracker(Guid container, ITrackerWrapper tracker)
        {
            if (!state.Manager.Container.ContainsKey(container))
            {
                throw new ArgumentException("Container not found");
            }

            var objects = state.Manager.GetContainerTree(container);

            var response = await tracker.Publish(objects);
            _ = await tracker.SetContainerRootHash(container, state.Manager.Container[container]);
            foreach (var file in objects
                .Select(o => o.Object)
                .Where(obj => obj.TypeCase == Fs.FileSystemObject.TypeOneofCase.File))
            {
                foreach (var chunk in file.File.Hashes.Hash)
                {
                    await tracker.MarkReachable(chunk, nodeURI);
                }
            }
        }

        public override async Task<RpcCommon.Empty> DownloadContainer(Ui.DownloadContainerOptions request, ServerCallContext context)
        {
            var tracker = new TrackerWrapper(request.TrackerUri, state);
            var hash = await tracker.GetContainerRootHash(Guid.Parse(request.ContainerGuid));
            await DownloadObjectByHash(hash, Guid.Parse(request.ContainerGuid), tracker, request.DestinationDir, request.MaxConcurrentChunks);

            return new RpcCommon.Empty();
        }

        private async Task DownloadObjectByHash(ByteString hash, Guid? guid, ITrackerWrapper tracker, string destinationDir, int maxConcurrentChunks)
        {
            List<ObjectWithHash> objects = await tracker.GetObjectTree(hash);
            state.Manager.CreateObjectContainer(objects.ToArray(), hash, guid);

            var semaphore = new SemaphoreSlim(maxConcurrentChunks);
            var fileTasks = objects
                .Where(obj => obj.Object.TypeCase == Fs.FileSystemObject.TypeOneofCase.File)
                .Select(obj => DownloadFile(obj, tracker, destinationDir, semaphore));

            await Task.WhenAll(fileTasks);
        }

        private async Task DownloadFile(ObjectWithHash obj, ITrackerWrapper tracker, string destinationDir, SemaphoreSlim semaphore)
        {
            List<Task> chunkTasks = [];

            var dir = @"\\?\" + destinationDir + "\\" + Hex.ToHexString(obj.Hash.ToByteArray());
            System.IO.Directory.CreateDirectory(dir);
            dir = dir + "\\" + obj.Object.Name;
            using var stream = new FileStream(dir, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            object streamLock = new();

            state.FileProgress[obj.Hash] = (0, obj.Object.File.Size);
            var i = 0;
            foreach (var hash in obj.Object.File.Hashes.Hash)
            {
                chunkTasks.Add(DownloadChunk(hash, obj.Hash, obj.Object.File.Hashes.ChunkSize * i, tracker, stream, streamLock, semaphore));
                i++;
            }

            await Task.WhenAll(chunkTasks);
            state.PathByHash[obj.Hash] = dir;
        }

        private async Task DownloadChunk(ByteString hash, ByteString fileHash, int chunkOffset, ITrackerWrapper tracker,
            FileStream stream, object streamLock, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                List<string> peers = await tracker.GetPeerList(new PeerRequest() { ChunkHash = hash, MaxPeerCount = 256 });

                // for now, pick a random peer
                var index = new Random((int)(DateTime.Now.Ticks % int.MaxValue)).Next() % peers.Count;
                var peerClient = state.GetNodeClient(new Uri(peers[index]));
                var peerCall = peerClient.GetChunk(new Node.ChunkRequest() { Hash = hash });

                List<Node.ChunkResponse> response = [];
                await foreach (var message in peerCall.ResponseStream.ReadAllAsync())
                {
                    response.Add(message);
                }

                lock (streamLock)
                {
                    stream.Seek(chunkOffset, SeekOrigin.Begin);
                    foreach (var item in response)
                    {
                        stream.Write(item.Response.Span);
                        (long current, long total) = state.FileProgress[fileHash];
                        current += item.Response.Span.Length;
                        state.FileProgress[fileHash] = (current, total);
                    }
                }

                await tracker.MarkReachable(hash, nodeURI);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
