using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private TabPage tabPageVariables;
        private TreeView treeViewVariables;
        private Label variablesStatusLabel;

        private void initializeVariables()
        {
            tabPageVariables = new TabPage("Variabili")
            {
                Name = "tabPageVariables",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(0)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppTheme.Surface
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            variablesStatusLabel = new Label
            {
                Name = "variablesStatusLabel",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 4, 6, 4),
                Text = "Debug inattivo — in attesa di una pausa."
            };
            treeViewVariables = new TreeView
            {
                Name = "treeViewVariables",
                AccessibleName = "Variabili locali del debugger",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                HideSelection = false,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };
            treeViewVariables.KeyDown += treeViewVariables_KeyDown;
            layout.Controls.Add(variablesStatusLabel, 0, 0);
            layout.Controls.Add(treeViewVariables, 0, 1);
            tabPageVariables.Controls.Add(layout);
            tabControlBottom.TabPages.Add(tabPageVariables);
        }

        private void treeViewVariables_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.C || treeViewVariables.SelectedNode == null) return;
            Clipboard.SetText(treeViewVariables.SelectedNode.Text);
            e.SuppressKeyPress = true;
        }

        private void updateVariablesExecutionState(bool running, bool paused)
        {
            if (treeViewVariables == null || variablesStatusLabel == null) return;
            variablesStatusLabel.Text = !running ? "Debug inattivo — in attesa di una pausa." : paused ?
                "Valori alla pausa corrente (snapshot in sola lettura)." : watchSnapshot == null ?
                "Esecuzione in corso — in attesa di una pausa." :
                "Esecuzione in corso — valori dell'ultima pausa, non in tempo reale.";
            refreshVariablesTree(running);
        }

        private void refreshVariablesTree(bool running)
        {
            string selectedPath = getVariableNodePath(treeViewVariables.SelectedNode);
            var expandedPaths = new HashSet<string>(treeViewVariables.Nodes.Cast<TreeNode>()
                .SelectMany(flattenVariableNodes).Where(node => node.IsExpanded)
                .Select(getVariableNodePath), StringComparer.Ordinal);
            treeViewVariables.BeginUpdate();
            try
            {
                treeViewVariables.Nodes.Clear();
                if (!running || watchSnapshot == null) return;

                DdfEditorDebugVariableSnapshot[] selectedVariables = getSelectedFrameVariables().ToArray();
                addVariableGroup("Parametri", "parameters", selectedVariables
                    .Where(variable => variable.Kind == DdfRuntimeVariableKind.Parameter));
                addVariableGroup("Variabili locali", "locals", selectedVariables
                    .Where(variable => variable.Kind == DdfRuntimeVariableKind.Local)
                    .OrderBy(variable => variable.ScopeDepth).ThenBy(variable => variable.Name, StringComparer.Ordinal));
                addVariableGroup("Variabili globali", "globals", selectedVariables
                    .Where(variable => variable.Kind == DdfRuntimeVariableKind.Global));

                foreach (TreeNode node in treeViewVariables.Nodes.Cast<TreeNode>().SelectMany(flattenVariableNodes))
                {
                    string path = getVariableNodePath(node);
                    if (node.Parent == null || expandedPaths.Contains(path)) node.Expand();
                    if (path == selectedPath) treeViewVariables.SelectedNode = node;
                }
            }
            finally
            {
                treeViewVariables.EndUpdate();
            }
        }

        private IEnumerable<DdfEditorDebugVariableSnapshot> getSelectedFrameVariables()
        {
            if (watchSnapshot == null) return Enumerable.Empty<DdfEditorDebugVariableSnapshot>();
            IEnumerable<DdfEditorDebugVariableSnapshot> globals = watchSnapshot.Globals;
            if (selectedCallStackFrameIndex >= 0 && selectedCallStackFrameIndex < watchSnapshot.CallStack.Count)
                return watchSnapshot.CallStack[selectedCallStackFrameIndex].Variables.Concat(globals);
            return watchSnapshot.Variables;
        }

        private void addVariableGroup(string title, string key, IEnumerable<DdfEditorDebugVariableSnapshot> variables)
        {
            DdfEditorDebugVariableSnapshot[] items = variables.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            if (items.Length == 0) return;
            var group = new TreeNode(title) { Name = key };
            foreach (DdfEditorDebugVariableSnapshot variable in items) group.Nodes.Add(createVariableNode(variable));
            treeViewVariables.Nodes.Add(group);
        }

        private static TreeNode createVariableNode(DdfEditorDebugVariableSnapshot variable)
        {
            string scope = variable.Kind == DdfRuntimeVariableKind.Local && variable.ScopeDepth > 0
                ? " — scope +" + variable.ScopeDepth
                : string.Empty;
            var node = new TreeNode(variable.Name + " : " + variable.TypeName + " = " + variable.DisplayValue + scope)
            {
                Name = variable.Name,
                Tag = variable
            };
            foreach (DdfEditorDebugVariableSnapshot child in variable.Children) node.Nodes.Add(createVariableNode(child));
            if (variable.IsTruncated) node.Nodes.Add(new TreeNode("… snapshot troncato") { Name = "truncated" });
            return node;
        }

        private static IEnumerable<TreeNode> flattenVariableNodes(TreeNode root)
        {
            yield return root;
            foreach (TreeNode child in root.Nodes)
                foreach (TreeNode descendant in flattenVariableNodes(child)) yield return descendant;
        }

        private static string getVariableNodePath(TreeNode node)
        {
            if (node == null) return null;
            var parts = new Stack<string>();
            for (TreeNode current = node; current != null; current = current.Parent) parts.Push(current.Name);
            return string.Join("/", parts);
        }
    }
}
