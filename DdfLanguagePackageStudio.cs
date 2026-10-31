using System;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Creates the same Package Studio shell for the editor and for an
    /// independent authoring application. The authoring engine remains in Core.
    /// </summary>
    public static class DdfLanguagePackageStudio
    {
        public static Form CreateForm(DdfLanguagePackage package) =>
            new LanguagePackageStudioForm(package ??
                throw new ArgumentNullException(nameof(package)));

        public static Form CreateForm(DdfLanguageAuthoringProject project,
            DdfLanguagePackage previewPackage = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            DdfLanguagePackage fallback = previewPackage ??
                DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            if (fallback == null)
                throw new InvalidOperationException(
                    "Il pacchetto DDF incorporato non è disponibile.");
            return new LanguagePackageStudioForm(fallback, project);
        }
    }
}
