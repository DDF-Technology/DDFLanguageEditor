using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Immutable input captured by an editor host before starting analysis.
    /// The language and workspace collections are copied so a request cannot
    /// observe later host mutations while it is running.
    /// </summary>
    public sealed class DdfEditorAnalysisRequest
    {
        public DdfEditorAnalysisRequest(
            string source,
            int position,
            long documentVersion,
            DdfLanguagePackage languagePackage,
            long languageRevision,
            string documentId = null,
            string documentPath = null,
            bool includeAllCompletions = false,
            IEnumerable<CompilationUnitSyntax> externalRoots = null,
            IEnumerable<DdfDocumentSymbol> externalSymbols = null,
            IEnumerable<DdfCompletionItem> externalCompletionItems = null,
            bool computeCompletions = true,
            DdfLexResult precomputedLexResult = null,
            DdfHostCatalogSnapshot hostCatalog = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
            if (documentVersion < 0) throw new ArgumentOutOfRangeException(nameof(documentVersion));
            if (languageRevision < 0) throw new ArgumentOutOfRangeException(nameof(languageRevision));
            LanguagePackage = languagePackage ?? throw new ArgumentNullException(nameof(languagePackage));
            Position = position;
            DocumentVersion = documentVersion;
            LanguageRevision = languageRevision;
            DocumentId = documentId ?? string.Empty;
            DocumentPath = documentPath ?? string.Empty;
            IncludeAllCompletions = includeAllCompletions;
            ComputeCompletions = computeCompletions;
            PrecomputedLexResult = precomputedLexResult;
            ExternalRoots = (externalRoots ?? Array.Empty<CompilationUnitSyntax>()).ToList().AsReadOnly();
            ExternalSymbols = (externalSymbols ?? Array.Empty<DdfDocumentSymbol>()).ToList().AsReadOnly();
            ExternalCompletionItems = (externalCompletionItems ?? Array.Empty<DdfCompletionItem>()).ToList().AsReadOnly();
            HostCatalog = hostCatalog;
        }

        public string Source { get; }
        public int Position { get; }
        public long DocumentVersion { get; }
        public DdfLanguagePackage LanguagePackage { get; }
        public long LanguageRevision { get; }
        public string DocumentId { get; }
        public string DocumentPath { get; }
        public bool IncludeAllCompletions { get; }
        public bool ComputeCompletions { get; }
        public DdfLexResult PrecomputedLexResult { get; }
        public IReadOnlyList<CompilationUnitSyntax> ExternalRoots { get; }
        public IReadOnlyList<DdfDocumentSymbol> ExternalSymbols { get; }
        public IReadOnlyList<DdfCompletionItem> ExternalCompletionItems { get; }
        public DdfHostCatalogSnapshot HostCatalog { get; }
    }

    /// <summary>
    /// Coherent, versioned analysis result shared by diagnostics, Outline and
    /// completion consumers. Syntax objects are immutable after construction.
    /// </summary>
    public sealed class DdfEditorAnalysisSnapshot
    {
        internal DdfEditorAnalysisSnapshot(
            long requestId,
            DdfEditorAnalysisRequest request,
            DdfLexResult lexResult,
            DdfParseResult parseResult,
            DdfSemanticModel semanticModel,
            DdfTypeCheckResult typeCheckResult,
            DdfIrBuildResult intermediateRepresentation,
            IReadOnlyList<DdfDocumentSymbol> symbols,
            IReadOnlyList<DdfFoldingRange> foldingRanges,
            DdfCompletionResult completions,
            IReadOnlyList<DdfDiagnostic> diagnostics,
            DateTimeOffset createdAtUtc)
        {
            RequestId = requestId;
            Request = request ?? throw new ArgumentNullException(nameof(request));
            LexResult = lexResult ?? throw new ArgumentNullException(nameof(lexResult));
            ParseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
            SemanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
            TypeCheckResult = typeCheckResult ?? throw new ArgumentNullException(nameof(typeCheckResult));
            IntermediateRepresentation = intermediateRepresentation ?? throw new ArgumentNullException(nameof(intermediateRepresentation));
            Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            FoldingRanges = foldingRanges ?? throw new ArgumentNullException(nameof(foldingRanges));
            Completions = completions ?? throw new ArgumentNullException(nameof(completions));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            CreatedAtUtc = createdAtUtc;
        }

        public long RequestId { get; }
        public DdfEditorAnalysisRequest Request { get; }
        public string Source => Request.Source;
        public int Position => Request.Position;
        public string DocumentId => Request.DocumentId;
        public string DocumentPath => Request.DocumentPath;
        public long DocumentVersion => Request.DocumentVersion;
        public long LanguageRevision => Request.LanguageRevision;
        public DdfLexResult LexResult { get; }
        public DdfParseResult ParseResult { get; }
        public DdfSemanticModel SemanticModel { get; }
        public DdfTypeCheckResult TypeCheckResult { get; }
        public DdfIrBuildResult IntermediateRepresentation { get; }
        public IReadOnlyList<DdfDocumentSymbol> Symbols { get; }
        public IReadOnlyList<DdfFoldingRange> FoldingRanges { get; }
        public DdfCompletionResult Completions { get; }
        public bool CompletionsComputed => Request.ComputeCompletions;
        public IReadOnlyList<DdfDiagnostic> Diagnostics { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public bool HasErrors => Diagnostics.Any(item => item.Severity == DdfDiagnosticSeverity.Error);
    }

    public interface IDdfEditorAnalysisService : IDisposable
    {
        long LatestRequestId { get; }
        DdfEditorAnalysisSnapshot Analyze(DdfEditorAnalysisRequest request,
            CancellationToken cancellationToken = default);
        Task<DdfEditorAnalysisSnapshot> RequestAsync(DdfEditorAnalysisRequest request,
            TimeSpan? debounceDelay = null, CancellationToken cancellationToken = default);
        void Cancel();
        bool IsCurrent(DdfEditorAnalysisSnapshot snapshot);
    }

    /// <summary>
    /// Produces one coherent analysis snapshot and guarantees that only the newest
    /// asynchronous request can complete. It owns no UI and never changes the
    /// process-wide language catalog.
    /// </summary>
    public sealed class DdfEditorAnalysisService : IDdfEditorAnalysisService
    {
        private readonly object syncRoot = new object();
        private CancellationTokenSource currentCancellation;
        private long latestRequestId;
        private bool disposed;

        public long LatestRequestId
        {
            get { lock (syncRoot) return latestRequestId; }
        }

        public DdfEditorAnalysisSnapshot Analyze(DdfEditorAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            long requestId;
            lock (syncRoot)
            {
                if (disposed) throw new ObjectDisposedException(nameof(DdfEditorAnalysisService));
                currentCancellation?.Cancel();
                requestId = ++latestRequestId;
            }
            return Build(requestId, request, cancellationToken);
        }

        public Task<DdfEditorAnalysisSnapshot> RequestAsync(DdfEditorAnalysisRequest request,
            TimeSpan? debounceDelay = null, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            TimeSpan delay = debounceDelay ?? TimeSpan.FromMilliseconds(75);
            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(debounceDelay));

            CancellationTokenSource requestCancellation;
            long requestId;
            lock (syncRoot)
            {
                if (disposed) throw new ObjectDisposedException(nameof(DdfEditorAnalysisService));
                currentCancellation?.Cancel();
                requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                currentCancellation = requestCancellation;
                requestId = ++latestRequestId;
            }
            return BuildAsync(requestId, request, delay, requestCancellation);
        }

        public void Cancel()
        {
            lock (syncRoot) currentCancellation?.Cancel();
        }

        public bool IsCurrent(DdfEditorAnalysisSnapshot snapshot)
        {
            if (snapshot == null) return false;
            lock (syncRoot) return !disposed && snapshot.RequestId == latestRequestId;
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
                cancellation = currentCancellation;
                currentCancellation = null;
            }
            cancellation?.Cancel();
        }

        private async Task<DdfEditorAnalysisSnapshot> BuildAsync(long requestId,
            DdfEditorAnalysisRequest request, TimeSpan delay,
            CancellationTokenSource requestCancellation)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, requestCancellation.Token).ConfigureAwait(false);
                DdfEditorAnalysisSnapshot result = await Task.Run(
                    () => Build(requestId, request, requestCancellation.Token),
                    requestCancellation.Token).ConfigureAwait(false);
                requestCancellation.Token.ThrowIfCancellationRequested();
                lock (syncRoot)
                {
                    if (disposed || requestId != latestRequestId)
                        throw new OperationCanceledException(requestCancellation.Token);
                }
                return result;
            }
            finally
            {
                lock (syncRoot)
                {
                    if (ReferenceEquals(currentCancellation, requestCancellation)) currentCancellation = null;
                }
                requestCancellation.Dispose();
            }
        }

        private static DdfEditorAnalysisSnapshot Build(long requestId,
            DdfEditorAnalysisRequest request, CancellationToken cancellationToken)
        {
            DdfLanguageDefinition language = request.LanguagePackage.Language;
            cancellationToken.ThrowIfCancellationRequested();
            DdfLexResult lex = request.PrecomputedLexResult ?? DdfLexer.Lex(request.Source, language);
            cancellationToken.ThrowIfCancellationRequested();
            DdfParseResult parse = DdfParser.Parse(request.Source, lex, language);
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<string> hostNames = request.HostCatalog == null
                ? Array.Empty<string>()
                : request.HostCatalog.Objects.Select(item => item.Name).Concat(
                    request.HostCatalog.Functions.Select(item => item.Name));
            IEnumerable<string> knownExternalNames = request.ExternalSymbols
                .Select(item => item.Name).Concat(hostNames);
            DdfSemanticModel semantics = DdfSemanticModel.Create(request.Source,
                parse.Root, knownExternalNames);
            cancellationToken.ThrowIfCancellationRequested();
            DdfTypeCheckResult types = DdfTypeChecker.Check(request.Source, parse.Root,
                request.ExternalRoots, language.Types, language.StandardLibrary,
                request.HostCatalog);
            cancellationToken.ThrowIfCancellationRequested();
            DdfIrBuildResult ir = DdfIrLowerer.Lower(request.Source, parse, language);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(parse, request.Source).Symbols;
            IReadOnlyList<DdfFoldingRange> folding = DdfFoldingRangeProvider.Create(parse, request.Source);
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<DdfCompletionItem> hostItems = CreateHostCompletionItems(
                request.HostCatalog);
            DdfCompletionResult completions = request.ComputeCompletions
                ? DdfCompletionService.GetCompletions(
                    request.Source, request.Position, request.IncludeAllCompletions, language,
                    request.ExternalSymbols, request.ExternalCompletionItems.Concat(hostItems),
                    request.ExternalRoots, cancellationToken, request.HostCatalog)
                : new DdfCompletionResult(request.Position, 0,
                    Array.Empty<DdfCompletionItem>());
            IReadOnlyList<DdfDiagnostic> diagnostics = MergeDiagnostics(
                parse.Diagnostics, semantics.Diagnostics, types.Diagnostics, ir.Diagnostics);
            return new DdfEditorAnalysisSnapshot(requestId, request, lex, parse, semantics, types,
                ir, symbols, folding, completions, diagnostics, DateTimeOffset.UtcNow);
        }

        private static IEnumerable<DdfCompletionItem> CreateHostCompletionItems(
            DdfHostCatalogSnapshot catalog)
        {
            if (catalog == null) return Array.Empty<DdfCompletionItem>();
            IEnumerable<DdfCompletionItem> types = catalog.Types.Select(item =>
                new DdfCompletionItem(item.Name, item.Name, DdfCompletionKind.Type,
                    item.Documentation, item.Name, "Host", int.MaxValue));
            IEnumerable<DdfCompletionItem> objects = catalog.Objects.Select(item =>
                new DdfCompletionItem(item.Name, item.Name, DdfCompletionKind.Variable,
                    item.Documentation, item.TypeName, "Host", int.MaxValue));
            IEnumerable<DdfCompletionItem> functions = catalog.Functions.Select(item =>
                new DdfCompletionItem(item.Name, item.Name, DdfCompletionKind.Function,
                    item.Signature + (string.IsNullOrWhiteSpace(item.Documentation)
                        ? string.Empty : " — " + item.Documentation),
                    item.ReturnType, "Host", int.MaxValue));
            return types.Concat(objects).Concat(functions);
        }

        private static IReadOnlyList<DdfDiagnostic> MergeDiagnostics(
            params IEnumerable<DdfDiagnostic>[] diagnosticSets)
        {
            return diagnosticSets
                .Where(set => set != null)
                .SelectMany(set => set)
                .GroupBy(item => new
                {
                    item.Code,
                    item.Message,
                    item.Start,
                    item.Length,
                    item.Severity,
                    item.Domain,
                    item.DocumentPath
                })
                .Select(group => group.First())
                .OrderBy(item => item.Start)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }
    }
}
