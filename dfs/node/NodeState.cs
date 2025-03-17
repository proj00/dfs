using common;
using Fs;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Node.Node;
using static Tracker.Tracker;

namespace node
{
    public class NodeState
    {
        public Dictionary<string, string> pathByHash { get; }
        public Dictionary<string, Fs.FileSystemObject> objectByHash { get; }
        private ChannelCache nodeChannel { get; }
        private ChannelCache trackerChannel { get; }

        public NodeState(TimeSpan channelTtl)
        {
            objectByHash = [];
            pathByHash = [];
            nodeChannel = new ChannelCache(channelTtl);
            trackerChannel = new ChannelCache(channelTtl);
        }

        public NodeClient GetNodeClient(Uri uri, GrpcChannelOptions? options = null)
        {
            var channel = nodeChannel.GetOrCreate(uri, options);
            return new NodeClient(channel);
        }

        public TrackerClient GetTrackerClient(Uri uri, GrpcChannelOptions? options = null)
        {
            var channel = trackerChannel.GetOrCreate(uri, options);
            return new TrackerClient(channel);
        }
    }
}
