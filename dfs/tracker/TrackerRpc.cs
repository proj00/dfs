using Fs;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker;
using Google.Protobuf;

namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase
    {
        private readonly ConcurrentDictionary<string, ObjectWithHash> _objects = new();
        private readonly ConcurrentDictionary<string, List<string>> _peers = new();
        private readonly HashSet<string> _reachableHashes = new();

        public override async Task GetObjectTree(Hash request, IServerStreamWriter<ObjectWithHash> responseStream, ServerCallContext context)
        {
            try
            {
                if (_objects.TryGetValue(request.Hex, out var rootObject))
                {
                    await responseStream.WriteAsync(rootObject);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetObjectTree method: {ex.Message}");
                throw;
            }
        }

        public override async Task GetPeerList(PeerRequest request, IServerStreamWriter<PeerResponse> responseStream, ServerCallContext context)
        {
            try
            {
                if (_peers.TryGetValue(request.ChunkHash, out var peerList))
                {
                    foreach (var peer in peerList)
                    {
                        await responseStream.WriteAsync(new PeerResponse { Peer = peer });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPeerList method: {ex.Message}");
                throw;
            }
        }

        public override async Task<Google.Rpc.Status> MarkReachable(IAsyncStreamReader<Hash> requestStream, ServerCallContext context)
        {
            try
            {
                await foreach (var hash in requestStream.ReadAllAsync())
                {
                    lock (_reachableHashes)
                    {
                        _reachableHashes.Add(hash.Hex);
                    }
                }
                return new Google.Rpc.Status { Code = 0, Message = "Success" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkReachable method: {ex.Message}");
                return new Google.Rpc.Status { Code = 2, Message = "Unexpected error occurred." };
            }
        }

        public override async Task<Google.Rpc.Status> MarkUnreachable(IAsyncStreamReader<Hash> requestStream, ServerCallContext context)
        {
            try
            {
                await foreach (var hash in requestStream.ReadAllAsync())
                {
                    lock (_reachableHashes)
                    {
                        _reachableHashes.Remove(hash.Hex);
                    }
                }
                return new Google.Rpc.Status { Code = 0, Message = "Success" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkUnreachable method: {ex.Message}");
                return new Google.Rpc.Status { Code = 2, Message = "Unexpected error occurred." };
            }
        }

        public override async Task<Google.Rpc.Status> Publish(IAsyncStreamReader<ObjectWithHash> requestStream, ServerCallContext context)
        {
            try
            {
                await foreach (var obj in requestStream.ReadAllAsync())
                {
                    // Validate UTF-8 encoding for the hash string
                    if (!IsValidUtf8(obj.Hash))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UTF-8 data received."));
                    }

                    _objects[obj.Hash] = obj;
                }
                return new Google.Rpc.Status { Code = 0, Message = "Success" };
            }
            catch (InvalidProtocolBufferException ex)
            {
                // Log the error and return a failure status
                Console.WriteLine($"Error in Publish method: {ex.Message}");
                return new Google.Rpc.Status { Code = 1, Message = "Invalid UTF-8 data received." };
            }
            catch (RpcException ex)
            {
                // Log the error and return a failure status
                Console.WriteLine($"Error in Publish method: {ex.Message}");
                return new Google.Rpc.Status { Code = 1, Message = ex.Status.Detail };
            }
            catch (Exception ex)
            {
                // Log unexpected errors
                Console.WriteLine($"Unexpected error in Publish method: {ex.Message}");
                return new Google.Rpc.Status { Code = 2, Message = "Unexpected error occurred." };
            }
        }

        private bool IsValidUtf8(string input)
        {
            try
            {
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input));
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }
    }
}
