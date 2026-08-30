using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm : Form
    {
        private const string DocumentFilter = "Sorgenti DDF (*.ddf)|*.ddf|Tutti i file (*.*)|*.*";
        private readonly DocumentSession documentSession = new DocumentSession();
        private readonly IncrementalDdfLexer incrementalLexer = new IncrementalDdfLexer();
        private readonly Timer highlightTimer;
        private readonly Timer delimiterHighlightTimer;
        private DdfLexResult lastLexResult;
        private string lastAnalyzedText = string.Empty;
        private DdfParseResult lastParseResult;
        private DdfSemanticModel lastSemanticModel;
        private DdfTypeCheckResult lastTypeCheckResult;
        private DdfDelimiterMatch activeDelimiterMatch;
        private DdfFoldProjection activeFoldProjection;
        private IReadOnlyList<DdfFoldingRange> foldingRanges = new List<DdfFoldingRange>();
        private readonly HashSet<int> collapsedFoldStarts = new HashSet<int>();
        private List<string> recentFiles;
        private FindReplaceForm findReplaceForm;
        private AboutForm aboutForm;
        private readonly ToolTip symbolToolTip;
        private DdfDocumentSymbol hoveredSymbol;
        private DdfTypedSpan hoveredTypedSpan;
        private int diagnosticsFormatStart = int.MaxValue;
        private bool isApplyingHighlighting;
        private bool isUpdatingDelimiterHighlight;
        private bool isMouseSelecting;
        private bool isReplacingDocument;
        private Func<OpenFileDialog, DialogResult> showOpenFileDialog;
        private Func<SaveFileDialog, DialogResult> showSaveFileDialog;
        private Action<string> saveRecentFilesSetting;
        private Func<string, string> requestSymbolRename;
        private Func<FolderBrowserDialog, DialogResult> showWorkspaceDialog;
        private Func<string> requestRuntimeInput;

        public MainForm()
        {
            InitializeComponent();
            initializeMainToolbar();
            initializeDebugger();

            System.Drawing.Icon applicationIcon = AppIconProvider.LoadIcon();
            if (applicationIcon != null)
            {
                Icon = applicationIcon;
            }

            highlightTimer = new Timer
            {
                Interval = 75
            };
            highlightTimer.Tick += highlightTimer_Tick;
            delimiterHighlightTimer = new Timer
            {
                Interval = 100
            };
            delimiterHighlightTimer.Tick += delimiterHighlightTimer_Tick;
            symbolToolTip = new ToolTip
            {
                AutomaticDelay = 350,
                AutoPopDelay = 5000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true
            };
            richTextBoxMainEditor.MouseDown += richTextBoxMainEditor_MouseDown;
            richTextBoxMainEditor.MouseUp += richTextBoxMainEditor_MouseUp;
            richTextBoxMainEditor.MouseMove += richTextBoxMainEditor_MouseMove;
            richTextBoxMainEditor.MouseLeave += richTextBoxMainEditor_MouseLeave;
            richTextBoxFoldedView.VScroll += richTextBoxFoldedView_VScroll;
            richTextBoxFoldedView.SelectionChanged += richTextBoxFoldedView_SelectionChanged;
            richTextBoxLineNumbers.TargetControl = richTextBoxMainEditor;
            initializeWorkspace();
            closeWorkspaceMenuItem.Enabled = false;
            initializePaletteBehavior();
            recentFiles = new List<string>(RecentFileList.Parse(AppSettingsStore.LoadRecentFiles()));
            initializeCompletion();
            showOpenFileDialog = dialog => dialog.ShowDialog(this);
            showSaveFileDialog = dialog => dialog.ShowDialog(this);
            saveRecentFilesSetting = AppSettingsStore.SaveRecentFiles;
            requestSymbolRename = showRenameSymbolDialog;
            showWorkspaceDialog = dialog => dialog.ShowDialog(this);
            requestRuntimeInput = showRuntimeInputDialog;
            applyApplicationTheme();
        }

        private void applyApplicationTheme()
        {
            AppTheme.ApplyLight(this);
            toolbarRunButton.ForeColor = AppTheme.Run;
            toolbarStopButton.ForeColor = AppTheme.Stop;
            toolbarBreakpointButton.ForeColor = AppTheme.Stop;
            listBoxDiagnostics.ForeColor = AppTheme.Error;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            stopExecution();
            highlightTimer.Stop();
            delimiterHighlightTimer.Stop();
            disposeCompletion();
            disposePaletteBehavior();
            symbolToolTip.Dispose();
            highlightTimer.Dispose();
            delimiterHighlightTimer.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            updateLineNumbers();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            documentSession.SetUntitled();
            refreshRecentMenu();
            updateDocumentUi();
            applyHighlighting();
            updateLineNumbers();
            updateCaretPosition();
            richTextBoxMainEditor.Focus();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!confirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }

            symbolToolTip.Active = false;
            symbolToolTip.Hide(richTextBoxMainEditor);
            symbolToolTip.RemoveAll();
            persistRecentFiles();
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            if (aboutForm == null || aboutForm.IsDisposed)
            {
                aboutForm = new AboutForm();
                aboutForm.FormClosed += (closedSender, closedArgs) => aboutForm = null;
                aboutForm.Show(this);
                return;
            }

            if (aboutForm.WindowState == FormWindowState.Minimized)
            {
                aboutForm.WindowState = FormWindowState.Normal;
            }

            aboutForm.Activate();
        }

        private void newMenuItem_Click(object sender, EventArgs e)
        {
            if (!confirmDiscardChanges())
            {
                return;
            }

            documentSession.SetUntitled();
            replaceEditorText(string.Empty);
            updateDocumentUi();
            richTextBoxMainEditor.Focus();
        }

        private void openMenuItem_Click(object sender, EventArgs e)
        {
            if (!confirmDiscardChanges())
            {
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = DocumentFilter;
                dialog.DefaultExt = "ddf";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                dialog.Title = "Apri sorgente DDF";
                if (documentSession.HasPath)
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(documentSession.CurrentPath);
                }

                if (showOpenFileDialog(dialog) == DialogResult.OK)
                {
                    openDocument(dialog.FileName);
                }
            }
        }

        private void saveMenuItem_Click(object sender, EventArgs e)
        {
            saveDocument(false);
        }

        private void saveAsMenuItem_Click(object sender, EventArgs e)
        {
            saveDocument(true);
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void recentFileMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            string path = menuItem?.Tag as string;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                recentFiles.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
                persistRecentFiles();
                refreshRecentMenu();
                MessageBox.Show(
                    this,
                    "Il file recente non esiste più:\n" + path,
                    "File non trovato",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (confirmDiscardChanges())
            {
                openDocument(path);
            }
        }

        private bool openDocument(string path)
        {
            try
            {
                string content = DdfDocumentFile.Load(path);
                documentSession.SetLoaded(path);
                replaceEditorText(content);
                updateWorkspaceDocument(path, content);
                addRecentFile(path);
                updateDocumentUi();
                richTextBoxMainEditor.Focus();
                return true;
            }
            catch (Exception exception) when (isDocumentException(exception))
            {
                showDocumentError("Impossibile aprire il documento", path, exception);
                return false;
            }
        }

        private bool saveDocument(bool forceSaveAs)
        {
            string path = documentSession.CurrentPath;
            if (forceSaveAs || string.IsNullOrEmpty(path))
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = DocumentFilter;
                    dialog.DefaultExt = "ddf";
                    dialog.AddExtension = true;
                    dialog.OverwritePrompt = true;
                    dialog.Title = "Salva sorgente DDF";
                    dialog.FileName = documentSession.DisplayName;
                    if (documentSession.HasPath)
                    {
                        dialog.InitialDirectory = Path.GetDirectoryName(documentSession.CurrentPath);
                    }

                    if (showSaveFileDialog(dialog) != DialogResult.OK)
                    {
                        return false;
                    }

                    path = dialog.FileName;
                }
            }

            try
            {
                DdfDocumentFile.Save(path, richTextBoxMainEditor.Text);
                documentSession.MarkSaved(path);
                richTextBoxMainEditor.Modified = false;
                addRecentFile(path);
                updateDocumentUi();
                updateWorkspaceDocument(path, richTextBoxMainEditor.Text);
                return true;
            }
            catch (Exception exception) when (isDocumentException(exception))
            {
                showDocumentError("Impossibile salvare il documento", path, exception);
                return false;
            }
        }

        private bool confirmDiscardChanges()
        {
            if (!documentSession.IsDirty)
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                this,
                "Salvare le modifiche a " + documentSession.DisplayName + "?",
                "Modifiche non salvate",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Cancel)
            {
                return false;
            }

            return result == DialogResult.No || saveDocument(false);
        }

        private void replaceEditorText(string content)
        {
            leaveFoldedView();
            clearBreakpoints();
            isReplacingDocument = true;
            highlightTimer.Stop();
            incrementalLexer.Reset();
            try
            {
                richTextBoxMainEditor.Text = content ?? string.Empty;
                richTextBoxMainEditor.Select(0, 0);
                richTextBoxMainEditor.ClearUndo();
                richTextBoxMainEditor.Modified = false;
            }
            finally
            {
                isReplacingDocument = false;
            }

            applyHighlighting();
            updateLineNumbers();
            updateCaretPosition();
        }

        private void addRecentFile(string path)
        {
            recentFiles = new List<string>(RecentFileList.Add(recentFiles, path));
            persistRecentFiles();
            refreshRecentMenu();
        }

        private void refreshRecentMenu()
        {
            recentMenuItem.DropDownItems.Clear();
            int index = 1;
            foreach (string path in recentFiles.Where(File.Exists))
            {
                var item = new ToolStripMenuItem
                {
                    Name = "recentFileMenuItem" + index,
                    Text = "&" + index + " " + Path.GetFileName(path) + " — " + Path.GetDirectoryName(path),
                    Tag = path,
                    ToolTipText = path
                };
                item.Click += recentFileMenuItem_Click;
                recentMenuItem.DropDownItems.Add(item);
                index++;
            }

            if (recentMenuItem.DropDownItems.Count == 0)
            {
                recentMenuItem.DropDownItems.Add(new ToolStripMenuItem("(nessun file recente)") { Enabled = false });
            }
        }

        private void persistRecentFiles()
        {
            try
            {
                saveRecentFilesSetting(RecentFileList.Serialize(recentFiles));
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText = "Impossibile memorizzare i file recenti: " + exception.Message;
            }
        }

        private void updateDocumentUi()
        {
            string dirtyMarker = documentSession.IsDirty ? "*" : string.Empty;
            Text = "DDFLanguageEditor 0.9.0 Beta — " + documentSession.DisplayName + dirtyMarker;
            statusFileLabel.Text = documentSession.HasPath
                ? documentSession.CurrentPath + dirtyMarker
                : documentSession.DisplayName + dirtyMarker;
            saveMenuItem.Enabled = documentSession.IsDirty || !documentSession.HasPath;
        }

        private void editMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (richTextBoxFoldedView.Visible)
            {
                undoMenuItem.Enabled = false;
                redoMenuItem.Enabled = false;
                cutMenuItem.Enabled = false;
                copyMenuItem.Enabled = richTextBoxFoldedView.SelectionLength > 0;
                pasteMenuItem.Enabled = false;
                selectAllMenuItem.Enabled = richTextBoxFoldedView.TextLength > 0;
                completionMenuItem.Enabled = false;
                formatDocumentMenuItem.Enabled = richTextBoxMainEditor.TextLength > 0;
                goToDefinitionMenuItem.Enabled = false;
                renameSymbolMenuItem.Enabled = false;
                return;
            }

            undoMenuItem.Enabled = richTextBoxMainEditor.CanUndo;
            redoMenuItem.Enabled = richTextBoxMainEditor.CanRedo;
            cutMenuItem.Enabled = richTextBoxMainEditor.SelectionLength > 0;
            copyMenuItem.Enabled = richTextBoxMainEditor.SelectionLength > 0;
            pasteMenuItem.Enabled = clipboardContainsText();
            selectAllMenuItem.Enabled = richTextBoxMainEditor.TextLength > 0;
            completionMenuItem.Enabled = true;
            formatDocumentMenuItem.Enabled = richTextBoxMainEditor.TextLength > 0;
            DdfSymbolOccurrence occurrence = getCurrentSymbolOccurrence();
            goToDefinitionMenuItem.Enabled = occurrence != null || getWorkspaceSymbolAtCaret(richTextBoxMainEditor.SelectionStart) != null;
            renameSymbolMenuItem.Enabled = occurrence != null;
        }

        private static bool clipboardContainsText()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try { return Clipboard.ContainsText(); }
                catch (ExternalException) { System.Threading.Thread.Sleep(10); }
            }
            return false;
        }

        private static bool trySetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (ExternalException) { System.Threading.Thread.Sleep(10); }
            }
            return false;
        }

        private static bool tryGetClipboardText(out string text)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                    return text != null;
                }
                catch (ExternalException) { System.Threading.Thread.Sleep(10); }
            }
            text = null;
            return false;
        }

        private void copySelectionToClipboard()
        {
            RichTextBox source = richTextBoxFoldedView.Visible ? richTextBoxFoldedView : richTextBoxMainEditor;
            if (source.SelectionLength > 0) trySetClipboardText(source.SelectedText);
        }

        private void cutSelectionToClipboard()
        {
            if (richTextBoxFoldedView.Visible || richTextBoxMainEditor.SelectionLength == 0) return;
            if (trySetClipboardText(richTextBoxMainEditor.SelectedText))
                richTextBoxMainEditor.SelectedText = string.Empty;
        }

        private void pasteTextFromClipboard()
        {
            if (richTextBoxFoldedView.Visible) return;
            if (tryGetClipboardText(out string text)) richTextBoxMainEditor.SelectedText = text;
        }

        private void undoMenuItem_Click(object sender, EventArgs e)
        {
            if (richTextBoxMainEditor.CanUndo)
            {
                richTextBoxMainEditor.Undo();
            }
        }

        private void redoMenuItem_Click(object sender, EventArgs e)
        {
            if (richTextBoxMainEditor.CanRedo)
            {
                richTextBoxMainEditor.Redo();
            }
        }

        private void cutMenuItem_Click(object sender, EventArgs e)
        {
            cutSelectionToClipboard();
        }

        private void copyMenuItem_Click(object sender, EventArgs e)
        {
            copySelectionToClipboard();
        }

        private void pasteMenuItem_Click(object sender, EventArgs e)
        {
            pasteTextFromClipboard();
        }

        private void selectAllMenuItem_Click(object sender, EventArgs e)
        {
            if (richTextBoxFoldedView.Visible)
            {
                richTextBoxFoldedView.SelectAll();
                richTextBoxFoldedView.Focus();
            }
            else
            {
                richTextBoxMainEditor.SelectAll();
                richTextBoxMainEditor.Focus();
            }
        }

        private void findMenuItem_Click(object sender, EventArgs e)
        {
            showFindReplace(false);
        }

        private void replaceMenuItem_Click(object sender, EventArgs e)
        {
            showFindReplace(true);
        }

        private void showFindReplace(bool showReplace)
        {
            leaveFoldedView();
            if (findReplaceForm == null || findReplaceForm.IsDisposed)
            {
                findReplaceForm = new FindReplaceForm(richTextBoxMainEditor);
                findReplaceForm.FormClosed += (sender, args) => findReplaceForm = null;
                findReplaceForm.SetReplaceMode(showReplace);
                findReplaceForm.Show(this);
            }
            else
            {
                findReplaceForm.SetReplaceMode(showReplace);
            }
            if (richTextBoxMainEditor.SelectionLength > 0)
            {
                findReplaceForm.PrefillFind(richTextBoxMainEditor.SelectedText);
            }
            findReplaceForm.Activate();
        }

        private static bool isDocumentException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is ArgumentException ||
                   exception is NotSupportedException ||
                   exception is System.Security.SecurityException;
        }

        private void showDocumentError(string title, string path, Exception exception)
        {
            MessageBox.Show(
                this,
                title + ":\n" + path + "\n\n" + exception.Message,
                "Errore documento",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
