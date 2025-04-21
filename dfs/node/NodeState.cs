using common;
using Fs;
using Google.Protobuf;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ui;
using static Node.Node;
using static Tracker.Tracker;

namespace node
{
    public class NodeState
    {
        public PersistentDictionary<ByteString, string> PathByHash { get; }
        public FilesystemManager Manager { get; }
        private ChannelCache NodeChannel { get; }
        private ChannelCache TrackerChannel { get; }
        public Dictionary<ByteString, (long, long)> FileProgress { get; }
        private PersistentDictionary<string, string> Whitelist { get; }
        private PersistentDictionary<string, string> Blacklist { get; }
        public NodeState(TimeSpan channelTtl)
        {
            NodeChannel = new ChannelCache(channelTtl);
            TrackerChannel = new ChannelCache(channelTtl);
            Manager = new FilesystemManager();
            FileProgress = new(new HashUtils.ByteStringComparer());
            PathByHash = new PersistentDictionary<ByteString, string>(
                System.IO.Path.Combine(Manager.DbPath, "PathByHash"),
                bs => bs.ToByteArray(),
                ByteString.CopyFrom,
                Encoding.UTF8.GetBytes,
                Encoding.UTF8.GetString
            );
            Whitelist = new(
                System.IO.Path.Combine(Manager.DbPath, "Whitelist"),
                keySerializer: Encoding.UTF8.GetBytes,
                keyDeserializer: Encoding.UTF8.GetString,
                valueSerializer: Encoding.UTF8.GetBytes,
                valueDeserializer: Encoding.UTF8.GetString
            );
            Blacklist = new(
                System.IO.Path.Combine(Manager.DbPath, "Whitelist"),
                keySerializer: Encoding.UTF8.GetBytes,
                keyDeserializer: Encoding.UTF8.GetString,
                valueSerializer: Encoding.UTF8.GetBytes,
                valueDeserializer: Encoding.UTF8.GetString
            );
        }

        public NodeClient GetNodeClient(Uri uri, GrpcChannelOptions? options = null)
        {
            var channel = NodeChannel.GetOrCreate(uri, options);
            return new NodeClient(channel);
        }

        public TrackerClient GetTrackerClient(Uri uri, GrpcChannelOptions? options = null)
        {
            var channel = TrackerChannel.GetOrCreate(uri, options);
            return new TrackerClient(channel);
        }

        public void FixBlockList(BlockListRequest request)
        {
            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                throw new ArgumentException("Invalid URL");
            }

            PersistentDictionary<string, string> reference = request.InWhitelist ? Whitelist : Blacklist;
            if (request.ShouldRemove && reference.ContainsKey(request.Url))
            {
                reference.Remove(request.Url);
            }
            else
            {
                reference[request.Url] = request.Url;
            }
        }

        public bool IsInBlockList(string url)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                Console.WriteLine($"warning: invalid URL {url}");
                return false;
            }

            bool passWhitelist = Whitelist.CountEstimate == 0 || Whitelist.ContainsKey(url);
            bool passBlacklist = Blacklist.CountEstimate == 0 || !Blacklist.ContainsKey(url);
            return !(passWhitelist && passBlacklist);
        }

        public BlockListResponse GetBlockList()
        {
            var response = new BlockListResponse();
            Whitelist.ForEach((string key, string value) =>
            {
                response.Entries.Add(new BlockListEntry { InWhitelist = true, Url = key });
                return true;
            });
            Blacklist.ForEach((string key, string value) =>
            {
                response.Entries.Add(new BlockListEntry { InWhitelist = false, Url = key });
                return true;
            });
            return response;
        }
    }
}
