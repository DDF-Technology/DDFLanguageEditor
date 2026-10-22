using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetDebugLocation
    {
        internal DdfDotNetDebugLocation(int methodToken, int ilOffset,
            string sourcePath, int line, int column)
        {
            MethodToken = methodToken;
            IlOffset = ilOffset;
            SourcePath = sourcePath;
            Line = line;
            Column = column;
        }

        public int MethodToken { get; }
        public int IlOffset { get; }
        public string SourcePath { get; }
        public int Line { get; }
        public int Column { get; }
    }

    public sealed class DdfDotNetDebugSymbolInfo
    {
        internal DdfDotNetDebugSymbolInfo(DdfDotNetDebugSymbol symbol)
        {
            SymbolId = symbol.SymbolId;
            Name = symbol.Name;
            Kind = symbol.Kind;
            Type = symbol.Type;
            SourcePath = symbol.SourcePath;
            DdfStart = symbol.DdfStart;
            DdfLength = symbol.DdfLength;
            StorageName = symbol.StorageName;
            IsWritable = symbol.IsWritable;
        }

        public string SymbolId { get; }
        public string Name { get; }
        public string Kind { get; }
        public string Type { get; }
        public string SourcePath { get; }
        public int DdfStart { get; }
        public int DdfLength { get; }
        internal string StorageName { get; }
        public bool IsWritable { get; }
    }

    public sealed class DdfDotNetDebugFrameInfo
    {
        internal DdfDotNetDebugFrameInfo(int methodToken,
            DdfDotNetDebugSymbolInfo function,
            IEnumerable<DdfDotNetDebugLocation> sequencePoints)
        {
            MethodToken = methodToken;
            Function = function;
            SequencePoints = sequencePoints.ToList().AsReadOnly();
        }

        public int MethodToken { get; }
        public DdfDotNetDebugSymbolInfo Function { get; }
        public IReadOnlyList<DdfDotNetDebugLocation> SequencePoints { get; }
    }

    public sealed class DdfDotNetDebugAdapterResult
    {
        internal DdfDotNetDebugAdapterResult(DdfDotNetDebugAdapter adapter,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Adapter = adapter;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>()).ToList()
                .AsReadOnly();
        }

        public DdfDotNetDebugAdapter Adapter { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Adapter != null && Issues.Count == 0;
    }

    /// <summary>
    /// Immutable, read-only projection of PE/PDB metadata into DDF identities.
    /// It does not load or execute the assembly and never returns a generated
    /// identifier as the display name of a frame or variable.
    /// </summary>
    public sealed class DdfDotNetDebugAdapter
    {
        private sealed class ScopeInfo
        {
            internal int MethodToken { get; init; }
            internal int StartOffset { get; init; }
            internal int EndOffset { get; init; }
            internal IReadOnlyList<DdfDotNetDebugSymbolInfo> Symbols { get; init; }
        }

        private readonly IReadOnlyDictionary<int, DdfDotNetDebugFrameInfo> frames;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<
            DdfDotNetDebugSymbolInfo>> parameters;
        private readonly IReadOnlyList<ScopeInfo> scopes;
        private readonly IReadOnlyList<DdfDotNetDebugSymbolInfo> globals;

        private DdfDotNetDebugAdapter(
            IEnumerable<DdfDotNetDebugFrameInfo> frames,
            IReadOnlyDictionary<int, IReadOnlyList<DdfDotNetDebugSymbolInfo>> parameters,
            IEnumerable<ScopeInfo> scopes,
            IEnumerable<DdfDotNetDebugSymbolInfo> globals)
        {
            this.frames = frames.ToDictionary(item => item.MethodToken);
            Frames = this.frames.Values.OrderBy(item => item.Function.SourcePath,
                StringComparer.Ordinal).ThenBy(item => item.Function.DdfStart)
                .ToList().AsReadOnly();
            this.parameters = parameters;
            this.scopes = scopes.ToList().AsReadOnly();
            this.globals = globals.ToList().AsReadOnly();
        }

        public IReadOnlyList<DdfDotNetDebugFrameInfo> Frames { get; }

        public static DdfDotNetDebugAdapterResult Create(
            DdfDotNetEmittedArtifact artifact)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (artifact == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC420",
                    "Artefatto compilato richiesto per il debugger."));
                return new DdfDotNetDebugAdapterResult(null, issues);
            }
            issues.AddRange(DdfDotNetArtifactContract.ValidateBundle(
                artifact.Manifest, artifact.SourceMap));
            if (!PayloadMatches(artifact.Manifest, DdfDotNetPayloadKind.Assembly,
                    artifact.AssemblyBytes) ||
                !PayloadMatches(artifact.Manifest,
                    DdfDotNetPayloadKind.PortablePdb,
                    artifact.PortablePdbBytes) ||
                !PayloadMatches(artifact.Manifest, DdfDotNetPayloadKind.SourceMap,
                    artifact.SourceMapBytes))
                issues.Add(new DdfDotNetContractIssue("DDFC420",
                    "I payload debug non corrispondono a hash e dimensioni del manifest."));
            if (issues.Count != 0)
                return new DdfDotNetDebugAdapterResult(null, issues);
            try
            {
                return Build(artifact, issues);
            }
            catch (BadImageFormatException exception)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC421",
                    "PE o Portable PDB non valido: " + exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC422",
                    "Metadati debug DDF incoerenti: " + exception.Message));
            }
            return new DdfDotNetDebugAdapterResult(null, issues);
        }

        private static bool PayloadMatches(DdfDotNetArtifactManifest manifest,
            DdfDotNetPayloadKind kind, byte[] bytes)
        {
            DdfDotNetPayloadDescriptor descriptor = manifest.Payloads.SingleOrDefault(
                item => item.Kind == kind);
            return descriptor != null && bytes != null &&
                descriptor.Size == bytes.LongLength &&
                descriptor.Sha256 == DdfDotNetArtifactContract.Hash(bytes);
        }

        public DdfDotNetDebugFrameInfo ResolveFrame(int methodToken) =>
            frames.TryGetValue(methodToken, out DdfDotNetDebugFrameInfo frame)
                ? frame : null;

        public DdfDotNetDebugLocation BindBreakpoint(string sourcePath, int line)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || line < 1) return null;
            string normalized = sourcePath.Replace('\\', '/');
            return Frames.SelectMany(frame => frame.SequencePoints)
                .Where(point => string.Equals(point.SourcePath, normalized,
                    StringComparison.Ordinal) && point.Line >= line)
                .OrderBy(point => point.Line == line ? 0 : 1)
                .ThenBy(point => point.Line)
                .ThenBy(point => point.Column)
                .ThenBy(point => point.MethodToken)
                .FirstOrDefault();
        }

        public IReadOnlyList<DdfDotNetDebugSymbolInfo> GetVisibleSymbols(
            int methodToken, int ilOffset)
        {
            if (!frames.ContainsKey(methodToken) || ilOffset < 0)
                return Array.Empty<DdfDotNetDebugSymbolInfo>();
            var visible = new List<DdfDotNetDebugSymbolInfo>();
            visible.AddRange(globals);
            if (parameters.TryGetValue(methodToken,
                    out IReadOnlyList<DdfDotNetDebugSymbolInfo> methodParameters))
                visible.AddRange(methodParameters);
            foreach (ScopeInfo scope in scopes.Where(item =>
                         item.MethodToken == methodToken &&
                         ilOffset >= item.StartOffset &&
                         ilOffset < item.EndOffset))
                visible.AddRange(scope.Symbols);
            return visible.GroupBy(item => item.SymbolId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => KindOrder(item.Kind))
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        private static DdfDotNetDebugAdapterResult Build(
            DdfDotNetEmittedArtifact artifact,
            List<DdfDotNetContractIssue> issues)
        {
            using var peStream = new MemoryStream(artifact.AssemblyBytes,
                writable: false);
            using var pe = new PEReader(peStream);
            if (!pe.HasMetadata) throw new BadImageFormatException(
                "L'immagine PE non contiene metadati gestiti.");
            MetadataReader metadata = pe.GetMetadataReader();
            using MetadataReaderProvider pdbProvider =
                MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(
                    artifact.PortablePdbBytes, writable: false));
            MetadataReader pdb = pdbProvider.GetMetadataReader();
            var symbolsByStorage = artifact.SourceMap.Symbols.ToDictionary(item =>
                item.StorageName, item => new DdfDotNetDebugSymbolInfo(item),
                StringComparer.Ordinal);
            var knownDocuments = artifact.SourceMap.Documents.Select(item =>
                item.SourcePath).ToHashSet(StringComparer.Ordinal);
            var frames = new List<DdfDotNetDebugFrameInfo>();
            var parameters = new Dictionary<int, IReadOnlyList<
                DdfDotNetDebugSymbolInfo>>();
            var scopes = new List<ScopeInfo>();
            foreach (MethodDebugInformationHandle debugHandle in
                     pdb.MethodDebugInformation)
            {
                int row = MetadataTokens.GetRowNumber(debugHandle);
                if (row == 0 || row > metadata.MethodDefinitions.Count) continue;
                MethodDefinitionHandle methodHandle =
                    MetadataTokens.MethodDefinitionHandle(row);
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                MethodDebugInformation debug = pdb.GetMethodDebugInformation(
                    debugHandle);
                MethodDefinitionHandle kickoffHandle =
                    debug.GetStateMachineKickoffMethod();
                MethodDefinition symbolMethod = method;
                if (!kickoffHandle.IsNil)
                    symbolMethod = metadata.GetMethodDefinition(kickoffHandle);
                string storageName = metadata.GetString(symbolMethod.Name);
                if (!symbolsByStorage.TryGetValue(storageName,
                        out DdfDotNetDebugSymbolInfo function) ||
                    function.Kind != DdfBoundSymbolKind.Function.ToString())
                    continue;
                int methodToken = MetadataTokens.GetToken(methodHandle);
                var points = new List<DdfDotNetDebugLocation>();
                DocumentHandle currentDocument = debug.Document;
                foreach (SequencePoint point in debug.GetSequencePoints())
                {
                    if (!point.Document.IsNil) currentDocument = point.Document;
                    if (point.IsHidden || currentDocument.IsNil) continue;
                    string sourcePath = pdb.GetString(
                        pdb.GetDocument(currentDocument).Name).Replace('\\', '/');
                    if (!knownDocuments.Contains(sourcePath))
                        throw new InvalidOperationException(
                            "Sequence point esterno ai documenti DDF: " +
                            sourcePath + ".");
                    points.Add(new DdfDotNetDebugLocation(methodToken,
                        point.Offset, sourcePath, point.StartLine,
                        point.StartColumn));
                }
                if (points.Count == 0) continue;
                frames.Add(new DdfDotNetDebugFrameInfo(methodToken, function,
                    points));
                parameters[methodToken] = symbolMethod.GetParameters()
                    .Select(handle => metadata.GetParameter(handle))
                    .Where(parameter => parameter.SequenceNumber > 0)
                    .Select(parameter => metadata.GetString(parameter.Name))
                    .Where(symbolsByStorage.ContainsKey)
                    .Select(name => symbolsByStorage[name]).ToList().AsReadOnly();
                foreach (LocalScopeHandle scopeHandle in
                         pdb.GetLocalScopes(methodHandle))
                {
                    LocalScope scope = pdb.GetLocalScope(scopeHandle);
                    IReadOnlyList<DdfDotNetDebugSymbolInfo> localSymbols = scope
                        .GetLocalVariables().Select(handle => pdb.GetString(
                            pdb.GetLocalVariable(handle).Name))
                        .Where(symbolsByStorage.ContainsKey)
                        .Select(name => symbolsByStorage[name]).ToList().AsReadOnly();
                    scopes.Add(new ScopeInfo
                    {
                        MethodToken = methodToken,
                        StartOffset = scope.StartOffset,
                        EndOffset = scope.StartOffset + scope.Length,
                        Symbols = localSymbols
                    });
                }
            }
            if (frames.Count == 0)
                throw new InvalidOperationException(
                    "Nessun frame DDF risolvibile nel Portable PDB.");
            var globals = artifact.SourceMap.Symbols.Where(item =>
                    item.Kind == DdfBoundSymbolKind.Global.ToString())
                .Select(item => symbolsByStorage[item.StorageName]);
            return new DdfDotNetDebugAdapterResult(new DdfDotNetDebugAdapter(
                frames, parameters, scopes, globals), issues);
        }

        private static int KindOrder(string kind) => kind switch
        {
            nameof(DdfBoundSymbolKind.Parameter) => 0,
            nameof(DdfBoundSymbolKind.Local) => 1,
            nameof(DdfBoundSymbolKind.Global) => 2,
            _ => 3
        };
    }
}
