using node.IpcService;
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

            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var publicServer = await StartPublicNodeServerAsync(rpc);
            var publicUrl = publicServer.Urls.First();

            UiService service = new(state, rpc, publicUrl);
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
                options.ListenAnyIP(0);
            });

            var app = builder.Build();

            app.UseRouting();
            app.UseCors(policyName);
            await app.StartAsync();
            return app;
        }

        private static async Task<WebApplication> StartGrpcWebServerAsync(UiService service, int port)
        {
            var builder = WebApplication.CreateBuilder();

            string policyName = "AllowLocalhost";
            builder.Services.AddGrpc();
            builder.Services.AddCors(o => o.AddPolicy(policyName, policy =>
            {
                policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
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
            app.UseCors(policyName);
            app.UseGrpcWeb();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<UiService>()
                         .EnableGrpcWeb()
                         .RequireCors(policyName);
            });

            await app.StartAsync();
            return app;
        }
    }
}
