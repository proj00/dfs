using Fs;
using Google.Protobuf;
using RpcCommon;
using Tracker;

namespace common
{
    public interface ITrackerWrapper
    {
        Task<List<ObjectWithHash>> GetObjectTree(ByteString hash);
        Task<Empty> Publish(List<PublishedObject> objects);
        Task<List<string>> GetPeerList(PeerRequest request, CancellationToken token);

        Task<Empty> MarkReachable(ByteString hash, string nodeURI);
        Task<Empty> MarkUnreachable(ByteString hash, string nodeURI);

        Task<ByteString> GetContainerRootHash(System.Guid containerGuid);
        Task<Empty> SetContainerRootHash(System.Guid containerGuid, ByteString rootHash);

        Task<List<SearchResponse>> SearchForObjects(string query);
        Task<DataUsage> GetDataUsage();
        Task<Empty> ReportDataUsage(bool isUpload, Int64 bytes);

        string GetUri();
    }
}
