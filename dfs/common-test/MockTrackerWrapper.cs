using common;
using Fs;
using Google.Rpc;
using Tracker;

namespace common_test
{
    public class MockTrackerWrapper : ITrackerWrapper
    {
        public Dictionary<string, Fs.FileSystemObject> objects { get; set; }
        public Dictionary<string, HashSet<string>> peers { get; set; }
        public string peerId { get; set; }

        public MockTrackerWrapper()
        {
            this.objects = [];
            this.peers = [];
            this.peerId = "";
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(string hash)
        {
            Dictionary<string, ObjectWithHash> obj = [];
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
                return p.ToList().GetRange(0, int.Min(request.MaxPeerCount, p.Count));
            }

            return [];
        }

        public async Task<Status> MarkReachable(string hash)
        {
            if (!peers.ContainsKey(hash))
            {
                peers[hash] = [];
            }
            peers[hash].Add(peerId);
            return new Status();
        }

        public async Task<Status> MarkUnreachable(string hash)
        {
            peers[hash].Remove(peerId);
            return new Status();
        }

        public async Task<Status> Publish(List<ObjectWithHash> objects)
        {
            foreach (var obj in objects)
            {
                this.objects[obj.Hash] = obj.Obj;
            }

            return new Status();
        }
    }
}
