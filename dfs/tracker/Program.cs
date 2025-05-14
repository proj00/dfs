using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RpcCommon;
using Tracker;

#pragma warning disable CA2000
namespace tracker
{
    public class TrackerRpc : Tracker.Tracker.TrackerBase, IDisposable
    {
        private readonly IFilesystemManager _filesystemManager;
        private readonly ConcurrentDictionary<string, HashSet<string>> _peers = new();
        private readonly IPersistentCache<string, DataUsage> _dataUsage;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<System.Guid, (System.Guid, long)> transactions = new();
        private bool disposedValue;
        const int trackerResponseLimit = 30000;
        private readonly CancellationTokenSource _source;

        public TrackerRpc(ILogger logger, IFilesystemManager manager, IPersistentCache<string, DataUsage> dataUsage, CancellationTokenSource source)
        {
            _filesystemManager = manager ?? throw new ArgumentNullException(nameof(manager));
            _dataUsage = dataUsage ?? throw new ArgumentNullException(nameof(dataUsage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public TrackerRpc(ILogger logger, string dbPath, CancellationTokenSource source) : this(logger, new FilesystemManager(dbPath), new PersistentCache<string, DataUsage>(
                System.IO.Path.Combine(dbPath, "DataUsage"),
                new StringSerializer(),
                new Serializer<DataUsage>()
            ), source)
        {
        }

        public override async Task<Hash> GetContainerRootHash(
            RpcCommon.Guid request,
            ServerCallContext context
        )
        {
            var rootHash = await _filesystemManager.Container.TryGetValue(
                System.Guid.Parse(request.Guid_)
            );
            if (rootHash != null)
            {
                return new Hash { Data = rootHash };
            }
            throw new RpcException(
                new Status(StatusCode.NotFound, "Container root hash not found.")
            );
        }

        public override async Task GetObjectTree(
            Hash request,
            IServerStreamWriter<ObjectWithHash> responseStream,
            ServerCallContext context
        )
        {
            try
            {
                if (await _filesystemManager.ObjectByHash.ContainsKey(request.Data))
                {
                    foreach (var o in await _filesystemManager.GetObjectTree(request.Data))
                    {
                        if (context.CancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        await responseStream.WriteAsync(o);
                    }
                }
                else
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "Object not found."));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetObjectTree method: {ex.Message}");
                throw;
            }
        }

        // Other methods remain unchanged but should use `_logger` and `_dataUsage` where applicable.  
    }
}
#pragma warning restore CA2000