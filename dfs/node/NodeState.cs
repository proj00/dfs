using common;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
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
        private PersistentDictionary<string, string> Whitelist { get; }
        private PersistentDictionary<string, string> Blacklist { get; }
        public DownloadManager Downloads { get; }

        private ILoggerFactory loggerFactory;

        public ILogger Logger { get; private set; }
        public string LogPath { get; private set; }
        private CancellationTokenSource cts = new CancellationTokenSource();
        public NodeState(TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath)
        {
            this.loggerFactory = loggerFactory;
            Logger = this.loggerFactory.CreateLogger("Node");
            LogPath = logPath;

            NodeChannel = new ChannelCache(channelTtl);
            TrackerChannel = new ChannelCache(channelTtl);
            Manager = new FilesystemManager();

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
                System.IO.Path.Combine(Manager.DbPath, "Blacklist"),
                keySerializer: Encoding.UTF8.GetBytes,
                keyDeserializer: Encoding.UTF8.GetString,
                valueSerializer: Encoding.UTF8.GetBytes,
                valueDeserializer: Encoding.UTF8.GetString
            );
            LogPath = logPath;
            Downloads = new DownloadManager(Manager.DbPath);
        }

        public NodeClient GetNodeClient(Uri uri, GrpcChannelOptions? options = null)
        {
            if (options == null)
            {
                options = new GrpcChannelOptions { LoggerFactory = loggerFactory };
            }
            else
            {
                options.LoggerFactory = loggerFactory;
            }
            var channel = NodeChannel.GetOrCreate(uri, options);
            return new NodeClient(channel);
        }

        public TrackerClient GetTrackerClient(Uri uri, GrpcChannelOptions? options = null)
        {
            if (options == null)
            {
                options = new GrpcChannelOptions { LoggerFactory = loggerFactory };
            }
            else
            {
                options.LoggerFactory = loggerFactory;
            }
            var channel = TrackerChannel.GetOrCreate(uri, options);
            return new TrackerClient(channel);
        }

        public void FixBlockList(BlockListRequest request)
        {
            _ = IPNetwork.Parse(request.Url);

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

            bool passWhitelist = Whitelist.CountEstimate == 0;
            if (!passWhitelist)
            {
                Whitelist.ForEach((uri, _) =>
                {
                    if (IPNetwork.TryParse(uri, out var network))
                    {
                        if (network.Contains(IPAddress.Parse((new Uri(url)).Host)))
                        {
                            passWhitelist = true;
                            return false;
                        }
                    }
                    return true;
                });
            }

            if (!passWhitelist)
            {
                return false;
            }

            bool passBlacklist = Blacklist.CountEstimate == 0;
            if (!passBlacklist)
            {
                Blacklist.ForEach((uri, _) =>
                {
                    if (IPNetwork.TryParse(uri, out var network))
                    {
                        if (!network.Contains(IPAddress.Parse((new Uri(url)).Host)))
                        {
                            passBlacklist = false;
                            return false;
                        }
                    }
                    return true;
                });
            }
            return !passBlacklist;
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
