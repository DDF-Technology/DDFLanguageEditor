using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private TabPage hostApiTabPage;
        private TreeView hostApiTreeView;
        private ImageList hostApiImages;
        private string embeddedHostDisplayName = "Applicazione host";

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EmbeddedHostDisplayName
        {
            get => embeddedHostDisplayName;
            set
            {
                embeddedHostDisplayName = string.IsNullOrWhiteSpace(value)
                    ? "Applicazione host" : value.Trim();
                refreshHostApiExplorer(panelEditor?.HostCatalog?.Snapshot);
            }
        }

        private void initializeHostApiExplorer()
        {
            if (!embeddedHostMode) return;
            hostApiImages = createHostApiImages();
            hostApiTreeView = new TreeView
            {
                Name = "hostApiTreeView",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9F),
                FullRowSelect = true,
                HideSelection = false,
                ImageList = hostApiImages,
                ShowNodeToolTips = true
            };
            hostApiTreeView.NodeMouseDoubleClick += hostApiTreeView_NodeMouseDoubleClick;
            hostApiTabPage = new TabPage("API host")
            {
                Name = "hostApiTabPage",
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Text,
                Padding = new Padding(4)
            };
            hostApiTabPage.Controls.Add(hostApiTreeView);
            navigationTabs.TabPages.Add(hostApiTabPage);
            panelEditor.HostCatalogChanged += panelEditor_HostCatalogChanged;
            refreshHostApiExplorer(panelEditor.HostCatalog?.Snapshot);
        }

        private void panelEditor_HostCatalogChanged(object sender,
            DdfHostCatalogChangedEventArgs e) => refreshHostApiExplorer(e?.Snapshot);

        private void refreshHostApiExplorer(DdfHostCatalogSnapshot catalog)
        {
            if (!embeddedHostMode || hostApiTreeView == null || hostApiTreeView.IsDisposed)
                return;
            catalog = catalog ?? panelEditor?.HostCatalog?.Snapshot;
            hostApiTreeView.BeginUpdate();
            try
            {
                hostApiTreeView.Nodes.Clear();
                var root = node(embeddedHostDisplayName, "project",
                    "Elementi esplicitamente esposti dall'applicazione agli script DDF.");
                hostApiTreeView.Nodes.Add(root);
                if (catalog == null)
                {
                    root.Nodes.Add(node("Nessuna API esposta", "info", string.Empty));
                    root.Expand();
                    return;
                }

                TreeNode objects = node("Oggetti e controlli", "object",
                    "Istanze accessibili dallo script.");
                foreach (DdfHostObjectDefinition item in catalog.Objects.OrderBy(item => item.Name,
                             StringComparer.CurrentCultureIgnoreCase))
                {
                    DdfHostTypeDefinition type = catalog.Types.FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, item.TypeName, StringComparison.Ordinal));
                    string icon = controlIcon(item.TypeName);
                    TreeNode objectNode = node(item.Name + " : " + item.TypeName, icon,
                        item.Documentation, item.Name);
                    if (type != null)
                    {
                        foreach (DdfHostFieldDefinition field in type.Fields)
                        {
                            string access = field.CanWrite ? "lettura/scrittura" : "sola lettura";
                            string label = field.Name + " : " + field.TypeName + "  [" + access + "]";
                            objectNode.Nodes.Add(node(label,
                                field.MemberKind == DdfHostMemberKind.Property ? "property" : "field",
                                field.Documentation, item.Name + "." + field.Name));
                        }
                        foreach (DdfHostEventDefinition hostEvent in type.Events)
                        {
                            string signature = string.IsNullOrWhiteSpace(hostEvent.HandlerSignature)
                                ? string.Empty : " : " + hostEvent.HandlerSignature;
                            objectNode.Nodes.Add(node(hostEvent.Name + signature, "event",
                                hostEvent.Documentation));
                        }
                    }
                    objects.Nodes.Add(objectNode);
                }
                if (objects.Nodes.Count > 0) root.Nodes.Add(objects);

                TreeNode functions = node("Funzioni", "function", "Funzioni globali richiamabili.");
                var groupedFunctionNames = new HashSet<string>(
                    catalog.FunctionLibraries.SelectMany(item => item.Functions).Select(item => item.Name)
                        .Concat(catalog.Databases.SelectMany(item => item.Operations)
                            .Select(item => item.FunctionName)), StringComparer.Ordinal);
                foreach (DdfHostFunctionDefinition item in catalog.Functions
                             .Where(item => !groupedFunctionNames.Contains(item.Name))
                             .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                    functions.Nodes.Add(node(item.Signature, "function", item.Documentation,
                        item.Name + "()"));
                if (functions.Nodes.Count > 0) root.Nodes.Add(functions);

                TreeNode libraries = node("Librerie", "library", "Librerie fornite dall'host.");
                foreach (DdfHostFunctionLibraryDefinition library in catalog.FunctionLibraries)
                {
                    TreeNode libraryNode = node(library.DisplayName, "library", library.Documentation);
                    foreach (DdfHostFunctionDefinition function in library.Functions)
                        libraryNode.Nodes.Add(node(function.Signature, "function",
                            function.Documentation, function.Name + "()"));
                    libraries.Nodes.Add(libraryNode);
                }
                if (libraries.Nodes.Count > 0) root.Nodes.Add(libraries);

                TreeNode databases = node("Database", "database", "Servizi dati esposti dall'host.");
                foreach (DdfHostDatabaseDefinition database in catalog.Databases)
                {
                    TreeNode databaseNode = node(database.Id, "database", database.Documentation);
                    foreach (DdfHostDatabaseOperationDefinition operation in database.Operations)
                    {
                        string signature = operation.FunctionName + "(" + string.Join(", ",
                            operation.Parameters.Select(parameter => parameter.TypeName + " " + parameter.Name)) +
                            ") out " + operation.ReturnType;
                        databaseNode.Nodes.Add(node(signature,
                            operation.Kind == DdfHostDatabaseOperationKind.Query ? "query" : "command",
                            operation.Documentation, operation.FunctionName + "()"));
                    }
                    databases.Nodes.Add(databaseNode);
                }
                if (databases.Nodes.Count > 0) root.Nodes.Add(databases);

                TreeNode types = node("Classi e tipi", "type", "Tipi dati dichiarati dall'host.");
                foreach (DdfHostTypeDefinition type in catalog.Types.OrderBy(item => item.Name,
                             StringComparer.CurrentCultureIgnoreCase))
                {
                    TreeNode typeNode = node(type.Name, "type", type.Documentation, type.Name);
                    foreach (DdfHostFieldDefinition field in type.Fields)
                        typeNode.Nodes.Add(node(field.Name + " : " + field.TypeName,
                            field.MemberKind == DdfHostMemberKind.Property ? "property" : "field",
                            field.Documentation));
                    foreach (DdfHostEventDefinition hostEvent in type.Events)
                        typeNode.Nodes.Add(node(hostEvent.Name, "event", hostEvent.Documentation));
                    types.Nodes.Add(typeNode);
                }
                if (types.Nodes.Count > 0) root.Nodes.Add(types);

                TreeNode permissions = node("Permessi", "permission",
                    "Autorizzazioni concesse allo script.");
                foreach (DdfHostPermissionDefinition permission in catalog.Permissions)
                {
                    bool granted = catalog.IsGranted(permission.Id);
                    permissions.Nodes.Add(node((granted ? "Concesso — " : "Negato — ") +
                        permission.DisplayName, granted ? "granted" : "denied",
                        permission.Documentation));
                }
                if (permissions.Nodes.Count > 0) root.Nodes.Add(permissions);
                root.Expand();
                objects.Expand();
            }
            finally
            {
                hostApiTreeView.EndUpdate();
            }
        }

        private void hostApiTreeView_NodeMouseDoubleClick(object sender,
            TreeNodeMouseClickEventArgs e)
        {
            if (!(e.Node?.Tag is string insertion) || string.IsNullOrWhiteSpace(insertion) ||
                richTextBoxMainEditor == null || richTextBoxMainEditor.ReadOnly) return;
            richTextBoxMainEditor.SelectedText = insertion;
            richTextBoxMainEditor.Focus();
        }

        private static TreeNode node(string text, string imageKey, string toolTip,
            string insertion = null) => new TreeNode(text ?? string.Empty)
        {
            ImageKey = imageKey,
            SelectedImageKey = imageKey,
            ToolTipText = toolTip ?? string.Empty,
            Tag = insertion
        };

        private static string controlIcon(string typeName)
        {
            if ((typeName ?? string.Empty).IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0)
                return "textbox";
            if ((typeName ?? string.Empty).IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0)
                return "button";
            if ((typeName ?? string.Empty).IndexOf("CheckBox", StringComparison.OrdinalIgnoreCase) >= 0)
                return "checkbox";
            return "object";
        }

        private static ImageList createHostApiImages()
        {
            var images = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            addHostApiImage(images, "project", "P", Color.FromArgb(0, 120, 215));
            addHostApiImage(images, "object", "O", Color.FromArgb(96, 82, 164));
            addHostApiImage(images, "textbox", "T", Color.FromArgb(0, 120, 140));
            addHostApiImage(images, "button", "B", Color.FromArgb(0, 135, 90));
            addHostApiImage(images, "checkbox", "✓", Color.FromArgb(0, 135, 90));
            addHostApiImage(images, "property", "◆", Color.FromArgb(180, 110, 0));
            addHostApiImage(images, "field", "▪", Color.FromArgb(180, 110, 0));
            addHostApiImage(images, "event", "⚡", Color.FromArgb(195, 55, 55));
            addHostApiImage(images, "function", "ƒ", Color.FromArgb(115, 70, 170));
            addHostApiImage(images, "library", "L", Color.FromArgb(80, 90, 105));
            addHostApiImage(images, "database", "D", Color.FromArgb(45, 110, 155));
            addHostApiImage(images, "query", "?", Color.FromArgb(45, 110, 155));
            addHostApiImage(images, "command", "!", Color.FromArgb(195, 80, 35));
            addHostApiImage(images, "type", "C", Color.FromArgb(85, 100, 170));
            addHostApiImage(images, "permission", "K", Color.FromArgb(90, 100, 110));
            addHostApiImage(images, "granted", "✓", Color.FromArgb(35, 145, 70));
            addHostApiImage(images, "denied", "×", Color.FromArgb(190, 55, 55));
            addHostApiImage(images, "info", "i", Color.FromArgb(90, 100, 110));
            return images;
        }

        private static void addHostApiImage(ImageList images, string key, string glyph, Color color)
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(color))
            using (var font = new Font("Segoe UI Symbol", 8F, FontStyle.Bold, GraphicsUnit.Point))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(brush, 1, 1, 14, 14);
                TextRenderer.DrawText(graphics, glyph, font, new Rectangle(1, 0, 14, 16),
                    Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }
            images.Images.Add(key, bitmap);
        }
    }
}
