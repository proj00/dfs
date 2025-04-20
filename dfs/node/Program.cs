using node.IpcService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RocksDbSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace node
{

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            int servicePort = 0;
            if (args.Length > 0 && int.TryParse(args[0], out int parsedPort) && parsedPort > 0 && parsedPort < 65536)
            {
                servicePort = parsedPort;
            }
            else
            {
                Console.WriteLine("Please provide a valid port number as the first argument (integer between 1 and 65535).");
                return;
            }
#if !DEBUG
            servicePort = 42069;
#endif

            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var publicServer = await StartPublicNodeServerAsync(rpc);
            var publicUrl = publicServer.Urls.First();

            UiService service = new(state, rpc, "publicUrl");
            var privateServer = await StartGrpcWebServerAsync(service, servicePort);

            await service.ShutdownEvent.WaitAsync();
            await publicServer.StopAsync();
            await privateServer.StopAsync();
        }

        private static async Task<WebApplication> StartPublicNodeServerAsync(NodeRpc rpc)
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
                options.ListenAnyIP(0, o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                });
            });
            builder.Services.AddGrpcReflection();

            var app = builder.Build();
            IWebHostEnvironment env = app.Environment;
#if DEBUG
            app.MapGrpcReflectionService();
#endif

            app.UseRouting();
            app.UseCors(policyName);
            await app.StartAsync();
            return app;
        }

        private static async Task<WebApplication> StartGrpcWebServerAsync(UiService service, int port)
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddGrpc();
            builder.Services.AddCors(o => o.AddPolicy("AllowAll", policy =>
            {
                policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            builder.Services.AddSingleton(service);
            builder.Services.AddGrpcReflection();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port
                    , o =>
                {
                    o.Protocols = HttpProtocols.Http1;
                }
                );
#if DEBUG
                options.ListenLocalhost(port + 1
                    , o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                }
                );
#endif
            });
            var app = builder.Build();

            IWebHostEnvironment env = app.Environment;
#if DEBUG
            app.MapGrpcReflectionService();
#endif

            app.UseRouting();
            app.UseCors("AllowAll");
            app.UseGrpcWeb();
            app.MapGrpcService<UiService>().EnableGrpcWeb().RequireCors("AllowAll");

            await app.StartAsync();
            return app;
        }
    }
}
