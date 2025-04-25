using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace tracker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            (string logPath, ILoggerFactory loggerFactory) = InternalLoggerProvider.CreateLoggerFactory(args.Length >= 1 ? args[0] + "\\logs" : "logs");
            ILogger logger = loggerFactory.CreateLogger("Main");
            using TrackerRpc rpc = new(logger, args.Length >= 1 ? args[0] : Path.Combine("./db", Guid.NewGuid().ToString()));
            int port = args.Length >= 2 ? int.Parse(args[1]) : 50330;
            var app = await StartPublicServerAsync(rpc, port, loggerFactory);

            logger.LogInformation("Running...");
            var boundPort = new Uri(app.Urls.First()).Port;

            logger.LogInformation($"Server is listening on port {boundPort}");
            logger.LogInformation("Press any key to quit; press a/A to view data usage...");
            while (true)
            {
                char k = Console.ReadKey(true).KeyChar;
                if (k != 'a' && k != 'A')
                {
                    break;
                }

                var usage = await rpc.GetTotalDataUsage();
                (string path, ILoggerFactory factory) = InternalLoggerProvider.CreateLoggerFactory("logs/usage");
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

            await app.StopAsync();
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
