using common;
using Fs;
using Google.Protobuf;
using RpcCommon;
using Tracker;
using Guid = System.Guid;

namespace common_test
{
    public class MockTrackerWrapper : ITrackerWrapper
    {
        public Dictionary<ByteString, Fs.FileSystemObject> objects { get; set; }
        public Dictionary<ByteString, string[]> peers { get; set; }
        public Dictionary<Guid, ByteString> Container { get; }
        public string peerId { get; set; }

        public MockTrackerWrapper()
        {
            this.objects = new(new HashUtils.ByteStringComparer());
            this.peers = new(new HashUtils.ByteStringComparer());
            this.peerId = "";
            Container = [];
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString hash)
        {
            Dictionary<ByteString, ObjectWithHash> obj = new(new HashUtils.ByteStringComparer());
            void Traverse(ObjectWithHash o)
            {
                obj[o.Hash] = o;
                if (o.Object.TypeCase != FileSystemObject.TypeOneofCase.Directory)
                {
                    return;
                }

                foreach (var next in o.Object.Directory.Entries)
                {
                    Traverse(new ObjectWithHash() { Hash = next, Object = objects[next] });
                }
            }

            Traverse(new ObjectWithHash { Hash = hash, Object = objects[hash] });
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

        public async Task<Empty> MarkReachable(ByteString hash, string nodeURI)
        {
            if (!peers.ContainsKey(hash))
            {
                peers[hash] = [];
            }
            peers[hash] = [peerId];
            return new();
        }

        public async Task<Empty> MarkUnreachable(ByteString hash, string nodeURI)
        {
            peers[hash] = [];
            return new();
        }

        public async Task<Empty> Publish(List<ObjectWithHash> objects)
        {
            foreach (var obj in objects)
            {
                this.objects[obj.Hash] = obj.Object;
            }

            return new();
        }

        public async Task<ByteString> GetContainerRootHash(Guid containerGuid)
        {
            return Container[containerGuid];
        }

        public async Task<Empty> SetContainerRootHash(Guid containerGuid, ByteString rootHash)
        {
            Container[containerGuid] = rootHash;
            return new();
        }

        public async Task<List<SearchResponse>> SearchForObjects(string query)
        {
            throw new NotImplementedException();
        }
    }
}
