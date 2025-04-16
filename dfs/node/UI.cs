using CefSharp;
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
        public UI()
        {
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
            await browser.LoadUrlAsync(sourceUrl);
#if DEBUG
            browser.ShowDevTools();
#endif
        }
    }
}
