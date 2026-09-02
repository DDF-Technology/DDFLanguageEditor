using System;
using System.Linq;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private void applyActiveLanguagePackageStatus()
        {
            DdfLanguagePackage package = DdfLanguageCatalog.DefaultPackage;
            string origin = package.Origin == DdfLanguagePackageOrigin.Directory
                ? "cartella"
                : package.Origin == DdfLanguagePackageOrigin.Archive ? "archivio" : "incorporato";

            statusEncodingLabel.Text = "UTF-8 · " + package.Name + " " + package.LanguageVersion;
            statusEncodingLabel.ToolTipText =
                "Pacchetto linguaggio: " + package.Id + " " + package.PackageVersion + Environment.NewLine +
                "Origine: " + origin + Environment.NewLine +
                "Sorgente: " + package.Source;

            if (DdfLanguageCatalog.LoadDiagnostics.Count > 0)
            {
                statusEncodingLabel.ToolTipText += Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, DdfLanguageCatalog.LoadDiagnostics.Select(item => item.ToString()));
            }
        }
    }
}
