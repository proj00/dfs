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
            this.objects = new(new ByteStringComparer());
            this.peers = new(new ByteStringComparer());
            this.peerId = "";
            Container = [];
        }

        public Task<List<ObjectWithHash>> GetObjectTree(ByteString hash, CancellationToken token)
        {
            Dictionary<ByteString, ObjectWithHash> obj = new(new ByteStringComparer());
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
            return Task.FromResult(obj.Values.ToList());
        }

        public Task<List<string>> GetPeerList(PeerRequest request, CancellationToken token)
        {
            if (peers.TryGetValue(request.ChunkHash, out string[]? p))
            {
                return Task.FromResult(p.ToList().GetRange(0, int.Min(request.MaxPeerCount, p.Length)));
            }

            return Task.FromResult(new List<string>());
        }

        public Task<Empty> MarkReachable(ByteString[] hash, Uri nodeURI, CancellationToken token)
        {
            foreach (var h in hash)
                peers[h] = [nodeURI.ToString()];
            return Task.FromResult(new Empty());
        }

        public Task<Empty> MarkUnreachable(ByteString[] hash, Uri nodeURI, CancellationToken token)
        {
            foreach (var h in hash)
                peers[h] = [];
            return Task.FromResult(new Empty());
        }

        public Task<Empty> Publish(IReadOnlyList<PublishedObject> objects, CancellationToken token)
        {
            foreach (var obj in objects)
            {
                this.objects[obj.Object.Hash] = obj.Object.Object;
            }

            return Task.FromResult(new Empty());
        }

        public Task<ByteString> GetContainerRootHash(Guid containerGuid, CancellationToken token)
        {
            return Task.FromResult(Container[containerGuid]);
        }

        public Task<List<SearchResponse>> SearchForObjects(string query, CancellationToken token)
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

        public Uri GetUri()
        {
            throw new NotImplementedException();
        }

        public Task<TransactionStartResponse> StartTransaction(TransactionRequest transactionRequest, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
