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
        public Dictionary<ByteString, string> pathByHash { get; }
        public Dictionary<ByteString, Fs.FileSystemObject> objectByHash { get; }
        public Dictionary<ByteString, ByteString[]> chunkParents { get; }
        private ChannelCache nodeChannel { get; }
        private ChannelCache trackerChannel { get; }

        public NodeState(TimeSpan channelTtl)
        {
            objectByHash = new(new HashUtils.ByteStringComparer());
            pathByHash = new(new HashUtils.ByteStringComparer());
            chunkParents = new(new HashUtils.ByteStringComparer());
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

        public void SetChunkParent(ByteString chunkHash, ByteString parentHash)
        {
            if (chunkParents.TryGetValue(chunkHash, out ByteString[]? parents))
            {
                chunkParents[chunkHash] = [.. parents, parentHash];
                return;
            }

            chunkParents[chunkHash] = [parentHash];
        }
    }
}
