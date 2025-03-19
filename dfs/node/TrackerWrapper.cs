using common;
using Google.Rpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Tracker;
using static System.Windows.Forms.AxHost;
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

        public async Task<List<ObjectWithHash>> GetObjectTree(string hash)
        {
            throw new NotImplementedException();
        }

        public async Task<List<string>> GetPeerList(PeerRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<Status> MarkReachable(string hash)
        {
            throw new NotImplementedException();
        }

        public async Task<Status> MarkUnreachable(string hash)
        {
            throw new NotImplementedException();
        }

        public async Task<Status> Publish(List<ObjectWithHash> objects)
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
