using Google.Protobuf;
using Tracker;

namespace common
{
    public interface ITrackerWrapper
    {
        Task<List<ObjectWithHash>> GetObjectTree(ByteString hash);
        Task<Empty> Publish(List<ObjectWithHash> objects);
        Task<List<string>> GetPeerList(PeerRequest request);

        Task<Empty> MarkReachable(ByteString hash);
        Task<Empty> MarkUnreachable(ByteString hash);

    }
}
