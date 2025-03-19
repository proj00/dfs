using CefSharp;
using CefSharp.DevTools.Network;
using common;
using Grpc.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Tracker;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

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

        public string ImportObjectFromDisk(string path, int chunkSize)
        {
            if (chunkSize <= 0 || chunkSize > Constants.maxChunkSize)
            {
                throw new Exception("Invalid chunk size");
            }

            if (File.Exists(path))
            {
                var obj = FilesystemUtils.GetFileObject(path, chunkSize);
                var hash = HashUtils.GetHash(obj);

                state.pathByHash[hash] = path;
                state.objectByHash[hash] = obj;
                foreach (var chunk in obj.File.Hashes.Hash)
                {
                    state.SetChunkParent(chunk, hash);
                }

                return hash;
            }

            if (Directory.Exists(path))
            {
                return FilesystemUtils.GetRecursiveDirectoryObject(path, chunkSize, (hash, path, obj) =>
                {
                    state.objectByHash[hash] = obj;
                    state.pathByHash[hash] = path;

                    if (obj.TypeCase != Fs.FileSystemObject.TypeOneofCase.File)
                    {
                        return;
                    }

                    foreach (var chunk in obj.File.Hashes.Hash)
                    {
                        state.SetChunkParent(chunk, hash);
                    }
                });
            }

            throw new Exception("Invalid path");
        }

        public async Task PublishToTracker(string[] hashes, string uri)
        {
            await PublishToTracker(hashes, new TrackerWrapper(uri, state));
        }

        public async Task PublishToTracker(string[] hashes, ITrackerWrapper tracker)
        {
            foreach (var hash in hashes)
            {
                if (!state.objectByHash.ContainsKey(hash))
                {
                    throw new Exception("invalid hash");
                }
            }

            var response = await tracker.Publish(hashes.Select(hash => new ObjectWithHash { Hash = hash, Obj = state.objectByHash[hash] }).ToList());
            foreach (var file in hashes
                .Select(hash => state.objectByHash[hash])
                .Where(obj => obj.TypeCase == Fs.FileSystemObject.TypeOneofCase.File))
            {
                foreach (var chunk in file.File.Hashes.Hash)
                {
                    await tracker.MarkReachable(chunk);
                }
            }

            if (response.Code != 0)
            {
                throw new Exception($"gRPC call Tracker.Publish() failed: code {response.Code} {response.Message}");
            }
        }

        public async Task DownloadObjectByHash(string hash, string uri, string destinationDir)
        {
            await DownloadObjectByHash(hash, new TrackerWrapper(uri, state), destinationDir);
        }

        public async Task DownloadObjectByHash(string hash, ITrackerWrapper tracker, string destinationDir)
        {
            List<ObjectWithHash> objects = await tracker.GetObjectTree(hash);
            foreach (var message in objects)
            {
                state.objectByHash[message.Hash] = message.Obj;
            }

            var fileTasks = objects
                .Where(obj => obj.Obj.TypeCase == Fs.FileSystemObject.TypeOneofCase.File)
                .Select(obj => DownloadFile(obj, tracker, destinationDir));

            await Task.WhenAll(fileTasks);
        }

        private async Task DownloadFile(ObjectWithHash obj, ITrackerWrapper tracker, string destinationDir)
        {
            List<Task> chunkTasks = [];

            var dir = @"\\?\" + destinationDir + "\\" + obj.Hash;
            Directory.CreateDirectory(dir);
            using var stream = new FileStream(dir + "\\" + obj.Obj.Name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            var i = 0;
            foreach (var hash in obj.Obj.File.Hashes.Hash)
            {
                chunkTasks.Add(DownloadChunk(hash, obj.Obj.File.Hashes.ChunkSize, i, tracker, stream));
                i++;
            }

            await Task.WhenAll(chunkTasks);

            foreach (var hash in obj.Obj.File.Hashes.Hash)
            {
                state.SetChunkParent(hash, obj.Hash);
            }
            state.pathByHash[obj.Hash] = destinationDir + "\\" + obj.Obj.Name;
        }

        private async Task DownloadChunk(string hash, int chunkSize, int chunkIndex, ITrackerWrapper tracker, FileStream stream)
        {
            List<string> peers = await tracker.GetPeerList(new PeerRequest() { ChunkHash = hash, MaxPeerCount = 256 });

            // for now, pick a random peer
            var index = new Random((int)(DateTime.Now.Ticks % int.MaxValue)).Next() % peers.Count;
            var peerClient = state.GetNodeClient(new Uri(peers[index]));

            var peerCall = peerClient.GetChunk(new Node.ChunkRequest() { Hash = hash });

            var buffer = new byte[chunkSize];
            var written = 0;
            await foreach (var message in peerCall.ResponseStream.ReadAllAsync())
            {
                var response = message.Response.ToByteArray();
                Array.Copy(response, 0, buffer, written, response.Length);
                written += response.Length;
            }

            lock (stream)
            {
                stream.Seek(chunkIndex * chunkSize, SeekOrigin.Begin);
                stream.Write(buffer, 0, written);
            }

            await tracker.MarkReachable(hash);
        }
    }
}
