using common;
using Fs;
using Google.Protobuf;
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
        public Dictionary<ByteString, string> PathByHash { get; }
        public FilesystemManager Manager { get; }
        private ChannelCache NodeChannel { get; }
        private ChannelCache TrackerChannel { get; }
        public Dictionary<ByteString, (long, long)> FileProgress { get; }
        public NodeState(TimeSpan channelTtl)
        {
            PathByHash = new(new HashUtils.ByteStringComparer());
            NodeChannel = new ChannelCache(channelTtl);
            TrackerChannel = new ChannelCache(channelTtl);
            Manager = new FilesystemManager();
            FileProgress = new(new HashUtils.ByteStringComparer());
        }

        public NodeClient GetNodeClient(Uri uri, GrpcChannelOptions? options = null)
        {
            var channel = NodeChannel.GetOrCreate(uri, options);
            return new NodeClient(channel);
        }

        public TrackerClient GetTrackerClient(Uri uri, GrpcChannelOptions? options = null)
        {
            var channel = TrackerChannel.GetOrCreate(uri, options);
            return new TrackerClient(channel);
        }
    }
}
