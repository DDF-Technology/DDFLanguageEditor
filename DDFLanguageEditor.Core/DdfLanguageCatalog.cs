namespace DDFLanguageEditor.Core
{
    public static class DdfLanguageCatalog
    {
        private static readonly DdfLanguagePackageLoadResult active = DdfLanguagePackageLoader.LoadWithEmbeddedFallback(
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "language-packages", "ddf"));

        public static DdfLanguagePackage DefaultPackage => active.Package ??
            throw new System.InvalidOperationException("Nessun pacchetto DDF valido disponibile.");
        public static DdfLanguageDefinition Default => DefaultPackage.Language;
        public static System.Collections.Generic.IReadOnlyList<DdfLanguagePackageDiagnostic> LoadDiagnostics => active.Diagnostics;
    }
}
