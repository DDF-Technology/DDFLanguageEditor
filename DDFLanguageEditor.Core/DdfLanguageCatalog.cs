namespace DDFLanguageEditor.Core
{
    public sealed class DdfLanguageCatalogSnapshot
    {
        internal DdfLanguageCatalogSnapshot(DdfLanguagePackage package, long revision)
        {
            Package = package;
            Revision = revision;
        }

        public DdfLanguagePackage Package { get; }
        public DdfLanguageDefinition Language => Package.Language;
        public long Revision { get; }
    }

    public static class DdfLanguageCatalog
    {
        private static readonly object SyncRoot = new object();
        private static DdfLanguagePackageLoadResult active = DdfLanguagePackageLoader.LoadWithEmbeddedFallback(
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "language-packages", "ddf"));
        private static long revision = 1;

        public static DdfLanguagePackage DefaultPackage
        {
            get
            {
                lock (SyncRoot)
                {
                    return active.Package ??
                        throw new System.InvalidOperationException("Nessun pacchetto DDF valido disponibile.");
                }
            }
        }
        public static DdfLanguageDefinition Default => DefaultPackage.Language;
        public static System.Collections.Generic.IReadOnlyList<DdfLanguagePackageDiagnostic> LoadDiagnostics
        {
            get { lock (SyncRoot) return active.Diagnostics; }
        }
        public static long Revision { get { lock (SyncRoot) return revision; } }

        public static DdfLanguageCatalogSnapshot Capture()
        {
            lock (SyncRoot)
            {
                DdfLanguagePackage package = active.Package ??
                    throw new System.InvalidOperationException("Nessun pacchetto DDF valido disponibile.");
                return new DdfLanguageCatalogSnapshot(package, revision);
            }
        }

        public static DdfLanguagePackageLoadResult TryActivate(string path)
        {
            DdfLanguagePackageLoadResult candidate = DdfLanguagePackageLoader.Load(path);
            if (candidate.Success)
            {
                lock (SyncRoot)
                {
                    active = candidate;
                    revision++;
                }
            }
            return candidate;
        }

        public static DdfLanguagePackageLoadResult ActivateEmbedded()
        {
            DdfLanguagePackageLoadResult candidate = DdfLanguagePackageLoader.LoadEmbeddedDefault();
            if (candidate.Success)
            {
                lock (SyncRoot)
                {
                    active = candidate;
                    revision++;
                }
            }
            return candidate;
        }
    }
}
