using common;
using Fs;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        public PersistentDictionary<ByteString, (long, long)> FileProgress { get; }
        private PersistentDictionary<string, string> Whitelist { get; }
        private PersistentDictionary<string, string> Blacklist { get; }
        public PersistentDictionary<ByteString, IncompleteFile> IncompleteFiles { get; private set; }
        public PersistentDictionary<ByteString, FileChunk> IncompleteChunks { get; private set; }

        private ILoggerFactory loggerFactory;
        private readonly System.Threading.Lock fileProgressLock;

        public ILogger Logger { get; private set; }
        public string LogPath { get; private set; }
        public NodeState(TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath)
        {
            fileProgressLock = new();
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
            IncompleteFiles = new(System.IO.Path.Combine(Manager.DbPath, "IncompleteFiles"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: IncompleteFile.Parser.ParseFrom
            );
            IncompleteChunks = new(System.IO.Path.Combine(Manager.DbPath, "IncompleteChunks"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: FileChunk.Parser.ParseFrom
            );
            FileProgress = new(
                System.IO.Path.Combine(Manager.DbPath, "FileProgress"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: ByteString.CopyFrom,
                valueSerializer: (a) => Encoding.UTF8.GetBytes($"{a.Item1} {a.Item2}"),
                valueDeserializer: str =>
                {
                    var parts = Encoding.UTF8.GetString(str).Split(' ');
                    if (parts.Length != 2)
                    {
                        throw new ArgumentException("Invalid format for FileProgress");
                    }
                    return (long.Parse(parts[0]), long.Parse(parts[1]));
                }
            );
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

        public void UpdateFileProgress(ByteString hash, long newProgress)
        {
            lock (fileProgressLock)
            {
                var progress = FileProgress[hash];
                progress.Item1 += newProgress;
                FileProgress[hash] = progress;
            }
        }
    }
}
