using node.IpcService;
using CefSharp;
using Grpc.Core;
using CefSharp.WinForms;
using node.UiResourceLoading;

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

            var internalServer = new Grpc.Core.Server()
            {
                Services = { Ui.Ui.BindService(service) },
                Ports = { new ServerPort("localhost", 42069, ServerCredentials.Insecure) }
            };

            internalServer.Start();

            CefSharpSettings.ConcurrentTaskExecution = true;
            var settings = new CefSettings();
            settings.RootCachePath = AppDomain.CurrentDomain.BaseDirectory + "\\" + Guid.NewGuid();
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
            Task[] tasks = [internalServer.ShutdownAsync(), publicServer.ShutdownAsync()];
            var shutdown = Task.WhenAll(tasks);

            // STAThread prohibits async main, this will do
            shutdown.Wait();
        }
    }
}
