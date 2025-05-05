using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Node;
using Org.BouncyCastle.Tls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Node.Node;

namespace node
{
    public class NodeRpc : NodeBase
    {
        private readonly NodeState state;
        public NodeRpc(NodeState state)
        {
            this.state = state;
        }

        public override async Task GetChunk(ChunkRequest request, IServerStreamWriter<ChunkResponse> responseStream, ServerCallContext context)
        {
            if (await state.BlockList.IsInBlockListAsync(new Uri(context.Peer)))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "request blocked"));
            }

            RpcCommon.HashList? parent = await state.Manager.ChunkParents.TryGetValue(request.Hash);
            if (parent == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "chunk id invalid or not found"));
            }

            var parentHash = parent.Data[0];
            var parentObj = (await state.Manager.ObjectByHash.GetAsync(parentHash)).Object;
            if (parentObj == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "internal failure"));
            }

            var size = parentObj.File.Hashes.ChunkSize;
            var chunkIndex = parentObj.File.Hashes.Hash.IndexOf(request.Hash);
            var offset = chunkIndex * size + request.Offset;
            var remainingSize = size - request.Offset;

            var buffer = new byte[remainingSize];
            long total = await state.AsyncIO.ReadBufferAsync(await state.PathHandler.GetPathAsync(parentHash), buffer, offset, context.CancellationToken);

            var subchunk = GetSubchunkSize(size, 1, Constants.maxChunkSize, Constants.maxChunkSize / 16, Constants.maxChunkSize);
            var used = 0;

            for (int i = 0; i < total; i += subchunk)
            {
                var res = ByteString.CopyFrom(buffer, i, (int)Math.Min(subchunk, total - i));
                used += res.Length;

                await responseStream.WriteAsync(new ChunkResponse()
                {
                    Response = res
                }, context.CancellationToken);

                state.Logger.LogInformation($"Sent {res.Length} bytes to {context.Peer}");
                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            var tracker = state.ClientHandler.GetTrackerWrapper(new Uri(request.TrackerUri));
            await tracker.ReportDataUsage(true, used, CancellationToken.None);
        }

        private static int GetSubchunkSize(
        long fileSize,
        long minChunkSize,
        long maxChunkSize,
        int minSize,
        int maxSize)
        {
            fileSize = Math.Clamp(fileSize, minChunkSize, maxChunkSize);
            // Clamp t between 0 and 1
            double t = (double)(fileSize - minChunkSize) / (maxChunkSize - minChunkSize);
            t = Math.Clamp(t, 0.0, 1.0);

            // Smoothstep: t * t * (3 - 2 * t)
            t = t * t * (3.0 - 2.0 * t);

            // Interpolate chunk size
            int size = Math.Clamp((int)(minSize + t * (maxSize - minSize)), minSize, maxSize);
            return size;
        }
    }
}
