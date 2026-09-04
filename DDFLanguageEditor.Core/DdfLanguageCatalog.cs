namespace DDFLanguageEditor.Core
{
    public static class DdfLanguageCatalog
    {
        private static volatile DdfLanguagePackageLoadResult active = DdfLanguagePackageLoader.LoadWithEmbeddedFallback(
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "language-packages", "ddf"));

        public static DdfLanguagePackage DefaultPackage => active.Package ??
            throw new System.InvalidOperationException("Nessun pacchetto DDF valido disponibile.");
        public static DdfLanguageDefinition Default => DefaultPackage.Language;
        public static System.Collections.Generic.IReadOnlyList<DdfLanguagePackageDiagnostic> LoadDiagnostics => active.Diagnostics;

        public static DdfLanguagePackageLoadResult TryActivate(string path)
        {
            DdfLanguagePackageLoadResult candidate = DdfLanguagePackageLoader.Load(path);
            if (candidate.Success) active = candidate;
            return candidate;
        }

        public static DdfLanguagePackageLoadResult ActivateEmbedded()
        {
            DdfLanguagePackageLoadResult candidate = DdfLanguagePackageLoader.LoadEmbeddedDefault();
            if (candidate.Success) active = candidate;
            return candidate;
        }
    }
}
