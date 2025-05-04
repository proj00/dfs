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
        public IPersistentCache<ByteString, string> PathByHash { get; }
        public FilesystemManager Manager { get; }
        private ChannelCache NodeChannel { get; }
        private ChannelCache TrackerChannel { get; }
        private IPersistentCache<string, string> Whitelist { get; }
        private IPersistentCache<string, string> Blacklist { get; }
        public DownloadManager Downloads { get; }

        private readonly ILoggerFactory loggerFactory;

        public ILogger Logger { get; private set; }
        public string LogPath { get; private set; }
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private bool disposedValue;
        public TransactionManager TransactionManager { get; } = new();

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
                new ByteStringSerializer(),
                new StringSerializer()
            );
            Whitelist = new PersistentCache<string, string>(
                System.IO.Path.Combine(Manager.DbPath, "Whitelist"),
                new StringSerializer(),
                new StringSerializer()
            );
            Blacklist = new PersistentCache<string, string>(
                System.IO.Path.Combine(Manager.DbPath, "Blacklist"),
                new StringSerializer(),
                new StringSerializer()
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

            IPersistentCache<string, string> reference = request.InWhitelist ? Whitelist : Blacklist;
            if (request.ShouldRemove && await reference.ContainsKey(request.Url))
            {
                await reference.Remove(request.Url);
            }
            else
            {
                await reference.SetAsync(request.Url, request.Url);
            }
        }

        public async Task<bool> IsInBlockListAsync(Uri url)
        {
            bool passWhitelist = await Whitelist.CountEstimate() == 0;
            if (!passWhitelist)
            {
                await Whitelist.ForEach((uri, _) =>
                {
                    if (IPNetwork.TryParse(uri, out var network) && network.Contains(IPAddress.Parse(url.Host)))
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
                    if (IPNetwork.TryParse(uri, out var network) && !network.Contains(IPAddress.Parse(url.Host)))
                    {
                        passBlacklist = false;
                        return false;
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    cts.Dispose();
                    NodeChannel.Dispose();
                    TrackerChannel.Dispose();
                    Manager.Dispose();
                    PathByHash.Dispose();
                    Whitelist.Dispose();
                    Blacklist.Dispose();
                    TransactionManager.Dispose();
                    LogPath = string.Empty;
                }

                disposedValue = true;
            }
        }

        ~NodeState()
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
    }
}
