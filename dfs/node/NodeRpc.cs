using Google.Protobuf;
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
            if (!state.chunkParents.TryGetValue(request.Hash, out HashSet<string>? parent))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "chunk id invalid or not found"));
            }

            var parentHash = parent.First();
            var parentObj = state.objectByHash[parentHash];
            if (parentObj == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "internal failure"));
            }

            var size = parentObj.File.Hashes.ChunkSize;
            var offset = parentObj.File.Hashes.Hash.IndexOf(request.Hash) * size;

            using var stream = new FileStream(state.pathByHash[parentHash], FileMode.Open);

            var buffer = new byte[size];
            var total = stream.Read(buffer, offset, size);
            var subchunk = Math.Max(1, (int)Math.Sqrt(size));

            for (int i = 0; i < total; i += subchunk)
            {
                await responseStream.WriteAsync(new ChunkResponse()
                {
                    Response = ByteString.CopyFrom(buffer, i, Math.Min(subchunk, total - subchunk * i))
                });
            }

        }
    }
}
