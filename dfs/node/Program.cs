using node.IpcService;
using CefSharp;
using Grpc.Core;

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
            service.ImportObjectFromDisk(@"C:\\Users\\as\\Documents\\paint.net App Files", 1024);

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

            server.ShutdownAsync().Wait();
        }
    }
}
