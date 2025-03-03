using CefSharp;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using node.IpcService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
        private NodeService service;

        public UI(NodeService _service)
        {
            service = _service;
            InitializeComponent();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

#if DEBUG
            const string sourceUrl = "http://localhost:59102/index.html";
#else
            const string sourceUrl = "http://ui.resources/index.html";
#endif

            browser.JavascriptObjectRepository.ResolveObject += (sender, e) =>
            {
                var repo = e.ObjectRepository;
                if (!e.Url.StartsWith(sourceUrl) || e.ObjectName != "nodeService")
                {
                    return;
                }

                repo.NameConverter = null;
                repo.Register("nodeService", service);
            };

            await browser.LoadUrlAsync(sourceUrl);
            browser.ShowDevTools();
        }
    }
}
