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
            global::System.Windows.Forms.Application.EnableVisualStyles();
            global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            global::System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);

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

            ServerPort port = server.Ports.First();
            UI? ui = null;
            NodeService service = new(state, rpc, () => ui, $"http://{port.Host}:{port.BoundPort}");

            ui = new UI(service);

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
            System.Windows.Forms.Application.Run(ui);
            server.ShutdownAsync().Wait();
        }
    }
}
