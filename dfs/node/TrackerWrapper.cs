using common;
using Google.Protobuf;
using Tracker;
using static Tracker.Tracker;

namespace node
{
    class TrackerWrapper : ITrackerWrapper
    {
        public TrackerClient client { get; }

        public TrackerWrapper(string trackerUri, NodeState state)
        {
            if (!Uri.TryCreate(trackerUri, new UriCreationOptions(), out Uri? uri) || uri == null)
            {
                throw new Exception("invalid uri");
            }

            client = state.GetTrackerClient(uri);
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString hash)
        {
            throw new NotImplementedException();
        }

        public async Task<List<string>> GetPeerList(PeerRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<Empty> MarkReachable(ByteString hash)
        {
            throw new NotImplementedException();
        }

        public async Task<Empty> MarkUnreachable(ByteString hash)
        {
            throw new NotImplementedException();
        }

        public async Task<Empty> Publish(List<ObjectWithHash> objects)
        {
            using var call = client.Publish();
            foreach (var obj in objects)
            {
                await call.RequestStream.WriteAsync(obj);
            }
            await call.RequestStream.CompleteAsync();

            return await call;
        }
    }
}
