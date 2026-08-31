using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private DdfWorkspaceIndex workspaceIndex;
        private TabControl navigationTabs;
        private TabPage workspaceTabPage;
        private TreeView treeViewWorkspace;
        private Label labelWorkspace;

        private void initializeWorkspace()
        {
            panelOutline.Controls.Remove(treeViewOutline);
            panelOutline.Controls.Remove(labelOutline);

            navigationTabs = new TabControl
            {
                Name = "navigationTabs",
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                BackColor = AppTheme.Window,
                ForeColor = AppTheme.Text
            };
            workspaceTabPage = new TabPage("File")
            {
                Name = "workspaceTabPage",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            var outlinePage = new TabPage("Outline")
            {
                Name = "outlineTabPage",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };

            labelWorkspace = new Label
            {
                Name = "labelWorkspace",
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(8, 0, 4, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = AppTheme.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "WORKSPACE — nessuna cartella"
            };
            treeViewWorkspace = new TreeView
            {
                Name = "treeViewWorkspace",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9F),
                FullRowSelect = true,
                HideSelection = false,
                ShowLines = true
            };
            treeViewWorkspace.NodeMouseDoubleClick += treeViewWorkspace_NodeMouseDoubleClick;

            workspaceTabPage.Controls.Add(treeViewWorkspace);
            workspaceTabPage.Controls.Add(labelWorkspace);
            outlinePage.Controls.Add(treeViewOutline);
            outlinePage.Controls.Add(labelOutline);
            navigationTabs.TabPages.Add(workspaceTabPage);
            navigationTabs.TabPages.Add(outlinePage);
            navigationTabs.SelectedTab = outlinePage;
            panelOutline.Controls.Add(navigationTabs);
            navigationTabs.BringToFront();
            buttonOutlinePin.BringToFront();
        }

        private void openWorkspaceMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Seleziona la cartella contenente i sorgenti DDF",
                ShowNewFolderButton = false,
                SelectedPath = workspaceIndex?.RootPath ?? string.Empty
            })
            {
                if (showWorkspaceDialog(dialog) == DialogResult.OK) openWorkspace(dialog.SelectedPath);
            }
        }

        private void closeWorkspaceMenuItem_Click(object sender, EventArgs e)
        {
            workspaceIndex = null;
            treeViewWorkspace.Nodes.Clear();
            labelWorkspace.Text = "WORKSPACE — nessuna cartella";
            closeWorkspaceMenuItem.Enabled = false;
            applyHighlighting();
            scheduleWorkspaceSearch();
        }

        private bool openWorkspace(string rootPath)
        {
            try
            {
                workspaceIndex = DdfWorkspaceIndex.Load(rootPath);
                foreach (OpenDocumentBuffer document in openDocuments.Documents)
                {
                    if (document.Session.HasPath && workspaceIndex.ContainsPath(document.Session.CurrentPath))
                    {
                        workspaceIndex = workspaceIndex.WithDocument(document.Session.CurrentPath, document.Source);
                    }
                }
                populateWorkspaceTree();
                closeWorkspaceMenuItem.Enabled = true;
                navigationTabs.SelectedTab = workspaceTabPage;
                applyHighlighting();
                scheduleWorkspaceSearch();
                return true;
            }
            catch (Exception exception) when (isWorkspaceException(exception))
            {
                MessageBox.Show(this, exception.Message, "Impossibile aprire la cartella", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void populateWorkspaceTree()
        {
            treeViewWorkspace.BeginUpdate();
            try
            {
                treeViewWorkspace.Nodes.Clear();
                if (workspaceIndex == null) return;
                var root = new TreeNode(new DirectoryInfo(workspaceIndex.RootPath).Name) { Tag = workspaceIndex.RootPath };
                foreach (DdfWorkspaceDocument document in workspaceIndex.Documents)
                {
                    addWorkspacePath(root, document.RelativePath.Split(Path.DirectorySeparatorChar), document.Path);
                }
                treeViewWorkspace.Nodes.Add(root);
                root.Expand();
                labelWorkspace.Text = "WORKSPACE — " + workspaceIndex.Documents.Count +
                    (workspaceIndex.Documents.Count == 1 ? " file" : " file");
            }
            finally
            {
                treeViewWorkspace.EndUpdate();
            }
        }

        private static void addWorkspacePath(TreeNode root, string[] parts, string fullPath)
        {
            TreeNode parent = root;
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                TreeNode child = parent.Nodes.Cast<TreeNode>().FirstOrDefault(node => node.Text == part);
                if (child == null)
                {
                    child = new TreeNode(part);
                    parent.Nodes.Add(child);
                }
                parent = child;
            }
            parent.Tag = fullPath;
        }

        private void treeViewWorkspace_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string path = e.Node.Tag as string;
            if (path == null || !File.Exists(path)) return;
            if (!prepareWorkspaceDocumentSwitch(path)) return;
            navigateWithHistory(() => openDocument(path));
        }

        private void updateWorkspaceDocument(string path, string source)
        {
            if (workspaceIndex == null || string.IsNullOrEmpty(path) || !workspaceIndex.ContainsPath(path)) return;
            workspaceIndex = workspaceIndex.WithDocument(path, source);
        }

        private void updateWorkspaceDocument(string path, string source, CompilationUnitSyntax root)
        {
            if (workspaceIndex == null || string.IsNullOrEmpty(path) || !workspaceIndex.ContainsPath(path)) return;
            workspaceIndex = workspaceIndex.WithDocument(path, source, root);
        }

        private IReadOnlyList<DdfCompletionItem> getWorkspaceCompletionItems()
        {
            if (workspaceIndex == null) return null;
            return workspaceIndex.Documents
                .Where(document => !string.Equals(document.Path, documentSession.CurrentPath, StringComparison.OrdinalIgnoreCase))
                .SelectMany(document => document.Symbols
                    .Where(symbol => symbol.Kind == DdfSymbolKind.Library ||
                                     symbol.Kind == DdfSymbolKind.Structure ||
                                     symbol.Kind == DdfSymbolKind.Function ||
                                     symbol.Kind == DdfSymbolKind.Variable)
                    .Select(symbol => DdfCompletionService.CreateSymbolItem(symbol, document.RelativePath)))
                .ToList()
                .AsReadOnly();
        }

        private IReadOnlyList<CompilationUnitSyntax> getWorkspaceTypeRoots()
        {
            return workspaceIndex == null
                ? null
                : workspaceIndex.GetExternalRoots(documentSession.CurrentPath);
        }

        private string getCurrentDocumentOrigin()
        {
            if (workspaceIndex != null && documentSession.HasPath)
            {
                DdfWorkspaceDocument document = workspaceIndex.Documents.FirstOrDefault(item =>
                    string.Equals(item.Path, documentSession.CurrentPath, StringComparison.OrdinalIgnoreCase));
                if (document != null) return document.RelativePath;
            }

            return documentSession.DisplayName;
        }

        private bool isResolvedByWorkspace(DdfDiagnostic diagnostic, string source)
        {
            if (workspaceIndex == null || diagnostic.Code != "DDF201" || diagnostic.Start + diagnostic.Length > source.Length) return false;
            string name = source.Substring(diagnostic.Start, diagnostic.Length);
            return workspaceIndex.FindDefinitions(name).Count > 0;
        }

        private DdfWorkspaceSymbol getWorkspaceSymbolAtCaret(int position)
        {
            if (workspaceIndex == null || lastLexResult == null || !string.Equals(lastAnalyzedText, richTextBoxMainEditor.Text, StringComparison.Ordinal)) return null;
            DdfToken token = lastLexResult.Tokens.FirstOrDefault(item =>
                item.Kind == DdfTokenKind.Identifier && position >= item.Start && position <= item.End);
            if (token == null) return null;
            string name = richTextBoxMainEditor.Text.Substring(token.Start, token.Length);
            IReadOnlyList<DdfWorkspaceSymbol> definitions = workspaceIndex.FindDefinitions(name);
            return definitions.FirstOrDefault(definition =>
                       string.Equals(definition.Document.Path, documentSession.CurrentPath, StringComparison.OrdinalIgnoreCase))
                   ?? definitions.FirstOrDefault();
        }

        private bool navigateToWorkspaceSymbol(DdfWorkspaceSymbol definition)
        {
            if (definition == null) return false;
            return navigateWithHistory(() => navigateToWorkspaceSymbolCore(definition));
        }

        private bool navigateToWorkspaceSymbolCore(DdfWorkspaceSymbol definition)
        {
            if (!string.Equals(documentSession.CurrentPath, definition.Document.Path, StringComparison.OrdinalIgnoreCase))
            {
                if (!prepareWorkspaceDocumentSwitch(definition.Document.Path) || !openDocument(definition.Document.Path)) return false;
            }
            int start = Math.Min(definition.Symbol.SelectionStart, richTextBoxMainEditor.TextLength);
            int length = Math.Min(definition.Symbol.SelectionLength, richTextBoxMainEditor.TextLength - start);
            richTextBoxMainEditor.Select(start, length);
            richTextBoxMainEditor.ScrollToCaret();
            richTextBoxMainEditor.Focus();
            return true;
        }

        private bool prepareWorkspaceDocumentSwitch(string targetPath)
        {
            return true;
        }

        private static bool isWorkspaceException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is ArgumentException || exception is NotSupportedException ||
                   exception is System.Security.SecurityException;
        }
    }
}
