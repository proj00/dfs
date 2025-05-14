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
                k = Console.ReadKey(true).KeyChar;
            }
            catch { }
            if (k == (char)0)
            {
                await Task.Delay(3000);
                return;
            }
            if (k != 'a' && k != 'A')
            {
                await source.CancelAsync();
            }

            var usage = await rpc.GetTotalDataUsage();
            (string path, ILoggerFactory factory) = InternalLogger.CreateLoggerFactory("logs/usage", level);
            var usageLogger = factory.CreateLogger("DataUsage");

            string output = "";

            output += "Data usage: \n";
            if (usage.Length == 0)
            {
                output += "No data usage found.\n";
            }
            else
            {
                foreach (var (key, u) in usage)
                {
                    output += $"URL: {key}, Up/Down: {u.Upload}/{u.Download} bytes\n";
                }
            }
            usageLogger.LogInformation(output);
            logger.LogInformation($"Usage logs written to {path}");
        }

        private static async Task<WebApplication> StartPublicServerAsync(TrackerRpc rpc, int port, ILoggerFactory loggerFactory)
        {
            var builder = WebApplication.CreateBuilder();

            // Define the CORS policy
            string policyName = "AllowAll";
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(loggerFactory);
            builder.Services.AddLogging();

            builder.Services.AddCors(o => o.AddPolicy(policyName, policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            builder.Services.AddSingleton(rpc);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port, o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                });
            });

            var app = builder.Build();

            app.UseRouting();
            app.UseCors(policyName);

            app.MapGrpcService<TrackerRpc>().RequireCors(policyName);

            await app.StartAsync();
            return app;
        }

        // Other methods remain unchanged but should use `_logger` and `_dataUsage` where applicable.  
    }
}
#pragma warning restore CA2000