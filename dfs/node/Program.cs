using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using node.IpcService;
using CefSharp.WinForms;
using CefSharp;
using node.UiResourceLoading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Grpc.Core;
using System.Threading.Tasks;
using common_test;

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
            GrpcEnvironment.SetLogger(new Grpc.Core.Logging.LogLevelFilterLogger(
                new Grpc.Core.Logging.ConsoleLogger(),
                Grpc.Core.Logging.LogLevel.Debug));

            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var server = new Grpc.Core.Server()
            {
                Services = { Node.Node.BindService(rpc) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };

            server.Start();

            foreach (var port in server.Ports)
            {
                Console.WriteLine($"Server is listening on {port.Host}:{port.BoundPort}");
            }

            NodeService service = new(state, rpc);


            if (true) // mock for demo
            {
                var file = "C:\\Users\\as\\Documents\\paint.net App Files";
                var guid = service.ImportObjectFromDisk(file, 1024);

                var tracker = new MockTrackerWrapper
                {
                    peerId = $"http://127.0.0.1:{server.Ports.First().BoundPort}"
                };
                service.PublishToTracker(guid, tracker).Wait();

                var folder = "C:\\Users\\as\\Documents\\test";

                service.DownloadObjectByHash(guid, tracker, folder, 1).Wait();
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
            else
            {
                CefSharpSettings.ConcurrentTaskExecution = true;
#if !DEBUG
                        var settings = new CefSettings();
                        settings.RegisterScheme(new CefCustomScheme()
                        {
                            SchemeName = "http",
                            DomainName = "ui.resources",
                            SchemeHandlerFactory = new UiResourceHandlerFactory(),
                        });
                        Cef.Initialize(settings);
#endif

                global::System.Windows.Forms.Application.EnableVisualStyles();
                global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                global::System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
                System.Windows.Forms.Application.Run(new UI(service));
            }

            server.ShutdownAsync().Wait();
        }
    }
}
