using common;
using System.Net;
using Ui;

namespace node
{
    public class BlockListHandler : IDisposable
    {
        private bool disposedValue;

        public BlockListHandler(IPersistentCache<string, string> whitelist, IPersistentCache<string, string> blacklist)
        {
            Whitelist = whitelist;
            Blacklist = blacklist;
        }

        private IPersistentCache<string, string> Whitelist { get; }
        private IPersistentCache<string, string> Blacklist { get; }

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

        public async Task<bool> IsInBlockListAsync(Uri url)
        {
            bool passWhitelist = await Whitelist.CountEstimate() == 0;
            if (!passWhitelist)
            {
                await Whitelist.ForEach((uri, _) =>
                {
                    var network = IPNetwork.Parse(uri);
                    if (network.Contains(IPAddress.Parse(url.Host)))
                    {
                        passWhitelist = true;
                        return false;
                    }
                    return true;
                });
            }

            if (!passWhitelist)
            {
                return true;
            }

            if (await Blacklist.CountEstimate() != 0)
            {
                bool passBlacklist = false;
                await Blacklist.ForEach((uri, _) =>
                {
                    var network = IPNetwork.Parse(uri);
                    if (network.Contains(IPAddress.Parse(url.Host)))
                    {
                        passBlacklist = true;
                        return false;
                    }
                    return true;
                });

                return passBlacklist;
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Whitelist.Dispose();
                    Blacklist.Dispose();
                }

                disposedValue = true;
            }
        }
        ~BlockListHandler()
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
