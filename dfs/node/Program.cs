using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using node.IpcService;
using CefSharp.WinForms;
using CefSharp;
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
            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeService service = new(state);

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

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            global::System.Windows.Forms.Application.EnableVisualStyles();
            global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            global::System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
            System.Windows.Forms.Application.Run(new UI(service));
        }
    }
}
