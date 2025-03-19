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

        public void ImportObjectFromDisk(string path, int chunkSize)
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

                return;
            }

            if (Directory.Exists(path))
            {
                var entries = FilesystemUtils.GetRecursiveDirectoryObject(path, chunkSize, (hash, path) =>
                {
                    var obj = state.objectByHash[hash];
                    state.pathByHash[hash] = path;

                    if (obj.TypeCase != Fs.FileSystemObject.TypeOneofCase.File)
                    {
                        return;
                    }

                    foreach (var chunk in obj.File.Hashes.Hash)
                    {
                        state.chunkParents[chunk].Add(hash);
                    }
                });


                foreach (var entry in entries)
                {
                    state.objectByHash[entry.Key] = entry.Value;
                }

                return;
            }

            throw new Exception("Invalid path");
        }

        public async Task PublishToTracker(string[] hashes, string trackerUri)
        {
            foreach (var hash in hashes)
            {
                if (!state.objectByHash.ContainsKey(hash))
                {
                    throw new Exception("invalid hash");
                }
            }

            if (!Uri.TryCreate(trackerUri, new UriCreationOptions(), out Uri? uri) || uri == null)
            {
                throw new Exception("invalid uri");
            }

            var client = state.GetTrackerClient(uri);

            using var call = client.Publish();
            foreach (var hash in hashes)
            {
                var objWithHash = new ObjectWithHash();
                objWithHash.Hash = hash;
                objWithHash.Obj = state.objectByHash[hash];

                await call.RequestStream.WriteAsync(objWithHash);
            }
            await call.RequestStream.CompleteAsync();

            var response = await call;

            if (response.Code != 0)
            {
                throw new Exception($"gRPC call Tracker.Publish() failed: code {response.Code} {response.Message}");
            }
        }

        public async Task DownloadObjectByHash(string hash, string trackerUri, string destinationDir)
        {
            if (!Uri.TryCreate(trackerUri, new UriCreationOptions(), out Uri? uri) || uri == null)
            {
                throw new Exception("invalid uri");
            }

            var client = state.GetTrackerClient(uri);

            var call = client.GetObjectTree(new Hash() { Hex = hash });

            List<ObjectWithHash> objects = [];
            await foreach (var message in call.ResponseStream.ReadAllAsync())
            {
                objects.Add(message);
                state.objectByHash[message.Hash] = message.Obj;
            }

            var fileTasks = objects
                .Where(obj => obj.Obj.TypeCase == Fs.FileSystemObject.TypeOneofCase.File)
                .Select(obj => DownloadFile(obj, uri, destinationDir + "\\" + obj.Hash));

            await Task.WhenAll(fileTasks);
        }

        private async Task DownloadFile(ObjectWithHash obj, Uri uri, string destinationDir)
        {
            List<Task> chunkTasks = [];

            using var stream = new FileStream(destinationDir + "\\" + obj.Obj.Name, FileMode.Create);
            var i = 0;
            foreach (var hash in obj.Obj.File.Hashes.Hash)
            {
                chunkTasks.Add(DownloadChunk(hash, obj.Obj.File.Hashes.ChunkSize, i, uri, stream));
                i++;
            }

            await Task.WhenAll(chunkTasks);

            foreach (var hash in obj.Obj.File.Hashes.Hash)
            {
                state.chunkParents[hash].Add(obj.Hash);
            }
            state.pathByHash[obj.Hash] = destinationDir + "\\" + obj.Obj.Name;
        }

        private async Task DownloadChunk(string hash, int chunkSize, int chunkIndex, Uri uri, FileStream stream)
        {
            var trackerClient = state.GetTrackerClient(uri);

            var call = trackerClient.GetPeerList(new PeerRequest() { ChunkHash = hash, MaxPeerCount = 256 });
            List<string> peers = [];
            await foreach (var message in call.ResponseStream.ReadAllAsync())
            {
                peers.Add(message.Peer);
            }

            // for now, pick a random peer
            var index = new Random((int)(DateTime.Now.Ticks % int.MaxValue)).Next() % peers.Count;
            var peerClient = state.GetNodeClient(new Uri(peers[index]));

            var peerCall = peerClient.GetChunk(new Node.ChunkRequest() { Hash = hash });

            var buffer = new byte[chunkSize];
            var written = 0;
            await foreach (var message in peerCall.ResponseStream.ReadAllAsync())
            {
                var response = message.Response.ToByteArray();
                Array.Copy(response, 0, buffer, chunkIndex * chunkSize + written, response.Length);
                written += response.Length;
            }

            lock (stream)
            {
                stream.Write(buffer, chunkIndex * chunkSize, written);
            }

            var markCall = trackerClient.MarkReachable();
            await markCall.RequestStream.WriteAsync(new Hash() { Hex = hash });
            await markCall.RequestStream.CompleteAsync();
        }
    }
}
