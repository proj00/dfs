using Fs;
using Google.Protobuf;
using RpcCommon;
using Tracker;

namespace common
{
    public interface ITrackerWrapper
    {
        Task<List<ObjectWithHash>> GetObjectTree(ByteString hash, CancellationToken token);
        Task<Empty> Publish(IReadOnlyList<PublishedObject> objects, CancellationToken token);
        Task<List<string>> GetPeerList(PeerRequest request, CancellationToken token);

        Task<Empty> MarkReachable(ByteString[] hash, string nodeURI, CancellationToken token);
        Task<Empty> MarkUnreachable(ByteString[] hash, string nodeURI, CancellationToken token);

        Task<ByteString> GetContainerRootHash(System.Guid containerGuid, CancellationToken token);
        Task<TransactionStartResponse> StartTransaction(TransactionRequest transactionRequest, CancellationToken token);

        Task<List<SearchResponse>> SearchForObjects(string query, CancellationToken token);
        Task<DataUsage> GetDataUsage(CancellationToken token);
        Task<Empty> ReportDataUsage(bool isUpload, Int64 bytes, CancellationToken token);

        string GetUri();
    }
}
