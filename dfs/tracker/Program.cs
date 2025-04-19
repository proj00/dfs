using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace tracker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            FilesystemManager filesystemManager = new FilesystemManager();
            TrackerRpc rpc = new TrackerRpc(filesystemManager);
            const int port = 50330;
            var app = await StartPublicServerAsync(rpc, port);

            Console.WriteLine("Running...");
            var boundPort = new Uri(app.Urls.First()).Port;

            Console.WriteLine($"Server is listening on port {boundPort}");
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();

            await app.StopAsync();
        }

        private static async Task<WebApplication> StartPublicServerAsync(TrackerRpc rpc, int port)
        {
            var builder = WebApplication.CreateBuilder();

            string policyName = "AllowAll";
            builder.Services.AddGrpc();
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
