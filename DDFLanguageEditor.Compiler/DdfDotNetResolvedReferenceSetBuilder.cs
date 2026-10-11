using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetResolvedReferenceSetResult
    {
        internal DdfDotNetResolvedReferenceSetResult(
            DdfDotNetReferenceSet referenceSet,
            IEnumerable<string> managedReferencePaths,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            ReferenceSet = referenceSet;
            ManagedReferencePaths = (managedReferencePaths ??
                Array.Empty<string>()).ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public DdfDotNetReferenceSet ReferenceSet { get; }
        public IReadOnlyList<string> ManagedReferencePaths { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => ReferenceSet != null && Issues.Count == 0;
    }

    /// <summary>
    /// Converts the managed-reference receipts produced by Core into the exact
    /// Roslyn reference set. Every file is checked again immediately before it
    /// is handed to the compiler, closing the gap between resolution and use.
    /// </summary>
    public static class DdfDotNetResolvedReferenceSetBuilder
    {
        public static DdfDotNetResolvedReferenceSetResult Create(
            string referencePackDirectory, string runtimeAssemblyPath,
            DdfBuildResult preparation)
        {
            var issues = new List<DdfDotNetContractIssue>();
            var receipts = new List<Receipt>();
            if (preparation == null || !preparation.Success)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC415",
                    "La preparazione Core valida è obbligatoria prima di " +
                    "costruire i riferimenti .NET."));
                return Failure(issues);
            }

            foreach (DdfResolvedExternalReference reference in preparation
                         .ResolvedExternalReferences.Where(value =>
                             value.BindingKind ==
                             DdfExternalReferenceBindingKind.ManagedAssembly)
                         .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                receipts.Add(new Receipt(reference.Id, reference.AssemblyPath,
                    reference.Sha256, reference.Size,
                    reference.DeclarationDocumentPath,
                    reference.DeclarationLine, reference.DeclarationColumn));
                receipts.AddRange(reference.Dependencies.Select(dependency =>
                    new Receipt(reference.Id + "/" + dependency.Id,
                        dependency.Path, dependency.Sha256, dependency.Size,
                        reference.DeclarationDocumentPath,
                        reference.DeclarationLine,
                        reference.DeclarationColumn)));
            }

            var paths = new List<string>();
            var pinnedByPath = new Dictionary<string, Receipt>(PathComparer());
            foreach (Receipt receipt in receipts.OrderBy(value => value.Id,
                         StringComparer.Ordinal))
            {
                if (!TryValidateReceipt(receipt, out string fullPath,
                        out string message))
                {
                    issues.Add(ReferenceIssue("DDFC415", message, receipt));
                    continue;
                }

                if (pinnedByPath.TryGetValue(fullPath,
                        out Receipt existing))
                {
                    if (existing.Size != receipt.Size || !string.Equals(
                            existing.Sha256, receipt.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                        issues.Add(ReferenceIssue("DDFC415",
                            "Ricevute incompatibili per lo stesso assembly " +
                            "gestito: " + receipt.Id + ".", receipt));
                    continue;
                }

                pinnedByPath.Add(fullPath, receipt);
                paths.Add(fullPath);
            }

            if (issues.Count != 0) return Failure(issues, paths);
            try
            {
                DdfDotNetReferenceSet referenceSet =
                    DdfDotNetReferencePackResolver.Resolve(
                        referencePackDirectory, runtimeAssemblyPath, paths);
                return new DdfDotNetResolvedReferenceSetResult(referenceSet,
                    paths, Array.Empty<DdfDotNetContractIssue>());
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                Receipt location = receipts.FirstOrDefault();
                issues.Add(location == null
                    ? new DdfDotNetContractIssue("DDFC416",
                    "Impossibile costruire il reference set .NET verificato: " +
                    exception.Message)
                    : ReferenceIssue("DDFC416",
                        "Impossibile costruire il reference set .NET " +
                        "verificato: " + exception.Message, location));
                return Failure(issues, paths);
            }
        }

        private static bool TryValidateReceipt(Receipt receipt,
            out string fullPath, out string message)
        {
            fullPath = string.Empty;
            message = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(receipt.Path) ||
                    !Path.IsPathRooted(receipt.Path) ||
                    !string.Equals(Path.GetExtension(receipt.Path), ".dll",
                        StringComparison.OrdinalIgnoreCase))
                {
                    message = "La ricevuta del riferimento gestito " +
                        receipt.Id + " non contiene un percorso DLL assoluto.";
                    return false;
                }

                fullPath = Path.GetFullPath(receipt.Path);
                if (!File.Exists(fullPath))
                {
                    message = "L'assembly gestito verificato non è più " +
                        "disponibile: " + receipt.Id + ".";
                    return false;
                }

                var info = new FileInfo(fullPath);
                if (receipt.Size < 0 || info.Length != receipt.Size)
                {
                    message = "L'assembly gestito è cambiato dopo la " +
                        "risoluzione (dimensione non corrispondente): " +
                        receipt.Id + ".";
                    return false;
                }

                using FileStream stream = File.OpenRead(fullPath);
                string actualHash = Convert.ToHexString(SHA256.HashData(stream))
                    .ToLowerInvariant();
                if (receipt.Sha256?.Length != 64 || !string.Equals(actualHash,
                        receipt.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    message = "L'assembly gestito è cambiato dopo la " +
                        "risoluzione (hash non corrispondente): " + receipt.Id +
                        ".";
                    return false;
                }
                return true;
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                message = "Impossibile verificare l'assembly gestito " +
                    receipt.Id + ": " + exception.Message;
                return false;
            }
        }

        private static DdfDotNetResolvedReferenceSetResult Failure(
            IEnumerable<DdfDotNetContractIssue> issues,
            IEnumerable<string> paths = null) => new(null, paths, issues);

        private static StringComparer PathComparer() => OperatingSystem
            .IsWindows() ? StringComparer.OrdinalIgnoreCase :
            StringComparer.Ordinal;

        private static DdfDotNetContractIssue ReferenceIssue(string code,
            string message, Receipt receipt) =>
            string.IsNullOrWhiteSpace(receipt.DeclarationDocumentPath)
                ? new DdfDotNetContractIssue(code, message)
                : new DdfDotNetContractIssue(code, message,
                    receipt.DeclarationDocumentPath,
                    receipt.DeclarationLine, receipt.DeclarationColumn);

        private sealed record Receipt(string Id, string Path, string Sha256,
            long Size, string DeclarationDocumentPath, int DeclarationLine,
            int DeclarationColumn);
    }
}
