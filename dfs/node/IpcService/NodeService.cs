using common;
using Google.Protobuf;
using Grpc.Core;
using Org.BouncyCastle.Utilities.Encoders;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Tracker;

namespace node.IpcService
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class NodeService
    {
        private UiService? service;
        private NodeState state;
        private NodeRpc rpc;

        public NodeService(NodeState state, NodeRpc rpc)
        {
            this.state = state;
            this.rpc = rpc;
        }

        public void RegisterUiService(dynamic service)
        {
            this.service = new UiService(service);
        }

        public string PickObjectPath(bool folder)
        {
            if (folder)
            {
                using var dialog = new FolderBrowserDialog();
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedPath) && Directory.Exists(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
            }
            else
            {
                using var dialog = new OpenFileDialog();
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.FileName) && File.Exists(dialog.FileName))
                {
                    return dialog.FileName;
                }
            }

            throw new Exception("Dialog failed");
        }

        public string GetObjectDiskPath(string base64Hash)
        {
            var hash = ByteString.FromBase64(base64Hash);
            return state.PathByHash[hash];
        }

        public string[] GetAllContainers()
        {
            return state.Manager.Container.Select(guid => guid.ToString()).ToArray();
        }
        public ObjectWithHash[] GetContainerObjects(string container)
        {
            var guid = Guid.Parse(container);
            return state.Manager.GetContainerTree(guid).ToArray();
        }

        public string ImportObjectFromDisk(string path, int chunkSize)
        {
            if (chunkSize <= 0 || chunkSize > Constants.maxChunkSize)
            {
                throw new Exception("Invalid chunk size");
            }

            ObjectWithHash[] objects = [];
            ByteString rootHash = ByteString.Empty;
            if (File.Exists(path))
            {
                var obj = FilesystemUtils.GetFileObject(path, chunkSize);
                rootHash = HashUtils.GetHash(obj);
                objects = [new ObjectWithHash { Hash = rootHash, Object = obj }];
            }

            if (Directory.Exists(path))
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

            return state.Manager.CreateObjectContainer(objects, rootHash).ToString();
        }

        public async Task PublishToTracker(string container, string uri)
        {
            await PublishToTracker(Guid.Parse(container), new TrackerWrapper(uri, state));
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
                    await tracker.MarkReachable(chunk);
                }
            }
        }

        public async Task DownloadContainer(string container, string uri, string destinationDir, int maxConcurrentChunks = 20)
        {
            var tracker = new TrackerWrapper(uri, state);
            var hash = await tracker.GetContainerRootHash(Guid.Parse(container));
            await DownloadObjectByHash(hash.ToBase64(), tracker, destinationDir, maxConcurrentChunks);
        }

        private async Task DownloadObjectByHash(string base64Hash, ITrackerWrapper tracker, string destinationDir, int maxConcurrentChunks)
        {
            var hash = ByteString.FromBase64(base64Hash);
            List<ObjectWithHash> objects = await tracker.GetObjectTree(hash);
            state.Manager.CreateObjectContainer(objects.ToArray(), hash);

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
            Directory.CreateDirectory(dir);
            dir = dir + "\\" + obj.Object.Name;
            using var stream = new FileStream(dir, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            object streamLock = new();

            var i = 0;
            foreach (var hash in obj.Object.File.Hashes.Hash)
            {
                chunkTasks.Add(DownloadChunk(hash, obj.Object.File.Hashes.ChunkSize * i, tracker, stream, streamLock, semaphore));
                i++;
            }

            await Task.WhenAll(chunkTasks);
            state.PathByHash[obj.Hash] = dir;
        }

        private async Task DownloadChunk(ByteString hash, int chunkOffset, ITrackerWrapper tracker,
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
                    }
                }

                await tracker.MarkReachable(hash);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
