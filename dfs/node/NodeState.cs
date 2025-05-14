using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Node;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Navigation;
using Tracker;
using Ui;
using static Node.Node;
using static Tracker.Tracker;
using static Ui.Ui;

namespace node
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, GrpcChannel>;

    public partial class NodeState : INodeState
    {
        public IFilesystemManager Manager { get; }
        public IDownloadManager Downloads { get; }
        public IBlockListHandler BlockList { get; }

        private readonly ILoggerFactory loggerFactory;
        public ILogger Logger { get; private set; }
        public IFilePathHandler PathHandler { get; }
        public string LogPath { get; private set; }
        private bool disposedValue;
        public TransactionManager Transactions { get; }
        public IAsyncIOWrapper AsyncIO { get; }
        public GrpcClientHandler ClientHandler { get; }
        public ObjectDownloadHandler Objects { get; }

        public NodeState(IFileSystem fs, TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath,
            IFilesystemManager manager, IDownloadManager downloads,
            IFilePathHandler pathHandler,
            IBlockListHandler blockList,
            GrpcChannelFactory grpcChannelFactory, IAsyncIOWrapper io, INativeMethods nativeMethods)
        {
            this.AsyncIO = io;
            this.loggerFactory = loggerFactory;
            Logger = this.loggerFactory.CreateLogger("Node");
            LogPath = logPath;
            Manager = manager;
            Downloads = downloads;

            this.ClientHandler = new(channelTtl, grpcChannelFactory, loggerFactory);
            this.PathHandler = pathHandler;
            BlockList = blockList;
            Transactions = new(Logger);
            Objects = new(fs, Logger, PathHandler, ClientHandler, Downloads, AsyncIO, Manager, nativeMethods);
        }

        public NodeState(TimeSpan channelTtl, ILoggerFactory loggerFactory, string logPath, string dbPath)
            : this(new FileSystem(), channelTtl, loggerFactory, logPath,
#pragma warning disable CA2000 // Dispose objects before losing scope
                  new FilesystemManager(dbPath),
                  new DownloadManager(loggerFactory, dbPath),
                  new FilePathHandler(new PersistentCache<ByteString, string>(
                System.IO.Path.Combine(dbPath, "PathByHash"),
                new ByteStringSerializer(),
                new StringSerializer()),
                    (string name, string args) => Process.Start(name, args)
                ),
            new BlockListHandler(
                    new PersistentCache<string, string>(
                    System.IO.Path.Combine(dbPath, "Whitelist"),
                    new StringSerializer(),
                    new StringSerializer()
                ), new PersistentCache<string, string>(
                    System.IO.Path.Combine(dbPath, "Blacklist"),
                    new StringSerializer(),
                    new StringSerializer()
                )
            ),
#pragma warning restore CA2000 // Dispose objects before losing scope
                GrpcChannel.ForAddress,
                new AsyncIOWrapper(),
                new NativeMethods()
                )
        { }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ClientHandler.Dispose();
                    Manager.Dispose();
                    PathHandler.Dispose();
                    BlockList.Dispose();
                    LogPath = string.Empty;
                    loggerFactory.Dispose();
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
