using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase
    {
        public override Task GetObjectTree(Hash request, IServerStreamWriter<ObjectWithHash> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task GetPeerList(PeerRequest request, IServerStreamWriter<PeerResponse> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task<Empty> MarkReachable(IAsyncStreamReader<Hash> requestStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task<Empty> MarkUnreachable(IAsyncStreamReader<Hash> requestStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task<Empty> Publish(IAsyncStreamReader<ObjectWithHash> requestStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }
    }
}
