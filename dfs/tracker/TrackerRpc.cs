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
using common;
using RpcCommon;


namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase
    {
        private readonly FilesystemManager _filesystemManager;
        private readonly ConcurrentDictionary<string, List<string>> _peers = new();
        private readonly ConcurrentDictionary<string, RpcCommon.Hash> _containerRoots = new();

        public TrackerRpc(FilesystemManager filesystemManager)
        {
            _filesystemManager = filesystemManager;
        }

        public override Task<Hash> GetContainerRootHash(RpcCommon.Guid request, ServerCallContext context)
        {
            if (_containerRoots.TryGetValue(request.Guid_, out var rootHash))
            {
                return Task.FromResult(rootHash);
            }
            throw new RpcException(new Status(StatusCode.NotFound, "Container root hash not found."));
        }

        public override async Task GetObjectTree(Hash request, IServerStreamWriter<ObjectWithHash> responseStream, ServerCallContext context)
        {
            try
            {
                if (_filesystemManager.ObjectByHash.ContainsKey(request.Data))
                {
                    foreach (var o in _filesystemManager.GetObjectTree(request.Data))
                    {
                        await responseStream.WriteAsync(o);
                    }
                }
                else
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "Object not found."));
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
                string chunkHashBase64 = request.ChunkHash.ToBase64();
                if (_peers.TryGetValue(chunkHashBase64, out var peerList))
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

        public override async Task<Empty> MarkReachable(IAsyncStreamReader<MarkRequest> requestStream, ServerCallContext context)
        {
            try
            {
                await foreach (var req in requestStream.ReadAllAsync())
                {
                    string hashBase64 = req.Hash.ToBase64();
                    if (_peers.TryGetValue(hashBase64, out List<string>? value))
                    {
                        value.Add(req.Peer);
                    }
                    else
                    {
                        _peers[hashBase64] = [req.Peer];
                    }
                }
                return new Empty();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkReachable method: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Unknown, "Unexpected error occurred."));
            }
        }

        public override async Task<Empty> MarkUnreachable(IAsyncStreamReader<MarkRequest> requestStream, ServerCallContext context)
        {
            try
            {
                await foreach (var req in requestStream.ReadAllAsync())
                {
                    string hashBase64 = req.Hash.ToBase64();
                    _peers[hashBase64].Remove(req.Peer);
                }
                return new Empty();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkUnreachable method: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Unknown, "Unexpected error occurred."));
            }
        }

        public override async Task<Empty> Publish(IAsyncStreamReader<ObjectWithHash> requestStream, ServerCallContext context)
        {
            try
            {
                await foreach (var obj in requestStream.ReadAllAsync())
                {
                    string hashBase64 = obj.Hash.ToBase64();
                    _filesystemManager.ObjectByHash[ByteString.FromBase64(hashBase64)] = obj;
                }
                return new Empty();
            }
            catch (InvalidProtocolBufferException ex)
            {
                Console.WriteLine($"Error in Publish method: {ex.Message}");
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UTF-8 data received."));
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"Error in Publish method: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in Publish method: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Unknown, "Unexpected error occurred."));
            }
        }

        public override Task<Empty> SetContainerRootHash(ContainerRootHash request, ServerCallContext context)
        {
            _containerRoots[request.Guid] = request.Hash;
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> DeleteObjectHash(Hash request, ServerCallContext context)
        {
            string hashBase64 = request.Data.ToBase64();
            _filesystemManager.ObjectByHash.TryRemove(ByteString.FromBase64(hashBase64), out _);
            return Task.FromResult(new Empty());
        }
    }
}
