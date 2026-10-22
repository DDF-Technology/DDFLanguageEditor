using System;
using System.Windows.Forms;
using DDFLanguageEditor.Compiler;

namespace DDF___Program_Language_Editor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
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
        }
    }
}
