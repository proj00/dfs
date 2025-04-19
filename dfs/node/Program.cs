using node.IpcService;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace node
{

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            int servicePort = -1;
            if (args.Length > 0 && int.TryParse(args[0], out int parsedPort) && parsedPort > 0)
            {
                servicePort = parsedPort;
            }
            else
            {
                Console.WriteLine("Please provide a valid port number as the first argument.");
                return;
            }

            GrpcEnvironment.SetLogger(new Grpc.Core.Logging.LogLevelFilterLogger(
                new Grpc.Core.Logging.ConsoleLogger(),
                Grpc.Core.Logging.LogLevel.Debug));

            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var publicServer = new Grpc.Core.Server()
            {
                Services = { Node.Node.BindService(rpc) },
                Ports = { new ServerPort("0.0.0.0", 0, ServerCredentials.Insecure) }
            };

            publicServer.Start();

            ServerPort publicPort = publicServer.Ports.First();
            UiService service = new(state, rpc, $"http://{publicPort.Host}:{publicPort.BoundPort}");

            StartGrpcWebServer(service, servicePort);

            await service.ShutdownEvent.WaitAsync();
            await publicServer.ShutdownAsync();
        }

        private static IHost StartGrpcWebServer(UiService service, int port)
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddGrpc();
            builder.Services.AddCors(o => o.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            builder.Services.AddSingleton(service);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port);
            });

            var app = builder.Build();

            app.UseRouting();
            app.UseCors("AllowLocalhost");
            app.UseGrpcWeb();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<UiService>()
                         .EnableGrpcWeb()
                         .RequireCors("AllowLocalhost");
            });

            app.Start();
            return app;
        }
    }
}
