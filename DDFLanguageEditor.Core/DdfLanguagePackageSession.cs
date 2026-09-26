using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public interface IDdfLanguagePackageSession
    {
        DdfLanguagePackage ActivePackage { get; }
        IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        long Revision { get; }
        event EventHandler<DdfLanguagePackageChangedEventArgs> ActiveLanguageChanged;

        DdfLanguagePackageLoadResult TryActivate(string path);
        DdfLanguagePackageLoadResult UseEmbedded();
        void UsePackage(DdfLanguagePackage package,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics = null);
    }

    public sealed class DdfLanguagePackageChangedEventArgs : EventArgs
    {
        public DdfLanguagePackageChangedEventArgs(DdfLanguagePackage package, long revision)
        {
            Package = package;
            Revision = revision;
        }

        public DdfLanguagePackage Package { get; }
        public long Revision { get; }
    }

    /// <summary>
    /// Atomic language selection scoped to one editor host. Unlike the legacy
    /// process catalog, activating a package here never affects another instance.
    /// </summary>
    public sealed class DdfLanguagePackageSession : IDdfLanguagePackageSession
    {
        private static readonly Lazy<DdfLanguagePackageLoadResult> EmbeddedPackage =
            new Lazy<DdfLanguagePackageLoadResult>(DdfLanguagePackageLoader.LoadEmbeddedDefault, true);

        public DdfLanguagePackageSession(DdfLanguagePackage initialPackage = null)
        {
            DdfLanguagePackageLoadResult embedded = initialPackage == null
                ? EmbeddedPackage.Value
                : null;
            ActivePackage = initialPackage ?? embedded?.Package ??
                throw new InvalidOperationException("Nessun pacchetto linguaggio valido disponibile.");
            Diagnostics = embedded?.Diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>();
            Revision = 1;
        }

        public DdfLanguagePackage ActivePackage { get; private set; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; private set; }
        public long Revision { get; private set; }

        public event EventHandler<DdfLanguagePackageChangedEventArgs> ActiveLanguageChanged;

        public DdfLanguagePackageLoadResult TryActivate(string path)
        {
            DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(path);
            if (result.Success) UsePackage(result.Package, result.Diagnostics);
            else Diagnostics = result.Diagnostics;
            return result;
        }

        public DdfLanguagePackageLoadResult UseEmbedded()
        {
            DdfLanguagePackageLoadResult result = EmbeddedPackage.Value;
            if (result.Success) UsePackage(result.Package, result.Diagnostics);
            else Diagnostics = result.Diagnostics;
            return result;
        }

        public void UsePackage(DdfLanguagePackage package,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics = null)
        {
            ActivePackage = package ?? throw new ArgumentNullException(nameof(package));
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>()).ToList().AsReadOnly();
            Revision++;
            ActiveLanguageChanged?.Invoke(this,
                new DdfLanguagePackageChangedEventArgs(ActivePackage, Revision));
        }
    }
}
