using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Full embeddable DDF editor shell. Hosts can add this control to their layout
    /// without configuring or owning the internal <see cref="MainForm"/> instance.
    /// </summary>
    public sealed class DdfEmbeddedEditorControl : UserControl
    {
        private readonly MainForm shell;

        public DdfEmbeddedEditorControl()
        {
            Name = "ddfEmbeddedEditor";
            Dock = DockStyle.Fill;
            shell = new MainForm(true)
            {
                Name = "embeddedDdfEditorShell",
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                WindowState = FormWindowState.Normal
            };
            Controls.Add(shell);
            shell.Show();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ContractVersion => DdfEditorSdkContract.CurrentVersion;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DdfEditorView Editor => shell.EmbeddedEditor;

        [DefaultValue("Applicazione host")]
        public string HostDisplayName
        {
            get => shell.EmbeddedHostDisplayName;
            set => shell.EmbeddedHostDisplayName = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsExecutionRunning => shell.IsEmbeddedExecutionRunning;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDebugPaused => shell.IsEmbeddedDebugPaused;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasBreakpoints => shell.HasEmbeddedBreakpoints;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DdfExecutionResult LastExecutionResult => shell.LastEmbeddedExecutionResult;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DdfEmbeddedModuleStoreCapability ModuleStoreCapability { get; private set; }

        public event EventHandler ExecutionPreparing
        {
            add => shell.EmbeddedExecutionPreparing += value;
            remove => shell.EmbeddedExecutionPreparing -= value;
        }

        public event EventHandler ExecutionCompleted
        {
            add => shell.EmbeddedExecutionCompleted += value;
            remove => shell.EmbeddedExecutionCompleted -= value;
        }

        public event EventHandler<DdfEmbeddedDocumentSaveEventArgs> DocumentSaveRequested
        {
            add => shell.EmbeddedDocumentSaveRequested += value;
            remove => shell.EmbeddedDocumentSaveRequested -= value;
        }

        public string SetPrimaryDocument(string displayName, string sourceText) =>
            shell.SetEmbeddedPrimaryDocument(displayName, sourceText);

        public string AddDocument(string displayName, string sourceText,
            bool activate = false) =>
            shell.AddEmbeddedDocument(displayName, sourceText, activate);

        public bool ActivateDocument(string documentId) =>
            shell.ActivateEmbeddedDocument(documentId);

        public bool SaveActiveDocument() => shell.RequestEmbeddedDocumentSave();

        public DdfLanguagePackageLoadResult ActivateLanguagePackage(string path) =>
            shell.EmbeddedEditor.LanguageSession.TryActivate(path);

        public DdfLanguagePackageLoadResult UseEmbeddedLanguage() =>
            shell.EmbeddedEditor.LanguageSession.UseEmbedded();

        public bool ExecuteScript(bool debug = false) =>
            shell.ExecuteEmbeddedScript(debug);

        public bool ExecuteProcedure(string entryPoint,
            IReadOnlyDictionary<string, object> arguments = null, bool debug = false) =>
            shell.ExecuteEmbeddedProcedure(entryPoint, arguments, debug);

        public void ToggleBreakpoint(int oneBasedLine) =>
            shell.ToggleEmbeddedBreakpoint(oneBasedLine);

        public bool ContinueDebug() => shell.ContinueEmbeddedDebug();

        /// <summary>
        /// Reapplies syntax colors, gutter and overview after host-side layout
        /// operations that may recreate native WinForms handles.
        /// </summary>
        public void RefreshEditorPresentation() =>
            shell.RefreshEmbeddedEditorPresentation();

        /// <summary>
        /// Captures the movable toolbar order, rows and visibility so the host
        /// can persist it with its own application state.
        /// </summary>
        public string CaptureToolbarLayout() =>
            shell.CaptureEmbeddedToolbarLayout();

        /// <summary>
        /// Restores a toolbar snapshot previously captured by this SDK.
        /// Invalid or future data is rejected without changing the current layout.
        /// </summary>
        public bool RestoreToolbarLayout(string serialized) =>
            shell.RestoreEmbeddedToolbarLayout(serialized);

        public void SetModuleStoreCapability(DdfEmbeddedModuleStoreCapability capability)
        {
            shell.SetEmbeddedModuleStoreCapability(capability);
            ModuleStoreCapability = capability;
        }

        public void RevokeModuleStoreCapability() => SetModuleStoreCapability(null);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SchedulePresentationRefresh();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) SchedulePresentationRefresh();
        }

        private void SchedulePresentationRefresh()
        {
            if (!IsHandleCreated || IsDisposed || Disposing) return;
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && !Disposing && Visible)
                    shell.RefreshEmbeddedEditorPresentation();
            }));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && shell != null && !shell.IsDisposed)
                shell.Close();
            base.Dispose(disposing);
        }
    }
}
