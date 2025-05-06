using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, ChannelBase>;
    public sealed class ChannelCache : IDisposable
    {
        private readonly MemoryCache cache;
        private readonly TimeSpan timeToLive;
        private readonly GrpcChannelFactory createChannel;
        public ChannelCache(TimeSpan timeToLive, GrpcChannelFactory createChannel)
        {
            this.createChannel = createChannel;
            cache = new MemoryCache(new MemoryCacheOptions());
            this.timeToLive = timeToLive;
        }

        public void Dispose()
        {
            cache.Dispose();
        }

        public ChannelBase GetOrCreate(Uri uri, GrpcChannelOptions? options = null)
        {
            return cache.GetOrCreate(uri, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = timeToLive;
                return createChannel(uri, options ?? new GrpcChannelOptions());
            }) ?? throw new ArgumentException("Failed to fetch channel from cache");
        }
    }
}
