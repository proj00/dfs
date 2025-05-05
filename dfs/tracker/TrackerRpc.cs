using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RpcCommon;
using Tracker;

namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase, IDisposable
    {
        private readonly FilesystemManager _filesystemManager;
        private readonly ConcurrentDictionary<string, List<string>> _peers = new();
        private readonly IPersistentCache<string, DataUsage> dataUsage;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<System.Guid, (System.Guid, long)> transactions =
            new();
        private bool disposedValue;
        const int trackerResponseLimit = 30000;
        private readonly CancellationTokenSource source;

        public TrackerRpc(ILogger logger, string dbPath, CancellationTokenSource source)
        {
            _filesystemManager = new FilesystemManager(dbPath);
            dataUsage = new PersistentCache<string, DataUsage>(
                System.IO.Path.Combine(_filesystemManager.DbPath, "DataUsage"),
                new StringSerializer(),
                new Serializer<DataUsage>()
            );
            this.logger = logger;
            this.source = source;
        }

        public override async Task<Hash> GetContainerRootHash(
            RpcCommon.Guid request,
            ServerCallContext context
        )
        {
            var rootHash = await _filesystemManager.Container.TryGetValue(
                System.Guid.Parse(request.Guid_)
            );
            if (rootHash != null)
            {
                return new Hash { Data = rootHash };
            }
            throw new RpcException(
                new Status(StatusCode.NotFound, "Container root hash not found.")
            );
        }

        public override async Task GetObjectTree(
            Hash request,
            IServerStreamWriter<ObjectWithHash> responseStream,
            ServerCallContext context
        )
        {
            try
            {
                if (await _filesystemManager.ObjectByHash.ContainsKey(request.Data))
                {
                    foreach (var o in await _filesystemManager.GetObjectTree(request.Data))
                    {
                        if (context.CancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
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

        public override async Task GetPeerList(
            PeerRequest request,
            IServerStreamWriter<PeerResponse> responseStream,
            ServerCallContext context
        )
        {
            try
            {
                string chunkHashBase64 = request.ChunkHash.ToBase64();
                if (_peers.TryGetValue(chunkHashBase64, out var peerList))
                {
                    foreach (var peer in peerList)
                    {
                        if (context.CancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
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

        public override async Task<Empty> MarkReachable(
            MarkRequest request,
            ServerCallContext context
        )
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
                    throw new RpcException(
                        new Status(StatusCode.Unknown, "Unexpected error occurred.")
                    );
                }
            });
        }

        public override async Task<Empty> MarkUnreachable(
            MarkRequest request,
            ServerCallContext context
        )
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
                    throw new RpcException(
                        new Status(StatusCode.Unknown, "Unexpected error occurred.")
                    );
                }
            });
        }

        public override async Task SearchForObjects(
            SearchRequest request,
            IServerStreamWriter<SearchResponse> responseStream,
            ServerCallContext context
        )
        {
            using var re = new IronRe2.Regex(request.Query);

            // collect all container GUIDs
            var allContainers = new List<System.Guid>();
            await _filesystemManager.Container.ForEach(
                (guid, bs) =>
                {
                    allContainers.Add(guid);
                    return true;
                }
            );

            foreach (var container in allContainers)
            {
                var matches = (await _filesystemManager.GetContainerTree(container))
                    .Where(o => re.IsMatch(o.Object.Name))
                    .Select(o => new SearchResponse { Guid = container.ToString(), Object = o })
                    .ToList();

                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (matches.Count == 0)
                    continue;

                foreach (var match in matches)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    await responseStream.WriteAsync(match);
                }
            }
        }

        public override async Task<DataUsage> GetDataUsage(Empty request, ServerCallContext context)
        {
            var match = Regex.Match(context.Peer, @"^(?:ipv4|ipv6):([\[\]a-fA-F0-9\.:]+):\d+$", RegexOptions.None, TimeSpan.FromMilliseconds(100));
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
            await dataUsage.ForEach(
                (key, value) =>
                {
                    list.Add((key, value));
                    return true;
                }
            );
            return list.ToArray();
        }

        public override async Task<Empty> ReportDataUsage(
            UsageReport request,
            ServerCallContext context
        )
        {
            var match = Regex.Match(context.Peer, @"^(?:ipv4|ipv6):([\[\]a-fA-F0-9\.:]+):\d+$", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            string ip = match.Success ? match.Groups[1].Value : "";
            DataUsage change = new()
            {
                Upload = request.IsUpload ? request.Bytes : 0,
                Download = request.IsUpload ? 0 : request.Bytes,
            };

            await dataUsage.MutateAsync(
                ip,
                (usage) =>
                {
                    if (usage == null)
                    {
                        return change;
                    }

                    usage.Upload += change.Upload;
                    usage.Download += change.Download;
                    return usage;
                }
            );

            return new Empty();
        }

        public override async Task<Empty> Publish(
            IAsyncStreamReader<PublishedObject> requestStream,
            ServerCallContext context
        )
        {
            var Now = DateTime.Now.Ticks;
            List<PublishedObject> objects = [];
            bool found = false;

            ByteString rootHash = ByteString.Empty;
            ValueTuple<System.Guid, long> transactionInfo = default;

            await foreach (var obj in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (!found)
                {
                    if (!transactions.TryGetValue(System.Guid.Parse(obj.TransactionGuid), out transactionInfo))
                    {
                        throw new RpcException(new Status(StatusCode.Cancelled, "invalid transaction id"));
                    }
                    if (TimeSpan.FromTicks(Now - transactionInfo.Item2).TotalMilliseconds > trackerResponseLimit)
                    {
                        transactions.TryRemove(new KeyValuePair<System.Guid, (System.Guid, long)>
                            (
                            System.Guid.Parse(obj.TransactionGuid),
                            transactionInfo)
                            );

                        throw new RpcException(new Status(StatusCode.DeadlineExceeded, "TTL has expired"));
                    }
                }
                found = true;
                objects.Add(obj);
                if (obj.IsRoot)
                    rootHash = obj.Object.Hash;
            }

            await _filesystemManager.CreateObjectContainer(
                objects.Select(o => o.Object).ToArray(),
                rootHash,
                transactionInfo.Item1
            );
            return new Empty();
        }

        public override async Task<TransactionStartResponse> StartTransaction(
            TransactionRequest request,
            ServerCallContext context
        )
        {
            return await Task.Run(() =>
            {
                var containerGuid = System.Guid.Parse(request.ContainerGuid);

                try
                {
                    var info = transactions.First(t => t.Value.Item1 == containerGuid);
                    if (info.Value.Item2 != 0
                    && TimeSpan.FromTicks(DateTime.Now.Ticks - info.Value.Item2).TotalMilliseconds < trackerResponseLimit)
                    {
                        return new TransactionStartResponse()
                        {
                            ActualContainerGuid = "",
                            State = TransactionState.Locked,
                            TransactionGuid = "",
                            TtlMs = 0,
                        };
                    }

                    transactions.TryRemove(info);
                }
                // this is fine transactions.First will throw if there is no match, and i don't care at all
                catch { }

                var currentGuid = System.Guid.NewGuid();
                var time = DateTime.Now.Ticks;
                transactions[currentGuid] = (containerGuid, time);
                return new TransactionStartResponse()
                {
                    ActualContainerGuid = request.ContainerGuid,
                    State = TransactionState.Ok,
                    TransactionGuid = currentGuid.ToString(),
                    TtlMs = trackerResponseLimit,
                };
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _filesystemManager.Dispose();
                    dataUsage.Dispose();
                }
                disposedValue = true;
            }
        }

        ~TrackerRpc()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override async Task<Empty> Shutdown(Empty request, ServerCallContext context)
        {
            await source.CancelAsync();
            return new Empty();
        }
    }
}
