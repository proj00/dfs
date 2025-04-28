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
    public class NodeState : IDisposable
    {
        public PersistentCache<ByteString, string> PathByHash { get; }
        public FilesystemManager Manager { get; }
        private ChannelCache NodeChannel { get; }
        private ChannelCache TrackerChannel { get; }
        private PersistentCache<string, string> Whitelist { get; }
        private PersistentCache<string, string> Blacklist { get; }
        public DownloadManager Downloads { get; }

        private ILoggerFactory loggerFactory;

        public ILogger Logger { get; private set; }
        public string LogPath { get; private set; }
        private CancellationTokenSource cts = new CancellationTokenSource();
        public NodeState(TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath, string dbPath)
        {
            this.loggerFactory = loggerFactory;
            Logger = this.loggerFactory.CreateLogger("Node");
            LogPath = logPath;

            NodeChannel = new ChannelCache(channelTtl);
            TrackerChannel = new ChannelCache(channelTtl);
            Manager = new FilesystemManager(dbPath);

            PathByHash = new PersistentCache<ByteString, string>(
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

        public async Task FixBlockListAsync(BlockListRequest request)
        {
            _ = IPNetwork.Parse(request.Url);

            PersistentCache<string, string> reference = request.InWhitelist ? Whitelist : Blacklist;
            if (request.ShouldRemove && await reference.ContainsKey(request.Url))
            {
                await reference.Remove(request.Url);
            }
            else
            {
                await reference.SetAsync(request.Url, request.Url);
            }
        }

        public async Task<bool> IsInBlockListAsync(string url)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                Console.WriteLine($"warning: invalid URL {url}");
                return false;
            }

            bool passWhitelist = await Whitelist.CountEstimate() == 0;
            if (!passWhitelist)
            {
                await Whitelist.ForEach((uri, _) =>
                {
                    if (IPNetwork.TryParse(uri, out var network) && network.Contains(IPAddress.Parse((new Uri(url)).Host)))
                    {
                        passWhitelist = true;
                        return false;
                    }
                    return true;
                });
            }

            if (!passWhitelist)
            {
                return false;
            }

            bool passBlacklist = await Blacklist.CountEstimate() == 0;
            if (!passBlacklist)
            {
                await Blacklist.ForEach((uri, _) =>
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

        public async Task<BlockListResponse> GetBlockListAsync()
        {
            var response = new BlockListResponse();
            await Whitelist.ForEach((string key, string value) =>
            {
                response.Entries.Add(new BlockListEntry { InWhitelist = true, Url = key });
                return true;
            });
            await Blacklist.ForEach((string key, string value) =>
            {
                response.Entries.Add(new BlockListEntry { InWhitelist = false, Url = key });
                return true;
            });
            return response;
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            NodeChannel.Dispose();
            TrackerChannel.Dispose();
            Manager.Dispose();
            PathByHash.Dispose();
            Whitelist.Dispose();
            Blacklist.Dispose();
            LogPath = string.Empty;
        }
    }
}
