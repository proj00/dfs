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
        public string GetUri() => trackerUri.ToString();
        private readonly Uri trackerUri;

        public TrackerWrapper(TrackerClient client, string trackerUri)
        {
            this.client = client;
            if (!Uri.TryCreate(trackerUri, new UriCreationOptions(), out Uri? uri))
            {
                throw new Exception($"invalid uri: {trackerUri}");
            }
            ArgumentNullException.ThrowIfNull(uri);
            this.trackerUri = uri;
        }

        public TrackerWrapper(string trackerUri, NodeState state)
        {
            if (!Uri.TryCreate(trackerUri, new UriCreationOptions(), out Uri? uri))
            {
                throw new Exception($"invalid uri: {trackerUri}");
            }
            ArgumentNullException.ThrowIfNull(uri);
            client = state.GetTrackerClient(uri);
            this.trackerUri = uri;
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString hash)
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

        public async Task<Empty> MarkReachable(ByteString[] hash, string nodeURI)
        {
            return await client.MarkReachableAsync(new MarkRequest
            {
                Hash = { hash },
                Peer = nodeURI
            });
        }

        public async Task<Empty> MarkUnreachable(ByteString[] hash, string nodeURI)
        {
            return await client.MarkUnreachableAsync(new MarkRequest
            {
                Hash = { hash },
                Peer = nodeURI
            });
        }

        public async Task<Empty> Publish(IReadOnlyList<PublishedObject> objects)
        {
            using var call = client.Publish();
            foreach (var obj in objects)
            {
                await call.RequestStream.WriteAsync(obj);
            }
            await call.RequestStream.CompleteAsync();

            return await call;
        }

        public async Task<ByteString> GetContainerRootHash(System.Guid containerGuid)
        {
            var response = await client.GetContainerRootHashAsync(new RpcCommon.Guid { Guid_ = containerGuid.ToString() });
            return response.Data;
        }

        public async Task<Empty> SetContainerRootHash(System.Guid containerGuid, ByteString rootHash)
        {
            return await client.SetContainerRootHashAsync(new ContainerRootHash
            {
                Guid = containerGuid.ToString(),
                Hash = new Hash { Data = rootHash }
            });
        }

        public async Task<List<SearchResponse>> SearchForObjects(string query)
        {
            using var response = client.SearchForObjects(new SearchRequest { Query = query });

            List<SearchResponse> responses = [];
            await foreach (var r in response.ResponseStream.ReadAllAsync())
            {
                responses.Add(r);
            }

            return responses;
        }

        public async Task<DataUsage> GetDataUsage()
        {
            return await client.GetDataUsageAsync(new Empty());
        }

        public async Task<Empty> ReportDataUsage(bool isUpload, long bytes)
        {
            return await client.ReportDataUsageAsync(new UsageReport
            {
                IsUpload = isUpload,
                Bytes = bytes
            });
        }
    }
}
