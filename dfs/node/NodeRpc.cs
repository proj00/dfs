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
            if (!state.Manager.ChunkParents.TryGetValue(request.Hash, out ByteString[]? parent))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "chunk id invalid or not found"));
            }

            var parentHash = parent[0];
            var parentObj = state.Manager.ObjectByHash[parentHash].Object;
            if (parentObj == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "internal failure"));
            }

            var size = parentObj.File.Hashes.ChunkSize;
            var chunkIndex = parentObj.File.Hashes.Hash.IndexOf(request.Hash);
            var offset = chunkIndex * size;

            using var stream = new FileStream(state.PathByHash[parentHash], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var buffer = new byte[size];
            stream.Seek(offset, SeekOrigin.Begin);
            var total = stream.Read(buffer, 0, size);
            var subchunk = Math.Max(1, (int)Math.Sqrt(size));

            for (int i = 0; i < total; i += subchunk)
            {
                await responseStream.WriteAsync(new ChunkResponse()
                {
                    Response = ByteString.CopyFrom(buffer, i, Math.Min(subchunk, total - i))
                });
            }

        }
    }
}
