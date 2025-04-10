using Fs;
using Google.Protobuf;
using RpcCommon;
using Tracker;

namespace common
{
    public interface ITrackerWrapper
    {
        Task<List<ObjectWithHash>> GetObjectTree(ByteString hash);
        Task<Empty> Publish(List<ObjectWithHash> objects);
        Task<List<string>> GetPeerList(PeerRequest request);

        Task<Empty> MarkReachable(ByteString hash, string nodeURI);
        Task<Empty> MarkUnreachable(ByteString hash, string nodeURI);

        Task<ByteString> GetContainerRootHash(RpcCommon.Guid containerGuid);
        Task<Empty> SetContainerRootHash(RpcCommon.Guid containerGuid, ByteString rootHash);

    }
}
