using Google.Rpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public interface ITrackerWrapper
    {
        Task<List<ObjectWithHash>> GetObjectTree(string hash);
        Task<Status> Publish(List<ObjectWithHash> objects);
        Task<List<string>> GetPeerList(PeerRequest request);

        Task<Status> MarkReachable(string hash);
        Task<Status> MarkUnreachable(string hash);

    }
}
