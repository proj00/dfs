using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using node.IpcService;
using CefSharp.WinForms;
using CefSharp;
using node.UiResourceLoading;
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
            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var server = new Grpc.Core.Server() { Services = { Node.Node.BindService(rpc) } };
            server.Start();

            NodeService service = new(state, rpc);

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
