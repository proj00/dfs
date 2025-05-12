using common;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using static Node.Node;
using static Tracker.Tracker;

namespace node
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, ChannelBase>;

    public class GrpcClientHandler : IDisposable
    {
        public GrpcClientHandler(TimeSpan channelTtl, GrpcChannelFactory channelFactory, ILoggerFactory loggerFactory)
        {
            NodeChannel = new ChannelCache(channelTtl, channelFactory);
            TrackerChannel = new ChannelCache(2 * channelTtl, channelFactory);
            this.loggerFactory = loggerFactory;
        }

        private readonly ChannelCache NodeChannel;
        private readonly ChannelCache TrackerChannel;
        private readonly ILoggerFactory loggerFactory;
        private bool disposedValue;

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

        public ITrackerWrapper GetTrackerWrapper(Uri trackerUri)
        {
            ArgumentNullException.ThrowIfNull(trackerUri);
            var client = GetTrackerClient(trackerUri);
            return new TrackerWrapper(client, trackerUri);
        }

        private TrackerClient GetTrackerClient(Uri uri, GrpcChannelOptions? options = null)
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    NodeChannel.Dispose();
                    TrackerChannel.Dispose();
                    loggerFactory.Dispose();
                }

                disposedValue = true;
            }
        }

        ~GrpcClientHandler()
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
