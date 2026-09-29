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

        private void initializeModulePackageCommands()
        {
            if (embeddedHostMode) return;
            modulesMenuItem = new ToolStripMenuItem("&Moduli")
            {
                Name = "modulesMenuItem"
            };
            manageModulePackagesMenuItem = new ToolStripMenuItem("Gestisci moduli progetto...")
            {
                Name = "manageModulePackagesMenuItem"
            };
            manageModulePackagesMenuItem.Click += manageModulePackagesMenuItem_Click;
            modulesMenuItem.DropDownItems.Add(manageModulePackagesMenuItem);
            int runIndex = menuStripMain.Items.IndexOf(runMenuItem);
            menuStripMain.Items.Insert(runIndex < 0 ? menuStripMain.Items.Count : runIndex, modulesMenuItem);

            toolbarModuleManagerButton = createToolbarButton("toolbarModuleManagerButton", "\uE8D5",
                "Gestisci moduli del progetto", manageModulePackagesMenuItem_Click);
            toolStripMain.Items.Add(toolbarModuleManagerButton);
        }

        private void manageModulePackagesMenuItem_Click(object sender, EventArgs e)
        {
            string root = workspaceIndex?.RootPath;
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
            modulePackageManagerForm = new ModulePackageManagerForm(new DdfModulePackageStore(root));
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
