using Fs;
using Grpc.Core;
using System.Collections.Concurrent;
using Tracker;
using Google.Protobuf;
using common;
using RpcCommon;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Text;


namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase, IDisposable
    {
        private readonly FilesystemManager _filesystemManager;
        private readonly ConcurrentDictionary<string, List<string>> _peers = new();
        private readonly PersistentCache<string, DataUsage> dataUsage;
        private readonly ILogger logger;

        public TrackerRpc(ILogger logger, string dbPath)
        {
            _filesystemManager = new FilesystemManager(dbPath);
            dataUsage = new(
                Path.Combine(_filesystemManager.DbPath, "DataUsage"),
                keySerializer: Encoding.UTF8.GetBytes,
                keyDeserializer: Encoding.UTF8.GetString,
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: DataUsage.Parser.ParseFrom
            );
            this.logger = logger;
        }

        public void Dispose()
        {
            _filesystemManager.Dispose();
            dataUsage.Dispose();
        }

        public override async Task<Hash> GetContainerRootHash(RpcCommon.Guid request, ServerCallContext context)
        {
            var rootHash = await _filesystemManager.Container.TryGetValue(System.Guid.Parse(request.Guid_));
            if (rootHash != null)
            {
                return new Hash { Data = rootHash };
            }
            throw new RpcException(new Status(StatusCode.NotFound, "Container root hash not found."));
        }

        public override async Task GetObjectTree(Hash request, IServerStreamWriter<ObjectWithHash> responseStream, ServerCallContext context)
        {
            try
            {
                if (await _filesystemManager.ObjectByHash.ContainsKey(request.Data))
                {
                    foreach (var o in await _filesystemManager.GetObjectTree(request.Data))
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
                logger.LogError($"Error in GetObjectTree method: {ex.Message}");
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
                logger.LogError($"Error in GetPeerList method: {ex.Message}");
                throw;
            }
        }

        public override async Task<Empty> MarkReachable(MarkRequest request, ServerCallContext context)
        {
            return await Task.Run(() =>
            {
                try
                {
                    foreach (var req in request.Hash)
                    {
                        string hashBase64 = req.ToBase64();
                        if (_peers.TryGetValue(hashBase64, out List<string>? value))
                        {
                            value.Add(request.Peer);
                        }
                        else
                        {
                            _peers[hashBase64] = [request.Peer];
                        }
                    }
                    return new Empty();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error in MarkReachable method: {ex.Message}");
                    throw new RpcException(new Status(StatusCode.Unknown, "Unexpected error occurred."));
                }
            });
        }

        public override async Task<Empty> MarkUnreachable(MarkRequest request, ServerCallContext context)
        {
            return await Task.Run(() =>
            {
                try
                {
                    foreach (var req in request.Hash)
                    {
                        string hashBase64 = req.ToBase64();
                        _peers[hashBase64].Remove(request.Peer);
                    }
                    return new Empty();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error in MarkUnreachable method: {ex.Message}");
                    throw new RpcException(new Status(StatusCode.Unknown, "Unexpected error occurred."));
                }
            });
        }

        public override async Task<Empty> SetContainerRootHash(ContainerRootHash request, ServerCallContext context)
        {
            await _filesystemManager.Container.SetAsync(System.Guid.Parse(request.Guid), request.Hash.Data);
            return new Empty();
        }

        public override async Task SearchForObjects(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
        {
            using var re = new IronRe2.Regex(request.Query);

            // collect all container GUIDs
            var allContainers = new List<System.Guid>();
            await _filesystemManager.Container.ForEach((guid, bs) =>
            {
                allContainers.Add(guid);
                return true;
            });

            foreach (var container in allContainers)
            {
                var matches = (await _filesystemManager
                                  .GetContainerTree(container))
                                  .Where(o => re.IsMatch(o.Object.Name))
                                  .Select(o => new SearchResponse
                                  {
                                      Guid = container.ToString(),
                                      Object = o,
                                  }).ToList();
                if (matches.Count == 0)
                    continue;

                foreach (var match in matches)
                {
                    await responseStream.WriteAsync(match);
                }
            }
        }

        public override async Task<DataUsage> GetDataUsage(Empty request, ServerCallContext context)
        {
            var match = Regex.Match(context.Peer, @"^(?:ipv4|ipv6):([\[\]a-fA-F0-9\.:]+):\d+$");
            string ip = match.Success ? match.Groups[1].Value : "";
            try
            {
                return await dataUsage.GetAsync(ip);
            }
            catch
            {
                return new DataUsage { };
            }
        }

        public async Task<(string key, DataUsage usage)[]> GetTotalDataUsage()
        {
            List<(string key, DataUsage usage)> list = [];
            await dataUsage.ForEach((key, value) =>
            {
                list.Add((key, value));
                return true;
            });
            return list.ToArray();
        }

        public override async Task<Empty> ReportDataUsage(UsageReport request, ServerCallContext context)
        {
            var match = Regex.Match(context.Peer, @"^(?:ipv4|ipv6):([\[\]a-fA-F0-9\.:]+):\d+$");
            string ip = match.Success ? match.Groups[1].Value : "";
            DataUsage change = new() { Upload = request.IsUpload ? request.Bytes : 0, Download = request.IsUpload ? 0 : request.Bytes };

            await dataUsage.MutateAsync(ip, (usage) =>
            {
                if (usage == null)
                {
                    return change;
                }

                usage.Upload += change.Upload;
                usage.Download += change.Download;
                return usage;
            });

            return new Empty();
        }

        public override Task<TransactionStartResponse> StartTransaction(Empty request, ServerCallContext context)
        {
            return base.StartTransaction(request, context);
        }

        public override Task<TransactionStateResponse> CheckTransactionState(RpcCommon.Guid request, ServerCallContext context)
        {
            return base.CheckTransactionState(request, context);
        }

        public override async Task<Empty> Publish(IAsyncStreamReader<PublishedObject> requestStream, ServerCallContext context)
        {
            try
            {
                List<ObjectWithHash> objects = [];
                await foreach (var obj in requestStream.ReadAllAsync())
                {
                    objects.Add(obj.Object);
                }
                foreach (var o in objects)
                    await _filesystemManager.ObjectByHash.SetAsync(o.Hash, o);
                return new Empty();
            }
            catch (InvalidProtocolBufferException ex)
            {
                logger.LogError($"Error in Publish method: {ex.Message}");
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UTF-8 data received."));
            }
            catch (RpcException ex)
            {
                logger.LogError($"Error in Publish method: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error in Publish method: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Unknown, "Unexpected error occurred."));
            }
        }
    }
}
