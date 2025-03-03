namespace node
{
    partial class UI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            browser = new CefSharp.WinForms.ChromiumWebBrowser();
            SuspendLayout();
            //
            // browser
            //
            browser.ActivateBrowserOnCreation = false;
            browser.Dock = DockStyle.Fill;
            browser.Location = new Point(0, 0);
            browser.Name = "browser";
            browser.Size = new Size(800, 450);
            browser.TabIndex = 0;
            //
            // UI
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(browser);
            Name = "UI";
            Text = "UI";
            ResumeLayout(false);
        }

        #endregion

        private CefSharp.WinForms.ChromiumWebBrowser browser;
    }
}
