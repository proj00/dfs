using Grpc.Core;
using Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Node.Node;

namespace node
{
    public class NodeRpc : NodeBase
    {
        public NodeState state { get; }
        public NodeRpc(NodeState state)
        {
            this.state = state;
        }

        public override async Task GetChunk(ChunkRequest request, IServerStreamWriter<ChunkResponse> responseStream, ServerCallContext context)
        {
            if (!state.chunkParents.ContainsKey(request.Hash))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "chunk id invalid or not found"));
            }

            var parentHash = state.chunkParents[request.Hash].First();
            var parentObj = state.objectByHash[parentHash];
            if (parentObj == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "internal failure"));
            }

            var size = parentObj.File.Hashes.ChunkSize;
            var offset = parentObj.File.Hashes.Hash.IndexOf(request.Hash) * size;

            var buffer = new byte[size];

            using var stream = new FileStream(state.pathByHash[parentHash], FileMode.Open);
            stream.Read(buffer, offset, size);
        }
    }
}
