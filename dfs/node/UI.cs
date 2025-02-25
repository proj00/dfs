using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace node
{
    public partial class UI : Form
    {
        public UI()
        {
            InitializeComponent();
#if DEBUG
            webView.Source = new Uri("localhost:59102", UriKind.Absolute);
#else
            webView.Source = new Uri("https://www.google.com", UriKind.Absolute);
#endif
            ((System.ComponentModel.ISupportInitialize)webView).EndInit();
            ResumeLayout(false);
        }
    }
}
