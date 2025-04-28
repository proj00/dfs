using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Org.BouncyCastle.Utilities;
using RpcCommon;
using Tracker;
using static Tracker.Tracker;

namespace node
{
    public class TrackerWrapper : ITrackerWrapper
    {
        public TrackerClient client { get; }
        public Uri GetUri() => trackerUri;
        private readonly Uri trackerUri;

        public TrackerWrapper(TrackerClient client, Uri trackerUri)
        {
            this.client = client;
            ArgumentNullException.ThrowIfNull(trackerUri);
            this.trackerUri = trackerUri;
        }

        public TrackerWrapper(Uri trackerUri, NodeState state, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(trackerUri);
            client = state.GetTrackerClient(trackerUri);
            this.trackerUri = trackerUri;
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString hash, CancellationToken token)
        {
            using var response = client.GetObjectTree(new Hash { Data = hash });

            List<ObjectWithHash> objects = [];
            await foreach (var obj in response.ResponseStream.ReadAllAsync())
            {
                objects.Add(obj);
            }
            return objects;
        }

        public async Task<List<string>> GetPeerList(PeerRequest request, CancellationToken token)
        {
            using var response = client.GetPeerList(request);

            List<string> peers = [];
            try
            {
                await foreach (var peer in response.ResponseStream.ReadAllAsync(token))
                {
                    peers.Add(peer.Peer);
                }
            }
            catch (OperationCanceledException)
            {
                return [];
            }

            return peers;
        }

        public async Task<Empty> MarkReachable(ByteString[] hash, Uri nodeURI, CancellationToken token)
        {
            return await client.MarkReachableAsync(new MarkRequest
            {
                Hash = { hash },
                Peer = nodeURI.ToString()
            });
        }

        public async Task<Empty> MarkUnreachable(ByteString[] hash, Uri nodeURI, CancellationToken token)
        {
            return await client.MarkUnreachableAsync(new MarkRequest
            {
                Hash = { hash },
                Peer = nodeURI.ToString()
            });
        }

        public async Task<Empty> Publish(IReadOnlyList<PublishedObject> objects, CancellationToken token)
        {
            using var call = client.Publish(null, null, token);
            foreach (var obj in objects)
            {
                await call.RequestStream.WriteAsync(obj, token);
            }
            await call.RequestStream.CompleteAsync();

            return await call;
        }

        public async Task<ByteString> GetContainerRootHash(System.Guid containerGuid, CancellationToken token)
        {
            var response = await client.GetContainerRootHashAsync(new RpcCommon.Guid { Guid_ = containerGuid.ToString() }, null, null, token);
            return response.Data;
        }

        public async Task<List<SearchResponse>> SearchForObjects(string query, CancellationToken token)
        {
            using var response = client.SearchForObjects(new SearchRequest { Query = query }, null, null, token);

            List<SearchResponse> responses = [];
            await foreach (var r in response.ResponseStream.ReadAllAsync(token))
            {
                responses.Add(r);
            }

            return responses;
        }

        public async Task<DataUsage> GetDataUsage(CancellationToken token)
        {
            return await client.GetDataUsageAsync(new Empty(), null, null, token);
        }

        public async Task<Empty> ReportDataUsage(bool isUpload, long bytes, CancellationToken token)
        {
            return await client.ReportDataUsageAsync(new UsageReport
            {
                IsUpload = isUpload,
                Bytes = bytes
            }, cancellationToken: token);
        }

        public async Task<TransactionStartResponse> StartTransaction(TransactionRequest transactionRequest, CancellationToken token)
        {
            return await client.StartTransactionAsync(transactionRequest, cancellationToken: token);
        }
    }
}
