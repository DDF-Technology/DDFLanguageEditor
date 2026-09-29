using System;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripMenuItem modulesMenuItem;
        private ToolStripMenuItem manageModulePackagesMenuItem;
        private ModulePackageManagerForm modulePackageManagerForm;
        private DdfEmbeddedModuleStoreCapability embeddedModuleStoreCapability;

        private void initializeModulePackageCommands()
        {
            modulesMenuItem = new ToolStripMenuItem("&Moduli")
            {
                Name = "modulesMenuItem",
                Available = !embeddedHostMode
            };
            manageModulePackagesMenuItem = new ToolStripMenuItem("Gestisci moduli progetto...")
            {
                Name = "manageModulePackagesMenuItem",
                Available = !embeddedHostMode
            };
            manageModulePackagesMenuItem.Click += manageModulePackagesMenuItem_Click;
            modulesMenuItem.DropDownItems.Add(manageModulePackagesMenuItem);
            int runIndex = menuStripMain.Items.IndexOf(runMenuItem);
            menuStripMain.Items.Insert(runIndex < 0 ? menuStripMain.Items.Count : runIndex, modulesMenuItem);

            toolbarModuleManagerButton = createToolbarButton("toolbarModuleManagerButton", "\uE8D5",
                "Gestisci moduli del progetto", manageModulePackagesMenuItem_Click);
            toolbarModuleManagerButton.Available = !embeddedHostMode;
            toolStripMain.Items.Add(toolbarModuleManagerButton);
        }

        internal void SetEmbeddedModuleStoreCapability(DdfEmbeddedModuleStoreCapability capability)
        {
            if (!embeddedHostMode)
                throw new InvalidOperationException(
                    "Le capability dello store moduli appartengono soltanto alla shell embedded.");
            if (ReferenceEquals(embeddedModuleStoreCapability, capability)) return;
            closeModulePackageManager();
            embeddedModuleStoreCapability = capability;
            bool available = capability != null && capability.Allows(
                DdfEmbeddedModuleStoreOperations.BrowseAndValidate);
            if (modulesMenuItem != null) modulesMenuItem.Available = available;
            if (manageModulePackagesMenuItem != null) manageModulePackagesMenuItem.Available = available;
            if (toolbarModuleManagerButton != null) toolbarModuleManagerButton.Available = available;
        }

        private void manageModulePackagesMenuItem_Click(object sender, EventArgs e)
        {
            string root = embeddedHostMode
                ? embeddedModuleStoreCapability?.ProjectRoot
                : workspaceIndex?.RootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                MessageBox.Show(this, "Aprire prima una cartella progetto. I moduli vengono installati nello store del progetto attivo.",
                    "Gestione moduli", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (modulePackageManagerForm != null && !modulePackageManagerForm.IsDisposed &&
                string.Equals(modulePackageManagerForm.ProjectRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                modulePackageManagerForm.Activate();
                return;
            }
            closeModulePackageManager();
            DdfEmbeddedModuleStoreOperations operations = embeddedHostMode
                ? embeddedModuleStoreCapability.Operations
                : DdfEmbeddedModuleStoreOperations.All;
            modulePackageManagerForm = new ModulePackageManagerForm(
                new DdfModulePackageStore(root), operations);
            modulePackageManagerForm.FormClosed += (closedSender, closedArgs) => modulePackageManagerForm = null;
            modulePackageManagerForm.Show(this);
        }

        private void closeModulePackageManager()
        {
            if (modulePackageManagerForm == null) return;
            if (!modulePackageManagerForm.IsDisposed) modulePackageManagerForm.Close();
            modulePackageManagerForm = null;
        }
    }
}
