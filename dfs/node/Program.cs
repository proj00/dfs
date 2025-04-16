using node.IpcService;
using CefSharp;
using Grpc.Core;
using CefSharp.WinForms;
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
        [STAThread]
        static void Main()
        {
            global::System.Windows.Forms.Application.EnableVisualStyles();
            global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            global::System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);

            GrpcEnvironment.SetLogger(new Grpc.Core.Logging.LogLevelFilterLogger(
                new Grpc.Core.Logging.ConsoleLogger(),
                Grpc.Core.Logging.LogLevel.Debug));

            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var publicServer = new Grpc.Core.Server()
            {
                Services = { Node.Node.BindService(rpc) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };

            publicServer.Start();

            ServerPort port = publicServer.Ports.First();
            UI? ui = null;
            UiService service = new(state, rpc, () => ui, $"http://{port.Host}:{port.BoundPort}");

            StartGrpcWebServer(service);

            CefSharpSettings.ConcurrentTaskExecution = true;
            var settings = new CefSettings();
            settings.RootCachePath = AppDomain.CurrentDomain.BaseDirectory + "\\" + Guid.NewGuid();
            settings.CefCommandLineArgs.Add("disable-features", "BlockInsecurePrivateNetworkRequests");
#if !DEBUG
            settings.RegisterScheme(new CefCustomScheme()
            {
                SchemeName = "http",
                DomainName = "ui.resources",
                SchemeHandlerFactory = new UiResourceHandlerFactory(),
            });
#endif
            Cef.Initialize(settings);

            ui = new UI();
            System.Windows.Forms.Application.Run(ui);

            // STAThread prohibits async main, this will do
            publicServer.ShutdownAsync().Wait();
        }

        private static IHost StartGrpcWebServer(UiService service)
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
                options.ListenLocalhost(42069);
            });

            var app = builder.Build();

            app.UseRouting();
            app.UseCors("AllowAll");
            app.UseGrpcWeb();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<UiService>()
                         .EnableGrpcWeb()
                         .RequireCors("AllowAll");
            });

            app.Start();
            return app;
        }
    }
}
