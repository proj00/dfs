﻿using common;
using Fs;
using Google.Protobuf;
using Tracker;

namespace common_test
{
    public class MockTrackerWrapper : ITrackerWrapper
    {
        public Dictionary<ByteString, Fs.FileSystemObject> objects { get; set; }
        public Dictionary<ByteString, string[]> peers { get; set; }
        public string peerId { get; set; }

        public MockTrackerWrapper()
        {
            this.objects = new(new HashUtils.ByteStringComparer());
            this.peers = new(new HashUtils.ByteStringComparer());
            this.peerId = "";
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString hash)
        {
            Dictionary<ByteString, ObjectWithHash> obj = new(new HashUtils.ByteStringComparer());
            void Traverse(ObjectWithHash o)
            {
                obj[o.Hash] = o;
                if (o.Obj.TypeCase != FileSystemObject.TypeOneofCase.Directory)
                {
                    return;
                }

                foreach (var next in o.Obj.Directory.Entries)
                {
                    Traverse(new ObjectWithHash() { Hash = next, Obj = objects[next] });
                }
            }

            Traverse(new ObjectWithHash { Hash = hash, Obj = objects[hash] });
            return obj.Values.ToList();
        }

        public async Task<List<string>> GetPeerList(PeerRequest request)
        {
            if (peers.ContainsKey(request.ChunkHash))
            {
                var p = peers[request.ChunkHash];
                return p.ToList().GetRange(0, int.Min(request.MaxPeerCount, p.Length));
            }

            return [];
        }

        public async Task<Empty> MarkReachable(ByteString hash)
        {
            if (!peers.ContainsKey(hash))
            {
                peers[hash] = [];
            }
            peers[hash] = [peerId];
            return new();
        }

        public async Task<Empty> MarkUnreachable(ByteString hash)
        {
            peers[hash] = [];
            return new();
        }

        public async Task<Empty> Publish(List<ObjectWithHash> objects)
        {
            foreach (var obj in objects)
            {
                this.objects[obj.Hash] = obj.Obj;
            }

            return new();
        }
    }
}
