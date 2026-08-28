using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class AboutForm : Form
    {
        private const string WebsiteUrl = "https://www.ddf.technology";

        public AboutForm()
        {
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PictureBox iconPicture = Controls.Find("aboutIconPictureBox", true).Length == 0
                    ? null
                    : Controls.Find("aboutIconPictureBox", true)[0] as PictureBox;
                if (iconPicture != null && iconPicture.Image != null)
                {
                    iconPicture.Image.Dispose();
                    iconPicture.Image = null;
                }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            BackColor = Color.FromArgb(37, 37, 38);
            ClientSize = new Size(620, 500);
            Font = new Font("Segoe UI", 9F);
            ForeColor = Color.Gainsboro;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "About DDFLanguageEditor";

            System.Drawing.Icon executableIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (executableIcon != null)
            {
                Icon = executableIcon;
            }

            var iconPictureBox = new PictureBox
            {
                Image = LoadHighResolutionIcon(),
                Location = new Point(24, 25),
                Name = "aboutIconPictureBox",
                Size = new Size(96, 96),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            var productLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(140, 24),
                Name = "aboutProductLabel",
                Text = "DDFLanguageEditor"
            };

            var versionLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 183, 77),
                Location = new Point(143, 65),
                Name = "aboutVersionLabel",
                Text = "Versione " + GetInformationalVersion() + " Beta"
            };

            var statusLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.Silver,
                Location = new Point(143, 91),
                Name = "aboutStatusLabel",
                Text = "Editor e linguaggio in fase beta"
            };

            var authorLabel = new Label
            {
                AutoSize = true,
                Location = new Point(24, 145),
                Name = "aboutAuthorLabel",
                Text = "© 2026 Fabio De Deo"
            };

            var websiteLink = new LinkLabel
            {
                ActiveLinkColor = Color.White,
                AutoSize = true,
                LinkColor = Color.FromArgb(86, 182, 194),
                Location = new Point(24, 171),
                Name = "aboutWebsiteLink",
                TabStop = true,
                Text = "www.ddf.technology",
                VisitedLinkColor = Color.FromArgb(86, 182, 194)
            };
            websiteLink.LinkClicked += websiteLink_LinkClicked;

            var licenseLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(24, 211),
                Name = "aboutLicenseLabel",
                Text = "Licenza MIT"
            };

            var licenseTextBox = new TextBox
            {
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = Color.Gainsboro,
                Location = new Point(27, 236),
                Multiline = true,
                Name = "aboutLicenseTextBox",
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(566, 200),
                TabStop = false,
                Text = GetMitLicenseText()
            };

            var closeButton = new Button
            {
                BackColor = Color.FromArgb(62, 62, 64),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(493, 452),
                Name = "aboutCloseButton",
                Size = new Size(100, 30),
                Text = "Chiudi",
                UseVisualStyleBackColor = false
            };
            closeButton.Click += (sender, eventArgs) => Close();

            AcceptButton = closeButton;
            CancelButton = closeButton;
            Controls.Add(iconPictureBox);
            Controls.Add(productLabel);
            Controls.Add(versionLabel);
            Controls.Add(statusLabel);
            Controls.Add(authorLabel);
            Controls.Add(websiteLink);
            Controls.Add(licenseLabel);
            Controls.Add(licenseTextBox);
            Controls.Add(closeButton);
        }

        private static string GetInformationalVersion()
        {
            var attribute = Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            return attribute == null ? Assembly.GetExecutingAssembly().GetName().Version.ToString(3) : attribute.InformationalVersion;
        }

        private static Image LoadHighResolutionIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("DDFLanguageEditor.AppIcon.png"))
            {
                if (stream == null) return null;
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
        }

        private static string GetMitLicenseText()
        {
            return "MIT License\r\n\r\n" +
                "Copyright (c) 2026 Fabio De Deo\r\n\r\n" +
                "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\r\n\r\n" +
                "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\r\n\r\n" +
                "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
        }

        private void websiteLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "Impossibile aprire il sito web.\r\n\r\n" + exception.Message,
                    "DDFLanguageEditor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
