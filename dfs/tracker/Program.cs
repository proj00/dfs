using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Runtime.InteropServices;
using System.IO;

namespace tracker
{
    internal class Program
    {
#if DEBUG
        private static LogLevel level = LogLevel.Debug;
#else
        private static LogLevel level = LogLevel.Information;
#endif
        static async Task Main(string[] args)
        {
            using CancellationTokenSource source = new();
            using var handler = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, _ =>
            {
                source.Cancel();
            });

            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(100));
            (string _, ILoggerFactory loggerFactory) = InternalLogger.CreateLoggerFactory(args.Length >= 1 ? args[0] + "\\logs" : "logs", level);
            ILogger logger = loggerFactory.CreateLogger("Main");
            using TrackerRpc rpc = new(logger, args.Length >= 1 ? args[0] : Path.Combine("./db", Guid.NewGuid().ToString()), source);
            int port = args.Length >= 2 ? int.Parse(args[1]) : 50330;
            var app = await StartPublicServerAsync(rpc, port, loggerFactory);

            logger.LogInformation("Running...");
            var boundPort = new Uri(app.Urls.First()).Port;

            logger.LogInformation($"Server is listening on port {boundPort}");
            logger.LogInformation("Press any key to quit; press a/A to view data usage...");
            while (!source.IsCancellationRequested)
            {
                await ReadInput(source, rpc, logger);
            }

            await app.StopAsync();
        }

        private static async Task ReadInput(CancellationTokenSource source, TrackerRpc rpc, ILogger logger)
        {
            char k = (char)0;
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
                source.Cancel();
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

    }
}
