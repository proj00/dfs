using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using RpcCommon;
using Tracker;
using static Tracker.Tracker;

namespace node
{
    class TrackerWrapper : ITrackerWrapper
    {
        public TrackerClient client { get; }

        public TrackerWrapper(string trackerUri, NodeState state)
        {
            if (!Uri.TryCreate(trackerUri, new UriCreationOptions(), out Uri? uri) || uri == null)
            {
                throw new Exception("invalid uri");
            }

            client = state.GetTrackerClient(uri);
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

        public async Task<List<string>> GetPeerList(PeerRequest request)
        {
            using var response = client.GetPeerList(request);

            List<string> peers = [];
            await foreach (var peer in response.ResponseStream.ReadAllAsync())
            {
                peers.Add(peer.Peer);
            }

            return peers;
        }

        public async Task<Empty> MarkReachable(ByteString hash, string nodeURI)
        {
            using var call = client.MarkReachable();
            await call.RequestStream.WriteAsync(new MarkRequest { Hash = hash, Peer = nodeURI });
            await call.RequestStream.CompleteAsync();

            return await call;
        }

        public async Task<Empty> MarkUnreachable(ByteString hash, string nodeURI)
        {
            using var call = client.MarkUnreachable();
            await call.RequestStream.WriteAsync(new MarkRequest { Hash = hash, Peer = nodeURI });
            await call.RequestStream.CompleteAsync();

            return await call;
        }

        public async Task<Empty> Publish(List<ObjectWithHash> objects)
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
    }
}
