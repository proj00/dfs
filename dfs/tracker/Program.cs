using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace tracker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            (string logPath, ILoggerFactory loggerFactory) = InternalLoggerProvider.CreateLoggerFactory("logs");
            ILogger logger = loggerFactory.CreateLogger("Main");
            FilesystemManager filesystemManager = new FilesystemManager();
            TrackerRpc rpc = new(filesystemManager, logger);
            const int port = 50330;
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

                var usage = rpc.GetTotalDataUsage();
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
                options.ListenAnyIP(port);
            });

            var app = builder.Build();

            app.UseRouting();
            app.UseCors(policyName);
            await app.StartAsync();
            return app;
        }
    }
}
