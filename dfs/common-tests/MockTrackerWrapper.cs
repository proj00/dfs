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

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString hash, CancellationToken token)
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

        public async Task<List<string>> GetPeerList(PeerRequest request, CancellationToken token)
        {
            if (peers.ContainsKey(request.ChunkHash))
            {
                var p = peers[request.ChunkHash];
                return p.ToList().GetRange(0, int.Min(request.MaxPeerCount, p.Length));
            }

            return [];
        }

        public async Task<Empty> MarkReachable(ByteString[] hash, string nodeURI, CancellationToken token)
        {
            foreach (var h in hash)
                peers[h] = [nodeURI];
            return new();
        }

        public async Task<Empty> MarkUnreachable(ByteString[] hash, string nodeURI, CancellationToken token)
        {
            foreach (var h in hash)
                peers[h] = [];
            return new();
        }

        public async Task<Empty> Publish(IReadOnlyList<PublishedObject> objects, CancellationToken token)
        {
            foreach (var obj in objects)
            {
                this.objects[obj.Object.Hash] = obj.Object.Object;
            }

            return new();
        }

        public async Task<ByteString> GetContainerRootHash(Guid containerGuid, CancellationToken token)
        {
            return Container[containerGuid];
        }

        public async Task<List<SearchResponse>> SearchForObjects(string query, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<DataUsage> GetDataUsage(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<Empty> ReportDataUsage(bool isUpload, long bytes, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public string GetUri()
        {
            throw new NotImplementedException();
        }

        public Task<TransactionStartResponse> StartTransaction(TransactionRequest transactionRequest, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
