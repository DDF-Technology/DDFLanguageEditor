using System;
using System.Windows.Forms;
using DDFLanguageEditor.Compiler;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args?.Length == 1 && string.Equals(args[0],
                DdfProductUpdateProcessHealthCheck.Argument,
                StringComparison.Ordinal))
                return verifyInstallation();
            ApplicationConfiguration.Initialize();
            var form = new MainForm();
            form.AttachStandaloneCompiledBuildService(
                new DdfDotNetEditorBuildService());
            var debugService = new DdfDotNetEditorDebugService(
                () => form.EmbeddedEditor.DocumentService,
                () => form.EmbeddedEditor.LanguageSession,
                form.PrepareStandaloneBackendExecution,
                eventDispatcher: form.EmbeddedEditor.EventDispatcher);
            form.AttachStandaloneDebugService(debugService);
            Application.Run(form);
            return 0;
        }

        private static int verifyInstallation()
        {
            try
            {
                DdfProductVersionIdentity identity =
                    DdfProductVersionCatalog.Current;
                if (string.IsNullOrWhiteSpace(identity.ProductId) ||
                    typeof(MainForm).Assembly == null ||
                    typeof(DdfDotNetEditorBuildService).Assembly == null)
                    return 1;
                // Exercise the same worker resolution performed during normal
                // startup, not just the assemblies loaded by this entry point.
                _ = new DdfDotNetEditorBuildService();
                using var debugService = new DdfDotNetEditorDebugService(
                    () => null, () => null, (_, _, _) => null);
                Console.Out.WriteLine(
                    DdfProductUpdateProcessHealthCheck.SuccessMarker);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("DDF installation self-test: " +
                    exception.Message);
                return 1;
            }
        }
    }
}
