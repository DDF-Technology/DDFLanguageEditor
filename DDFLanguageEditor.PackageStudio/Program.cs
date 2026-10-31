using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDF___Program_Language_Editor;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.PackageStudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 1 && string.Equals(args[0], "--smoke-home",
            StringComparison.Ordinal))
        {
            using var home = new PackageStudioHomeForm();
            return home.StartPosition == FormStartPosition.CenterScreen &&
                home.Controls.Find("packageStudioEmbeddedButton", true).Length == 1 &&
                home.Controls.Find("packageStudioDirectoryButton", true).Length == 1 &&
                home.Controls.Find("packageStudioArchiveButton", true).Length == 1
                ? 0 : 1;
        }
        bool smoke = args.Length > 0 && string.Equals(args[0],
            "--smoke", StringComparison.Ordinal);
        string[] paths = smoke ? args.Skip(1).ToArray() : args;
        if (paths.Length > 1)
            return Fail("Uso: DDFLanguagePackageStudio.exe [cartella-pacchetto | archivio.ddflang]");

        try
        {
            if (!smoke && paths.Length == 0)
            {
                Application.Run(new PackageStudioHomeForm());
                return 0;
            }
            using Form studio = OpenStudio(paths.Length == 0 ? null : paths[0]);
            if (smoke)
            {
                Control[] tabs = studio.Controls.Find(
                    "languagePackageStudioTabs", true);
                Control[] sources = studio.Controls.Find(
                    "languageStudioSourceFilesListBox", true);
                return tabs.Length == 1 && tabs[0] is TabControl tabControl &&
                    tabControl.TabPages.Count == 12 && sources.Length == 1
                    ? 0 : 1;
            }
            Application.Run(studio);
            return 0;
        }
        catch (Exception exception) when (exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is System.Text.Json.JsonException)
        {
            return Fail(exception.Message);
        }
    }

    internal static Form OpenStudio(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return DdfLanguagePackageStudio.CreateForm(RequirePackage(
                DdfLanguagePackageLoader.LoadEmbeddedDefault()));

        if (Directory.Exists(path))
        {
            DdfLanguageAuthoringResult project =
                DdfLanguagePackageAuthoring.LoadDirectory(path);
            if (!project.Success)
                throw new InvalidOperationException(string.Join(Environment.NewLine,
                    project.Diagnostics.Select(item => item.ToString())));
            // An incomplete authoring project can still be opened and repaired
            // through the shared JSON/EBNF source editor.
            DdfLanguagePackageLoadResult preview =
                DdfLanguagePackageLoader.Load(path);
            return DdfLanguagePackageStudio.CreateForm(project.Project,
                preview.Package);
        }

        return DdfLanguagePackageStudio.CreateForm(RequirePackage(
            DdfLanguagePackageLoader.Load(path)));
    }

    private static DdfLanguagePackage RequirePackage(
        DdfLanguagePackageLoadResult result)
    {
        if (result.Success) return result.Package;
        throw new InvalidOperationException(string.Join(Environment.NewLine,
            result.Diagnostics.Select(item => item.ToString())));
    }

    private static int Fail(string message)
    {
        MessageBox.Show(message, "DDF Language Package Studio",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
    }
}
