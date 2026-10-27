using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public enum DdfLanguageAuthoringChangeKind { Upsert, Remove }

    public sealed class DdfLanguageAuthoringDocument
    {
        internal DdfLanguageAuthoringDocument(string relativePath, string content)
        {
            RelativePath = relativePath;
            Content = content;
            ContentHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        }

        public string RelativePath { get; }
        public string Content { get; }
        public string ContentHash { get; }
    }

    public sealed class DdfLanguageAuthoringProject
    {
        private readonly IReadOnlyDictionary<string, DdfLanguageAuthoringDocument>
            documents;

        internal DdfLanguageAuthoringProject(string sourceRoot, long revision,
            IEnumerable<DdfLanguageAuthoringDocument> documents)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            Revision = revision;
            var copy = new SortedDictionary<string, DdfLanguageAuthoringDocument>(
                StringComparer.OrdinalIgnoreCase);
            foreach (DdfLanguageAuthoringDocument document in documents ??
                Array.Empty<DdfLanguageAuthoringDocument>())
            {
                if (copy.ContainsKey(document.RelativePath))
                    throw new ArgumentException("Documento di authoring duplicato: " +
                        document.RelativePath, nameof(documents));
                copy.Add(document.RelativePath, document);
            }
            this.documents = new ReadOnlyDictionary<string,
                DdfLanguageAuthoringDocument>(copy);
            Fingerprint = ComputeFingerprint(copy.Values);
        }

        public string SourceRoot { get; }
        public long Revision { get; }
        public string Fingerprint { get; }
        public IReadOnlyDictionary<string, DdfLanguageAuthoringDocument> Documents =>
            documents;

        public bool TryGetDocument(string relativePath,
            out DdfLanguageAuthoringDocument document)
        {
            document = null;
            return DdfLanguageAuthoringPaths.TryNormalize(relativePath,
                out string normalized) && documents.TryGetValue(normalized, out document);
        }

        private static string ComputeFingerprint(
            IEnumerable<DdfLanguageAuthoringDocument> documents)
        {
            var canonical = new StringBuilder();
            foreach (DdfLanguageAuthoringDocument document in documents.OrderBy(
                item => item.RelativePath, StringComparer.Ordinal))
            {
                canonical.Append(document.RelativePath).Append('\0')
                    .Append(document.ContentHash).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                canonical.ToString()))).ToLowerInvariant();
        }
    }

    public sealed class DdfLanguageAuthoringChange
    {
        private DdfLanguageAuthoringChange(DdfLanguageAuthoringChangeKind kind,
            string relativePath, string content)
        {
            Kind = kind;
            RelativePath = relativePath;
            Content = content;
        }

        public DdfLanguageAuthoringChangeKind Kind { get; }
        public string RelativePath { get; }
        public string Content { get; }

        public static DdfLanguageAuthoringChange Upsert(string relativePath,
            string content) => new DdfLanguageAuthoringChange(
                DdfLanguageAuthoringChangeKind.Upsert, relativePath,
                content ?? throw new ArgumentNullException(nameof(content)));

        public static DdfLanguageAuthoringChange Remove(string relativePath) =>
            new DdfLanguageAuthoringChange(DdfLanguageAuthoringChangeKind.Remove,
                relativePath, null);
    }

    public sealed class DdfLanguageAuthoringChangeSet
    {
        public DdfLanguageAuthoringChangeSet(long expectedRevision,
            IEnumerable<DdfLanguageAuthoringChange> changes, string description = null)
        {
            if (expectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            ExpectedRevision = expectedRevision;
            Changes = (changes ?? throw new ArgumentNullException(nameof(changes)))
                .ToList().AsReadOnly();
            Description = description ?? string.Empty;
        }

        public long ExpectedRevision { get; }
        public IReadOnlyList<DdfLanguageAuthoringChange> Changes { get; }
        public string Description { get; }
    }

    public enum DdfLanguageAuthoringDocumentChangeKind
    {
        Added,
        Modified,
        Removed
    }

    public enum DdfLanguageAuthoringDiffLineKind
    {
        Unchanged,
        Added,
        Removed
    }

    public sealed class DdfLanguageAuthoringDiffLine
    {
        internal DdfLanguageAuthoringDiffLine(DdfLanguageAuthoringDiffLineKind kind,
            int? oldLineNumber, int? newLineNumber, string text)
        {
            Kind = kind;
            OldLineNumber = oldLineNumber;
            NewLineNumber = newLineNumber;
            Text = text ?? string.Empty;
        }

        public DdfLanguageAuthoringDiffLineKind Kind { get; }
        public int? OldLineNumber { get; }
        public int? NewLineNumber { get; }
        public string Text { get; }
    }

    public sealed class DdfLanguageAuthoringDocumentDiff
    {
        internal DdfLanguageAuthoringDocumentDiff(string relativePath,
            DdfLanguageAuthoringDocumentChangeKind kind, string oldContentHash,
            string newContentHash, IEnumerable<DdfLanguageAuthoringDiffLine> lines)
        {
            RelativePath = relativePath;
            Kind = kind;
            OldContentHash = oldContentHash ?? string.Empty;
            NewContentHash = newContentHash ?? string.Empty;
            Lines = (lines ?? Array.Empty<DdfLanguageAuthoringDiffLine>())
                .ToList().AsReadOnly();
        }

        public string RelativePath { get; }
        public DdfLanguageAuthoringDocumentChangeKind Kind { get; }
        public string OldContentHash { get; }
        public string NewContentHash { get; }
        public IReadOnlyList<DdfLanguageAuthoringDiffLine> Lines { get; }
        public int AddedLineCount => Lines.Count(item =>
            item.Kind == DdfLanguageAuthoringDiffLineKind.Added);
        public int RemovedLineCount => Lines.Count(item =>
            item.Kind == DdfLanguageAuthoringDiffLineKind.Removed);
    }

    public interface IDdfLanguageAuthoringTransformation
    {
        string Id { get; }
        string DisplayName { get; }
        DdfLanguageAuthoringChangeSet CreateChangeSet(
            DdfLanguageAuthoringProject project);
    }

    public sealed class DdfLanguageAuthoringPreview
    {
        internal DdfLanguageAuthoringPreview(long baseRevision,
            string baseFingerprint, string transformationId,
            DdfLanguageAuthoringChangeSet changeSet,
            DdfLanguageAuthoringProject resultingProject,
            IEnumerable<DdfLanguageAuthoringDocumentDiff> documents,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            BaseRevision = baseRevision;
            BaseFingerprint = baseFingerprint ?? string.Empty;
            TransformationId = transformationId ?? string.Empty;
            ChangeSet = changeSet;
            ResultingProject = resultingProject;
            Documents = (documents ?? Array.Empty<DdfLanguageAuthoringDocumentDiff>())
                .ToList().AsReadOnly();
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>())
                .ToList().AsReadOnly();
        }

        public long BaseRevision { get; }
        public string BaseFingerprint { get; }
        public string TransformationId { get; }
        public DdfLanguageAuthoringChangeSet ChangeSet { get; }
        public DdfLanguageAuthoringProject ResultingProject { get; }
        public IReadOnlyList<DdfLanguageAuthoringDocumentDiff> Documents { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => ChangeSet != null && ResultingProject != null &&
            !Diagnostics.Any(item => item.Severity ==
                DdfLanguagePackageDiagnosticSeverity.Error);
        public bool HasChanges => Documents.Count != 0;
    }

    public sealed class DdfLanguageAuthoringHistoryEntry
    {
        internal DdfLanguageAuthoringHistoryEntry(string description,
            string beforeFingerprint, string afterFingerprint,
            DdfLanguageAuthoringChangeSet forward,
            DdfLanguageAuthoringChangeSet inverse)
        {
            Description = description ?? string.Empty;
            BeforeFingerprint = beforeFingerprint ?? string.Empty;
            AfterFingerprint = afterFingerprint ?? string.Empty;
            Forward = forward;
            Inverse = inverse;
        }

        public string Description { get; }
        public string BeforeFingerprint { get; }
        public string AfterFingerprint { get; }
        internal DdfLanguageAuthoringChangeSet Forward { get; }
        internal DdfLanguageAuthoringChangeSet Inverse { get; }
    }

    public sealed class DdfLanguageAuthoringSession
    {
        private readonly object sync = new object();
        private readonly int historyLimit;
        private readonly List<DdfLanguageAuthoringHistoryEntry> undo = new();
        private readonly List<DdfLanguageAuthoringHistoryEntry> redo = new();
        private DdfLanguageAuthoringProject project;

        public DdfLanguageAuthoringSession(DdfLanguageAuthoringProject project,
            int historyLimit = 100)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            if (historyLimit < 1 || historyLimit > 1000)
                throw new ArgumentOutOfRangeException(nameof(historyLimit));
            this.historyLimit = historyLimit;
        }

        public DdfLanguageAuthoringProject Project
        {
            get { lock (sync) return project; }
        }

        public bool CanUndo { get { lock (sync) return undo.Count != 0; } }
        public bool CanRedo { get { lock (sync) return redo.Count != 0; } }

        public IReadOnlyList<DdfLanguageAuthoringHistoryEntry> UndoHistory
        {
            get { lock (sync) return undo.ToList().AsReadOnly(); }
        }

        public IReadOnlyList<DdfLanguageAuthoringHistoryEntry> RedoHistory
        {
            get { lock (sync) return redo.ToList().AsReadOnly(); }
        }

        public DdfLanguageAuthoringPreview Preview(
            IDdfLanguageAuthoringTransformation transformation)
        {
            lock (sync)
                return DdfLanguagePackageAuthoring.PreviewTransformation(project,
                    transformation);
        }

        public DdfLanguageAuthoringResult Commit(
            DdfLanguageAuthoringPreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            lock (sync)
            {
                if (!preview.Success || !preview.HasChanges)
                    return SessionFailure("DDFPA025",
                        "L'anteprima non contiene una trasformazione applicabile.");
                DdfLanguageAuthoringProject before = project;
                DdfLanguageAuthoringResult result =
                    DdfLanguagePackageAuthoring.ApplyPreview(before, preview);
                if (!result.Success) return result;

                undo.Add(new DdfLanguageAuthoringHistoryEntry(
                    preview.ChangeSet.Description, before.Fingerprint,
                    result.Project.Fingerprint, preview.ChangeSet, result.Inverse));
                if (undo.Count > historyLimit) undo.RemoveAt(0);
                redo.Clear();
                project = result.Project;
                return result;
            }
        }

        public DdfLanguageAuthoringResult Undo()
        {
            lock (sync)
            {
                if (undo.Count == 0)
                    return SessionFailure("DDFPA026",
                        "Nessuna operazione di authoring da annullare.");
                DdfLanguageAuthoringHistoryEntry entry = undo[^1];
                if (!string.Equals(project.Fingerprint, entry.AfterFingerprint,
                    StringComparison.Ordinal))
                    return SessionFailure("DDFPA027",
                        "Lo snapshot corrente non coincide con lo storico undo.");

                DdfLanguageAuthoringResult result =
                    DdfLanguagePackageAuthoring.Apply(project,
                        Rebase(entry.Inverse, project.Revision));
                if (!result.Success) return result;
                if (!string.Equals(result.Project.Fingerprint,
                    entry.BeforeFingerprint, StringComparison.Ordinal))
                    return SessionFailure("DDFPA028",
                        "Undo non deterministico; lo snapshot non è stato sostituito.");
                undo.RemoveAt(undo.Count - 1);
                redo.Add(entry);
                project = result.Project;
                return result;
            }
        }

        public DdfLanguageAuthoringResult Redo()
        {
            lock (sync)
            {
                if (redo.Count == 0)
                    return SessionFailure("DDFPA029",
                        "Nessuna operazione di authoring da ripristinare.");
                DdfLanguageAuthoringHistoryEntry entry = redo[^1];
                if (!string.Equals(project.Fingerprint, entry.BeforeFingerprint,
                    StringComparison.Ordinal))
                    return SessionFailure("DDFPA030",
                        "Lo snapshot corrente non coincide con lo storico redo.");

                DdfLanguageAuthoringResult result =
                    DdfLanguagePackageAuthoring.Apply(project,
                        Rebase(entry.Forward, project.Revision));
                if (!result.Success) return result;
                if (!string.Equals(result.Project.Fingerprint,
                    entry.AfterFingerprint, StringComparison.Ordinal))
                    return SessionFailure("DDFPA031",
                        "Redo non deterministico; lo snapshot non è stato sostituito.");
                redo.RemoveAt(redo.Count - 1);
                undo.Add(entry);
                if (undo.Count > historyLimit) undo.RemoveAt(0);
                project = result.Project;
                return result;
            }
        }

        private static DdfLanguageAuthoringChangeSet Rebase(
            DdfLanguageAuthoringChangeSet changeSet, long revision) =>
            new DdfLanguageAuthoringChangeSet(revision, changeSet.Changes,
                changeSet.Description);

        private static DdfLanguageAuthoringResult SessionFailure(string code,
            string message) => new DdfLanguageAuthoringResult(null, new[]
            {
                new DdfLanguagePackageDiagnostic(code, message, severity:
                    DdfLanguagePackageDiagnosticSeverity.Error,
                    domain: DdfDiagnosticDomain.LanguageDefinition)
            });
    }

    public sealed class DdfLanguageAuthoringResult
    {
        internal DdfLanguageAuthoringResult(DdfLanguageAuthoringProject project,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics,
            DdfLanguageAuthoringChangeSet inverse = null)
        {
            Project = project;
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>())
                .ToList().AsReadOnly();
            Inverse = inverse;
        }

        public DdfLanguageAuthoringProject Project { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public DdfLanguageAuthoringChangeSet Inverse { get; }
        public bool Success => Project != null && !Diagnostics.Any(item =>
            item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public sealed class DdfLanguageAuthoringGenerationResult
    {
        internal DdfLanguageAuthoringGenerationResult(string outputPath,
            DdfLanguagePackage package,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            OutputPath = outputPath ?? string.Empty;
            Package = package;
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>())
                .ToList().AsReadOnly();
        }

        public string OutputPath { get; }
        public DdfLanguagePackage Package { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => Package != null && !Diagnostics.Any(item =>
            item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public interface IDdfLanguageAuthoringMigration
    {
        string Id { get; }
        string SourceSchemaVersion { get; }
        string TargetSchemaVersion { get; }
        DdfLanguageAuthoringChangeSet CreateChangeSet(
            DdfLanguageAuthoringProject project);
    }

    public sealed class DdfLanguageAuthoringMigrationResult
    {
        internal DdfLanguageAuthoringMigrationResult(
            DdfLanguageAuthoringProject project, IEnumerable<string> appliedMigrationIds,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            Project = project;
            AppliedMigrationIds = (appliedMigrationIds ?? Array.Empty<string>())
                .ToList().AsReadOnly();
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>())
                .ToList().AsReadOnly();
        }

        public DdfLanguageAuthoringProject Project { get; }
        public IReadOnlyList<string> AppliedMigrationIds { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => Project != null && !Diagnostics.Any(item =>
            item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public static class DdfLanguagePackageAuthoring
    {
        private const int MaximumDiffLines = 2000;
        private const int MaximumDiffCells = 2_000_000;

        public static DdfLanguageAuthoringResult LoadDirectory(string directory)
        {
            var diagnostics = new List<DdfLanguagePackageDiagnostic>();
            if (string.IsNullOrWhiteSpace(directory))
                return Failure("DDFPA001", "Directory di authoring non specificata.");
            try
            {
                string root = Path.GetFullPath(directory);
                if (!Directory.Exists(root))
                    return Failure("DDFPA002", "Directory di authoring non trovata.",
                        root);

                var documents = new List<DdfLanguageAuthoringDocument>();
                foreach (string file in EnumerateFilesWithoutLinks(root))
                {
                    string relative = Path.GetRelativePath(root, file)
                        .Replace('\\', '/');
                    if (!DdfLanguageAuthoringPaths.TryNormalize(relative,
                        out string normalized))
                    {
                        diagnostics.Add(Diagnostic("DDFPA003",
                            "Percorso non valido nel progetto di authoring.", relative));
                        continue;
                    }
                    documents.Add(new DdfLanguageAuthoringDocument(normalized,
                        File.ReadAllText(file, Encoding.UTF8)));
                }
                if (diagnostics.Any())
                    return new DdfLanguageAuthoringResult(null, diagnostics);
                return new DdfLanguageAuthoringResult(
                    new DdfLanguageAuthoringProject(root, 0, documents), diagnostics);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                return Failure("DDFPA004", exception.Message, directory);
            }
        }

        public static DdfLanguageAuthoringResult Apply(
            DdfLanguageAuthoringProject project,
            DdfLanguageAuthoringChangeSet changeSet)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (changeSet == null) throw new ArgumentNullException(nameof(changeSet));
            if (changeSet.ExpectedRevision != project.Revision)
                return Failure("DDFPA005",
                    "La revisione del progetto è cambiata; la modifica non è stata applicata.");
            if (changeSet.Changes.Count == 0)
                return Failure("DDFPA006", "La modifica non contiene operazioni.");

            var documents = project.Documents.ToDictionary(item => item.Key,
                item => item.Value, StringComparer.OrdinalIgnoreCase);
            var inverse = new List<DdfLanguageAuthoringChange>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DdfLanguageAuthoringChange change in changeSet.Changes)
            {
                if (change == null || !DdfLanguageAuthoringPaths.TryNormalize(
                    change?.RelativePath, out string path))
                    return Failure("DDFPA007", "Percorso di modifica non valido.",
                        change?.RelativePath);
                if (!seen.Add(path))
                    return Failure("DDFPA008",
                        "Lo stesso documento è modificato più volte nello stesso change set.",
                        path);

                bool existed = documents.TryGetValue(path,
                    out DdfLanguageAuthoringDocument previous);
                if (change.Kind == DdfLanguageAuthoringChangeKind.Remove)
                {
                    if (!existed)
                        return Failure("DDFPA009",
                            "Il documento da rimuovere non esiste.", path);
                    documents.Remove(path);
                    inverse.Add(DdfLanguageAuthoringChange.Upsert(path,
                        previous.Content));
                }
                else
                {
                    documents[path] = new DdfLanguageAuthoringDocument(path,
                        change.Content);
                    inverse.Add(existed
                        ? DdfLanguageAuthoringChange.Upsert(path, previous.Content)
                        : DdfLanguageAuthoringChange.Remove(path));
                }
            }

            var updated = new DdfLanguageAuthoringProject(project.SourceRoot,
                checked(project.Revision + 1), documents.Values);
            inverse.Reverse();
            return new DdfLanguageAuthoringResult(updated,
                Array.Empty<DdfLanguagePackageDiagnostic>(),
                new DdfLanguageAuthoringChangeSet(updated.Revision, inverse,
                    "Annulla: " + changeSet.Description));
        }

        public static DdfLanguageAuthoringPreview Preview(
            DdfLanguageAuthoringProject project,
            DdfLanguageAuthoringChangeSet changeSet)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (changeSet == null) throw new ArgumentNullException(nameof(changeSet));
            DdfLanguageAuthoringResult result = Apply(project, changeSet);
            if (!result.Success)
                return new DdfLanguageAuthoringPreview(project.Revision,
                    project.Fingerprint, string.Empty, changeSet, null,
                    Array.Empty<DdfLanguageAuthoringDocumentDiff>(),
                    result.Diagnostics);
            return new DdfLanguageAuthoringPreview(project.Revision,
                project.Fingerprint, string.Empty, changeSet, result.Project,
                BuildDocumentDiffs(project, result.Project), result.Diagnostics);
        }

        public static DdfLanguageAuthoringPreview PreviewTransformation(
            DdfLanguageAuthoringProject project,
            IDdfLanguageAuthoringTransformation transformation)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (transformation == null)
                throw new ArgumentNullException(nameof(transformation));
            if (string.IsNullOrWhiteSpace(transformation.Id) ||
                string.IsNullOrWhiteSpace(transformation.DisplayName))
                return PreviewFailure(project, "DDFPA021",
                    "La trasformazione deve dichiarare identità e nome.");
            DdfLanguageAuthoringChangeSet changeSet =
                transformation.CreateChangeSet(project);
            if (changeSet == null)
                return PreviewFailure(project, "DDFPA022",
                    "La trasformazione non ha prodotto un change set.");
            DdfLanguageAuthoringPreview preview = Preview(project, changeSet);
            return new DdfLanguageAuthoringPreview(preview.BaseRevision,
                preview.BaseFingerprint, transformation.Id, preview.ChangeSet,
                preview.ResultingProject, preview.Documents, preview.Diagnostics);
        }

        public static DdfLanguageAuthoringResult ApplyPreview(
            DdfLanguageAuthoringProject project,
            DdfLanguageAuthoringPreview preview)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (!preview.Success)
                return Failure("DDFPA023",
                    "L'anteprima contiene errori e non può essere applicata.");
            if (project.Revision != preview.BaseRevision ||
                !string.Equals(project.Fingerprint, preview.BaseFingerprint,
                    StringComparison.Ordinal))
                return Failure("DDFPA024",
                    "Il progetto è cambiato dopo l'anteprima; rigenerare il diff.");
            return Apply(project, preview.ChangeSet);
        }

        public static DdfLanguagePackageLoadResult Validate(
            DdfLanguageAuthoringProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            string temporary = Path.Combine(Path.GetTempPath(),
                "ddf-language-authoring-" + Guid.NewGuid().ToString("N"));
            try
            {
                WriteDocuments(project, temporary);
                return DdfLanguagePackageLoader.Load(temporary);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                return new DdfLanguagePackageLoadResult(null, new[]
                {
                    Diagnostic("DDFPA010", exception.Message, temporary)
                });
            }
            finally
            {
                TryDeleteDirectory(temporary);
            }
        }

        public static DdfLanguagePackageLoadResult Evaluate(
            DdfLanguageAuthoringProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            string temporary = Path.Combine(Path.GetTempPath(),
                "ddf-language-authoring-evaluate-" + Guid.NewGuid().ToString("N"));
            try
            {
                WriteDocuments(project, temporary);
                return DdfLanguagePackageLoader.LoadAuthoringSnapshot(temporary);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                return new DdfLanguagePackageLoadResult(null, new[]
                {
                    Diagnostic("DDFPA025", exception.Message, temporary)
                });
            }
            finally
            {
                TryDeleteDirectory(temporary);
            }
        }

        public static DdfLanguageAuthoringGenerationResult GenerateDirectory(
            DdfLanguageAuthoringProject project, string destination)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(destination))
                return new DdfLanguageAuthoringGenerationResult(string.Empty, null,
                    new[] { Diagnostic("DDFPA011", "Destinazione non specificata.") });

            string target = Path.GetFullPath(destination);
            if (Directory.Exists(target) || File.Exists(target))
                return new DdfLanguageAuthoringGenerationResult(target, null,
                    new[] { Diagnostic("DDFPA012",
                        "La destinazione esiste già e non verrà sovrascritta.", target) });
            string parent = Path.GetDirectoryName(target);
            string staging = target + ".generating-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(parent);
                WriteDocuments(project, staging);
                DdfLanguagePackageLoadResult validation =
                    DdfLanguagePackageLoader.Load(staging);
                if (!validation.Success)
                    return new DdfLanguageAuthoringGenerationResult(target, null,
                        validation.Diagnostics);
                Directory.Move(staging, target);
                return new DdfLanguageAuthoringGenerationResult(target,
                    validation.Package, validation.Diagnostics);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                return new DdfLanguageAuthoringGenerationResult(target, null,
                    new[] { Diagnostic("DDFPA013", exception.Message, target) });
            }
            finally
            {
                TryDeleteDirectory(staging);
            }
        }

        public static DdfLanguageAuthoringMigrationResult Migrate(
            DdfLanguageAuthoringProject project, string targetSchemaVersion,
            IEnumerable<IDdfLanguageAuthoringMigration> migrations)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(targetSchemaVersion))
                throw new ArgumentException("Versione destinazione richiesta.",
                    nameof(targetSchemaVersion));

            if (!TryReadSchemaVersion(project, out string current,
                out DdfLanguagePackageDiagnostic manifestDiagnostic))
                return new DdfLanguageAuthoringMigrationResult(null,
                    Array.Empty<string>(), new[] { manifestDiagnostic });

            var available = (migrations ?? Array.Empty<IDdfLanguageAuthoringMigration>())
                .Where(item => item != null).ToList();
            var applied = new List<string>();
            DdfLanguageAuthoringProject working = project;
            var visited = new HashSet<string>(StringComparer.Ordinal) { current };
            while (!string.Equals(current, targetSchemaVersion,
                StringComparison.Ordinal))
            {
                List<IDdfLanguageAuthoringMigration> candidates = available
                    .Where(item => string.Equals(item.SourceSchemaVersion, current,
                        StringComparison.Ordinal)).ToList();
                if (candidates.Count != 1)
                    return MigrationFailure("DDFPA014",
                        candidates.Count == 0
                            ? "Nessuna migrazione disponibile da schema " + current + "."
                            : "Percorso di migrazione ambiguo da schema " + current + ".",
                        applied);
                IDdfLanguageAuthoringMigration migration = candidates[0];
                if (string.IsNullOrWhiteSpace(migration.Id) ||
                    string.IsNullOrWhiteSpace(migration.TargetSchemaVersion) ||
                    !visited.Add(migration.TargetSchemaVersion))
                    return MigrationFailure("DDFPA015",
                        "Migrazione non valida o ciclica: " + migration.Id + ".", applied);

                DdfLanguageAuthoringChangeSet changes =
                    migration.CreateChangeSet(working);
                if (changes == null || changes.ExpectedRevision != working.Revision)
                    return MigrationFailure("DDFPA016",
                        "La migrazione non è correlata alla revisione corrente: " +
                        migration.Id + ".", applied);
                DdfLanguageAuthoringResult result = Apply(working, changes);
                if (!result.Success)
                    return new DdfLanguageAuthoringMigrationResult(null, applied,
                        result.Diagnostics);
                working = result.Project;
                if (!TryReadSchemaVersion(working, out string declaredTarget,
                    out DdfLanguagePackageDiagnostic targetDiagnostic))
                    return new DdfLanguageAuthoringMigrationResult(null, applied,
                        new[] { targetDiagnostic });
                if (!string.Equals(declaredTarget,
                    migration.TargetSchemaVersion, StringComparison.Ordinal))
                    return MigrationFailure("DDFPA020",
                        "La migrazione " + migration.Id +
                        " non ha dichiarato lo schema destinazione atteso.", applied);
                current = migration.TargetSchemaVersion;
                applied.Add(migration.Id);
            }
            return new DdfLanguageAuthoringMigrationResult(working, applied,
                Array.Empty<DdfLanguagePackageDiagnostic>());
        }

        private static bool TryReadSchemaVersion(DdfLanguageAuthoringProject project,
            out string schemaVersion, out DdfLanguagePackageDiagnostic diagnostic)
        {
            schemaVersion = null;
            diagnostic = null;
            if (!project.TryGetDocument("manifest.json", out var manifest))
            {
                diagnostic = Diagnostic("DDFPA017",
                    "manifest.json non è presente nel progetto.", "manifest.json");
                return false;
            }
            try
            {
                using JsonDocument json = JsonDocument.Parse(manifest.Content,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                if (!json.RootElement.TryGetProperty("schemaVersion", out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                {
                    diagnostic = Diagnostic("DDFPA018",
                        "schemaVersion non è dichiarata nel manifest.",
                        "manifest.json", "$.schemaVersion");
                    return false;
                }
                schemaVersion = value.GetString();
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = Diagnostic("DDFPA019", exception.Message,
                    "manifest.json");
                return false;
            }
        }

        private static IReadOnlyList<DdfLanguageAuthoringDocumentDiff>
            BuildDocumentDiffs(DdfLanguageAuthoringProject before,
                DdfLanguageAuthoringProject after)
        {
            var paths = new SortedSet<string>(before.Documents.Keys,
                StringComparer.OrdinalIgnoreCase);
            paths.UnionWith(after.Documents.Keys);
            var result = new List<DdfLanguageAuthoringDocumentDiff>();
            foreach (string path in paths)
            {
                before.Documents.TryGetValue(path, out var oldDocument);
                after.Documents.TryGetValue(path, out var newDocument);
                if (oldDocument != null && newDocument != null &&
                    string.Equals(oldDocument.ContentHash, newDocument.ContentHash,
                        StringComparison.Ordinal))
                    continue;
                var kind = oldDocument == null
                    ? DdfLanguageAuthoringDocumentChangeKind.Added
                    : newDocument == null
                        ? DdfLanguageAuthoringDocumentChangeKind.Removed
                        : DdfLanguageAuthoringDocumentChangeKind.Modified;
                result.Add(new DdfLanguageAuthoringDocumentDiff(path, kind,
                    oldDocument?.ContentHash, newDocument?.ContentHash,
                    BuildLineDiff(oldDocument?.Content ?? string.Empty,
                        newDocument?.Content ?? string.Empty)));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<DdfLanguageAuthoringDiffLine> BuildLineDiff(
            string oldContent, string newContent)
        {
            string[] oldLines = SplitLines(oldContent);
            string[] newLines = SplitLines(newContent);
            if (oldLines.Length > MaximumDiffLines ||
                newLines.Length > MaximumDiffLines ||
                (long)oldLines.Length * newLines.Length > MaximumDiffCells)
                return BuildCoarseLineDiff(oldLines, newLines);

            var lcs = new int[oldLines.Length + 1, newLines.Length + 1];
            for (int oldIndex = oldLines.Length - 1; oldIndex >= 0; oldIndex--)
            {
                for (int newIndex = newLines.Length - 1;
                    newIndex >= 0; newIndex--)
                {
                    lcs[oldIndex, newIndex] = string.Equals(oldLines[oldIndex],
                        newLines[newIndex], StringComparison.Ordinal)
                        ? lcs[oldIndex + 1, newIndex + 1] + 1
                        : Math.Max(lcs[oldIndex + 1, newIndex],
                            lcs[oldIndex, newIndex + 1]);
                }
            }

            var result = new List<DdfLanguageAuthoringDiffLine>();
            int oldCursor = 0;
            int newCursor = 0;
            while (oldCursor < oldLines.Length || newCursor < newLines.Length)
            {
                if (oldCursor < oldLines.Length && newCursor < newLines.Length &&
                    string.Equals(oldLines[oldCursor], newLines[newCursor],
                        StringComparison.Ordinal))
                {
                    result.Add(new DdfLanguageAuthoringDiffLine(
                        DdfLanguageAuthoringDiffLineKind.Unchanged,
                        oldCursor + 1, newCursor + 1, oldLines[oldCursor]));
                    oldCursor++;
                    newCursor++;
                }
                else if (newCursor < newLines.Length &&
                    (oldCursor == oldLines.Length ||
                     lcs[oldCursor, newCursor + 1] >=
                     lcs[oldCursor + 1, newCursor]))
                {
                    result.Add(new DdfLanguageAuthoringDiffLine(
                        DdfLanguageAuthoringDiffLineKind.Added, null,
                        newCursor + 1, newLines[newCursor++]));
                }
                else
                {
                    result.Add(new DdfLanguageAuthoringDiffLine(
                        DdfLanguageAuthoringDiffLineKind.Removed,
                        oldCursor + 1, null, oldLines[oldCursor++]));
                }
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<DdfLanguageAuthoringDiffLine>
            BuildCoarseLineDiff(string[] oldLines, string[] newLines)
        {
            var result = new List<DdfLanguageAuthoringDiffLine>();
            for (int index = 0; index < oldLines.Length; index++)
                result.Add(new DdfLanguageAuthoringDiffLine(
                    DdfLanguageAuthoringDiffLineKind.Removed, index + 1, null,
                    oldLines[index]));
            for (int index = 0; index < newLines.Length; index++)
                result.Add(new DdfLanguageAuthoringDiffLine(
                    DdfLanguageAuthoringDiffLineKind.Added, null, index + 1,
                    newLines[index]));
            return result.AsReadOnly();
        }

        private static string[] SplitLines(string content) =>
            content.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n').Split('\n');

        private static DdfLanguageAuthoringPreview PreviewFailure(
            DdfLanguageAuthoringProject project, string code, string message) =>
            new DdfLanguageAuthoringPreview(project.Revision,
                project.Fingerprint, string.Empty, null, null,
                Array.Empty<DdfLanguageAuthoringDocumentDiff>(),
                new[] { Diagnostic(code, message) });

        private static IEnumerable<string> EnumerateFilesWithoutLinks(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                string directory = pending.Pop();
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Link o reparse point non ammesso: " + child);
                    pending.Push(child);
                }
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Link o reparse point non ammesso: " + file);
                    yield return file;
                }
            }
        }

        private static void WriteDocuments(DdfLanguageAuthoringProject project,
            string root)
        {
            Directory.CreateDirectory(root);
            string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            foreach (DdfLanguageAuthoringDocument document in
                project.Documents.Values.OrderBy(item => item.RelativePath,
                    StringComparer.Ordinal))
            {
                string path = Path.GetFullPath(Path.Combine(root,
                    document.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Documento fuori dalla destinazione.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, document.Content, new UTF8Encoding(false));
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static DdfLanguageAuthoringResult Failure(string code,
            string message, string file = null) => new DdfLanguageAuthoringResult(
                null, new[] { Diagnostic(code, message, file) });

        private static DdfLanguageAuthoringMigrationResult MigrationFailure(
            string code, string message, IEnumerable<string> applied) =>
            new DdfLanguageAuthoringMigrationResult(null, applied,
                new[] { Diagnostic(code, message) });

        private static DdfLanguagePackageDiagnostic Diagnostic(string code,
            string message, string file = null, string jsonPath = null) =>
            new DdfLanguagePackageDiagnostic(code, message, file, jsonPath,
                DdfLanguagePackageDiagnosticSeverity.Error,
                DdfDiagnosticDomain.LanguageDefinition);
    }

    internal static class DdfLanguageAuthoringPaths
    {
        internal static bool TryNormalize(string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
                path.IndexOf('\0') >= 0 || path.IndexOf(':') >= 0)
                return false;
            string candidate = path.Replace('\\', '/').Trim('/');
            string[] segments = candidate.Split('/',
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(item => item == "." ||
                item == ".." || item.Length == 0))
                return false;
            normalized = string.Join("/", segments);
            return true;
        }
    }
}
