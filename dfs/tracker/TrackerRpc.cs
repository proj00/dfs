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
using System.Text.RegularExpressions;


namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase
    {
        private readonly FilesystemManager _filesystemManager;
        private readonly ConcurrentDictionary<string, List<string>> _peers = new();
        private readonly ConcurrentDictionary<string, DataUsage> dataUsage = new();
        private readonly object usageLock = new();

        public TrackerRpc(FilesystemManager filesystemManager)
        {
            _filesystemManager = filesystemManager;
        }

        public override Task<Hash> GetContainerRootHash(RpcCommon.Guid request, ServerCallContext context)
        {
            if (_filesystemManager.Container.TryGetValue(System.Guid.Parse(request.Guid_), out var rootHash))
            {
                return Task.FromResult(new Hash { Data = rootHash });
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
            _filesystemManager.Container[System.Guid.Parse(request.Guid)] = request.Hash.Data;
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> DeleteObjectHash(Hash request, ServerCallContext context)
        {
            string hashBase64 = request.Data.ToBase64();
            _filesystemManager.ObjectByHash.Remove(ByteString.FromBase64(hashBase64), out _);
            return Task.FromResult(new Empty());
        }

        public override async Task SearchForObjects(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
        {
            using var re = new IronRe2.Regex(request.Query);

            // collect all container GUIDs
            var allContainers = new List<System.Guid>();
            _filesystemManager.Container.ForEach((guid, bs) =>
            {
                allContainers.Add(guid);
                return true;
            });

            foreach (var container in allContainers)
            {
                var matches = _filesystemManager
                                  .GetContainerTree(container)
                                  .Where(o => re.IsMatch(o.Object.Name))
                                  .ToList();
                if (matches.Count == 0)
                    continue;

                var response = new SearchResponse
                {
                    Guid = container.ToString()
                };
                response.Hash.AddRange(matches.Select(o => o.Hash));
                await responseStream.WriteAsync(response);
            }
        }

        public override async Task<DataUsage> GetDataUsage(Empty request, ServerCallContext context)
        {
            var match = Regex.Match(context.Peer, @"^(?:ipv4|ipv6):([\[\]a-fA-F0-9\.:]+):\d+$");
            string ip = match.Success ? match.Groups[1].Value : "";
            try
            {
                return dataUsage[ip];
            }
            catch
            {
                return new DataUsage { };
            }
        }

        public override async Task<Empty> ReportDataUsage(UsageReport request, ServerCallContext context)
        {
            var match = Regex.Match(context.Peer, @"^(?:ipv4|ipv6):([\[\]a-fA-F0-9\.:]+):\d+$");
            string ip = match.Success ? match.Groups[1].Value : "";
            DataUsage change = new DataUsage { Upload = request.IsUpload ? request.Bytes : 0, Download = request.IsUpload ? 0 : request.Bytes };
            if (dataUsage.TryGetValue(ip, out var usage))
            {
                usage.Upload += change.Upload;
                usage.Download += change.Download;
            }
            else
            {
                dataUsage[ip] = change;
            }

            return new Empty();
        }
    }
}
