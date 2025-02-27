using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace node
{
    public partial class UI : Form
    {
        public UI()
        {
            InitializeComponent();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

#if DEBUG
            webView.Source = new Uri("localhost:59102", UriKind.Absolute);
#else
            await LoadUiResources();
#endif
        }

        private async Task LoadUiResources()
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.AddWebResourceRequestedFilter("http://ui.resources/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);

            webView.CoreWebView2.WebResourceRequested +=
                (object? sender, CoreWebView2WebResourceRequestedEventArgs args) =>
            {
                string path = args.Request.Uri[20..];
                var contents = GetContents(path);

                Console.WriteLine($"{path} {(contents == null ? 404 : 200)}");

                if (contents == null)
                {
                    args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(), 404, "Not Found", "");
                    return;
                }

                List<string> headers = ["Access-Control-Allow-Origin: *"];

                new FileExtensionContentTypeProvider().TryGetContentType(path, out string? contentType);
                headers.Add($"Content-Type: {contentType ?? "application/octet-stream"}");

                var headerField = headers.Aggregate((p, h) => $"{p}\r\n{h}");
                var response = webView.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(contents), 200, "OK", headerField);
                args.Response = response;
            };

            webView.NavigateToString(Encoding.UTF8.GetString(GetContents("index.html") ?? throw new Exception(">:(")));
        }

        private static byte[]? GetContents(string resourceName)
        {
            object? contents = UiResources.ResourceManager.GetObject(resourceName, UiResources.Culture);
            return contents == null ? null : (byte[])contents;
        }
    }
}
