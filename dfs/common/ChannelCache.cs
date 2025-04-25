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
    public sealed class ChannelCache : IDisposable
    {
        private MemoryCache cache;
        private readonly TimeSpan timeToLive;

        public ChannelCache(TimeSpan timeToLive)
        {
            cache = new MemoryCache(new MemoryCacheOptions());
            this.timeToLive = timeToLive;
        }

        public void Dispose()
        {
            cache.Dispose();
        }

        public GrpcChannel GetOrCreate(Uri uri, GrpcChannelOptions? options = null)
        {
            return cache.GetOrCreate(uri, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = timeToLive;
                return GrpcChannel.ForAddress(uri, options ?? new GrpcChannelOptions());
            }) ?? throw new ArgumentException("Failed to fetch channel from cache");
        }
    }
}
