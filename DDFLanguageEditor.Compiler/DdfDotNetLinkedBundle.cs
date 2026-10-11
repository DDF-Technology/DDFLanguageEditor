using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetPackagedReference
    {
        private readonly byte[] assemblyBytes;

        internal DdfDotNetPackagedReference(string assemblyIdentity,
            string sha256, long size, byte[] bytes)
        {
            AssemblyIdentity = assemblyIdentity;
            Sha256 = sha256;
            Size = size;
            assemblyBytes = (bytes ?? throw new ArgumentNullException(
                nameof(bytes))).ToArray();
        }

        public string AssemblyIdentity { get; }
        public string Sha256 { get; }
        public long Size { get; }
        public byte[] GetAssemblyBytes() => assemblyBytes.ToArray();
    }

    public sealed class DdfDotNetPeVerificationResult
    {
        internal DdfDotNetPeVerificationResult(
            IEnumerable<DdfDotNetContractIssue> issues,
            long methodBodies = 0, long instructions = 0,
            long totalIlBytes = 0)
        {
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
            MethodBodies = methodBodies;
            Instructions = instructions;
            TotalIlBytes = totalIlBytes;
        }

        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public long MethodBodies { get; }
        public long Instructions { get; }
        public long TotalIlBytes { get; }
        public bool Success => Issues.Count == 0;
    }

    /// <summary>
    /// Inspects PE and metadata directly. It never loads an assembly, resolves a
    /// type, runs a module initializer or executes user code.
    /// </summary>
    public static class DdfDotNetPeVerifier
    {
        public static DdfDotNetPeVerificationResult Verify(
            DdfDotNetLinkedProgram program,
            IEnumerable<DdfDotNetEmittedArtifact> artifacts,
            DdfDotNetReferenceSet referenceSet,
            DdfDotNetPeVerificationLimits limits = null)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (program == null)
                issues.Add(new DdfDotNetContractIssue("DDFC506",
                    "È richiesto un programma collegato valido."));
            if (referenceSet == null)
                issues.Add(new DdfDotNetContractIssue("DDFC510",
                    "È richiesta l'allowlist delle reference assembly."));
            limits ??= DdfDotNetPeVerificationLimits.Default;
            List<DdfDotNetEmittedArtifact> supplied = (artifacts ??
                Array.Empty<DdfDotNetEmittedArtifact>()).ToList();
            if (program == null || referenceSet == null)
                return new DdfDotNetPeVerificationResult(issues);
            var byIdentity = new Dictionary<string, DdfDotNetEmittedArtifact>(
                StringComparer.Ordinal);
            foreach (DdfDotNetEmittedArtifact artifact in supplied)
            {
                if (artifact == null || !byIdentity.TryAdd(
                        artifact.ModuleIdentity, artifact))
                    issues.Add(new DdfDotNetContractIssue("DDFC506",
                        "Artefatto binario assente o duplicato nel bundle collegato."));
            }
            if (byIdentity.Count != program.Artifacts.Count ||
                program.Artifacts.Any(artifact =>
                    !byIdentity.ContainsKey(artifact.Identity)))
                issues.Add(new DdfDotNetContractIssue("DDFC506",
                    "Il bundle binario non coincide col grafo collegato."));
            if (issues.Count != 0)
                return new DdfDotNetPeVerificationResult(issues);

            var linkedAssemblyNames = program.Artifacts.ToDictionary(
                artifact => artifact.AssemblyName, artifact => artifact.Identity,
                StringComparer.OrdinalIgnoreCase);
            var linkedAssemblyIdentities = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (DdfDotNetLinkedArtifact linked in program.Artifacts)
            {
                try
                {
                    linkedAssemblyIdentities.Add(linked.AssemblyName,
                        ReadAssemblyDefinitionIdentity(
                            byIdentity[linked.Identity].AssemblyBytes));
                }
                catch (Exception exception) when (
                    exception is BadImageFormatException ||
                    exception is IOException ||
                    exception is InvalidOperationException)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC507",
                        linked.Identity +
                        ": identità assembly non leggibile: " +
                        exception.Message));
                }
            }
            IReadOnlyDictionary<string, IReadOnlySet<string>> allowedReferences =
                CreateReferenceAllowlist(referenceSet, issues);
            long methodBodies = 0;
            long instructions = 0;
            long totalIlBytes = 0;
            long typeDefinitions = 0;
            long methodDefinitions = 0;
            long fieldDefinitions = 0;
            long exceptionRegions = 0;
            foreach (DdfDotNetLinkedArtifact linked in program.Artifacts)
            {
                if (linked.Manifest.BuildReceipt?.ReferenceSetFingerprint !=
                    referenceSet.Fingerprint)
                    issues.Add(new DdfDotNetContractIssue("DDFC510",
                        linked.Identity +
                        ": reference set diverso dalla ricevuta di build."));
                DdfDotNetEmittedArtifact emitted = byIdentity[linked.Identity];
                DdfDotNetIlVerificationSummary summary = VerifyArtifact(program,
                    linked, emitted, linkedAssemblyNames, allowedReferences,
                    linkedAssemblyIdentities, limits, issues);
                methodBodies += summary.MethodBodies;
                instructions += summary.Instructions;
                totalIlBytes += summary.TotalIlBytes;
                typeDefinitions += summary.TypeDefinitions;
                methodDefinitions += summary.MethodDefinitions;
                fieldDefinitions += summary.FieldDefinitions;
                exceptionRegions += summary.ExceptionRegions;
            }
            if (typeDefinitions > limits.MaxTypes ||
                methodDefinitions > limits.MaxMethods ||
                fieldDefinitions > limits.MaxFields ||
                totalIlBytes > limits.MaxTotalIlBytes ||
                instructions > limits.MaxInstructions ||
                exceptionRegions > limits.MaxExceptionRegions)
                issues.Add(new DdfDotNetContractIssue("DDFC511",
                    "Il programma collegato supera i budget strutturali complessivi."));
            return new DdfDotNetPeVerificationResult(issues, methodBodies,
                instructions, totalIlBytes);
        }

        private static DdfDotNetIlVerificationSummary VerifyArtifact(
            DdfDotNetLinkedProgram program,
            DdfDotNetLinkedArtifact linked, DdfDotNetEmittedArtifact emitted,
            IReadOnlyDictionary<string, string> linkedAssemblyNames,
            IReadOnlyDictionary<string, IReadOnlySet<string>> allowedReferences,
            IReadOnlyDictionary<string, string> linkedAssemblyIdentities,
            DdfDotNetPeVerificationLimits limits,
            ICollection<DdfDotNetContractIssue> issues)
        {
            var summary = new DdfDotNetIlVerificationSummary();
            DdfDotNetPayloadDescriptor actual =
                DdfDotNetPayloadDescriptor.FromBytes(
                    DdfDotNetPayloadKind.Assembly,
                    Path.GetFileName(emitted.AssemblyPath),
                    emitted.AssemblyBytes);
            if (emitted.Manifest == null ||
                emitted.ModuleIdentity != linked.Identity ||
                emitted.Manifest.ModuleId + "@" +
                    emitted.Manifest.ModuleVersion != linked.Identity ||
                emitted.Manifest.AssemblyName != linked.AssemblyName ||
                actual.Sha256 != linked.AssemblySha256)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC506",
                    linked.Identity +
                    ": payload o manifest non coincide col programma collegato."));
                return summary;
            }
            try
            {
                using var stream = new MemoryStream(emitted.AssemblyBytes,
                    writable: false);
                using var pe = new PEReader(stream,
                    PEStreamOptions.LeaveOpen);
                if (!pe.HasMetadata || pe.PEHeaders.CorHeader == null)
                    throw new BadImageFormatException(
                        "Il payload non contiene metadati CLR.");
                CorFlags flags = pe.PEHeaders.CorHeader.Flags;
                if ((flags & CorFlags.ILOnly) == 0 ||
                    (flags & CorFlags.NativeEntryPoint) != 0 ||
                    pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress != 0)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC509",
                        linked.Identity +
                        ": sono ammessi soltanto assembly IL-only senza entry point nativo o autonomo."));
                }
                MetadataReader metadata = pe.GetMetadataReader();
                if (!metadata.IsAssembly)
                    throw new BadImageFormatException(
                        "Il payload non contiene una definizione assembly.");
                AssemblyDefinition definition = metadata.GetAssemblyDefinition();
                string assemblyName = metadata.GetString(definition.Name);
                if (!string.Equals(assemblyName, linked.AssemblyName,
                        StringComparison.Ordinal))
                    issues.Add(new DdfDotNetContractIssue("DDFC508",
                        linked.Identity + ": identità PE inattesa: " +
                        assemblyName + "."));

                var references = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (AssemblyReferenceHandle handle in
                         metadata.AssemblyReferences)
                {
                    AssemblyReference reference = metadata.GetAssemblyReference(
                        handle);
                    string referenceName = metadata.GetString(reference.Name);
                    if (!references.Add(referenceName))
                        issues.Add(new DdfDotNetContractIssue("DDFC508",
                            linked.Identity + ": riferimento assembly duplicato: " +
                            referenceName + "."));
                    AssemblyName referenceIdentity = ReferenceIdentity(
                        metadata, reference);
                    string canonical = CanonicalIdentity(referenceIdentity);
                    if (linkedAssemblyNames.ContainsKey(referenceName))
                    {
                        if (!linkedAssemblyIdentities.TryGetValue(referenceName,
                                out string expectedIdentity) ||
                            !string.Equals(canonical, expectedIdentity,
                                StringComparison.OrdinalIgnoreCase))
                            issues.Add(new DdfDotNetContractIssue("DDFC508",
                                linked.Identity +
                                ": identità della dipendenza DDF incompatibile: " +
                                referenceIdentity.FullName + "."));
                    }
                    else
                    {
                        if (!allowedReferences.TryGetValue(referenceName,
                                out IReadOnlySet<string> allowed) ||
                            !allowed.Contains(canonical))
                            issues.Add(new DdfDotNetContractIssue("DDFC510",
                                linked.Identity +
                                ": riferimento gestito fuori allowlist: " +
                                referenceIdentity.FullName + "."));
                    }
                }
                var expectedDependencies = linked.DependencyIdentities.Select(
                        identity => program.Artifacts.Single(artifact =>
                            artifact.Identity == identity).AssemblyName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string expected in expectedDependencies)
                    if (!references.Contains(expected))
                        issues.Add(new DdfDotNetContractIssue("DDFC508",
                            linked.Identity +
                            ": riferimento alla dipendenza DDF assente: " +
                            expected + "."));
                foreach (string reference in references)
                    if (linkedAssemblyNames.TryGetValue(reference,
                            out string referencedIdentity) &&
                        !expectedDependencies.Contains(reference))
                        issues.Add(new DdfDotNetContractIssue("DDFC508",
                            linked.Identity +
                            ": riferimento DDF non dichiarato: " +
                            referencedIdentity + "."));

                if (metadata.GetTableRowCount(TableIndex.ModuleRef) > 0)
                    issues.Add(new DdfDotNetContractIssue("DDFC509",
                        linked.Identity +
                        ": riferimenti a moduli nativi non consentiti."));
                foreach (MethodDefinitionHandle handle in
                         metadata.MethodDefinitions)
                {
                    MethodDefinition method = metadata.GetMethodDefinition(handle);
                    if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC509",
                            linked.Identity +
                            ": metodo P/Invoke non consentito."));
                        break;
                    }
                }
                if (!issues.Any(issue => issue.Code is "DDFC507" or
                        "DDFC509" or "DDFC510" or "DDFC511" or "DDFC512"))
                    summary = DdfDotNetIlVerifier.Verify(linked.Identity, pe,
                        metadata, limits, issues);
            }
            catch (Exception exception) when (exception is BadImageFormatException ||
                                               exception is IOException ||
                                               exception is InvalidOperationException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC507",
                    linked.Identity + ": PE o metadati non validi: " +
                    exception.Message));
            }
            return summary;
        }

        private static string ReadAssemblyDefinitionIdentity(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                throw new BadImageFormatException("Metadati CLR assenti.");
            MetadataReader metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly)
                throw new BadImageFormatException("Definizione assembly assente.");
            AssemblyDefinition definition = metadata.GetAssemblyDefinition();
            var identity = new AssemblyName
            {
                Name = metadata.GetString(definition.Name),
                Version = definition.Version,
                CultureName = definition.Culture.IsNil
                    ? null
                    : metadata.GetString(definition.Culture)
            };
            byte[] key = definition.PublicKey.IsNil
                ? Array.Empty<byte>()
                : metadata.GetBlobBytes(definition.PublicKey);
            identity.SetPublicKey(key);
            return CanonicalIdentity(identity);
        }

        private static IReadOnlyDictionary<string, IReadOnlySet<string>>
            CreateReferenceAllowlist(DdfDotNetReferenceSet referenceSet,
                ICollection<DdfDotNetContractIssue> issues)
        {
            var result = new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (DdfDotNetReferenceDescriptor descriptor in
                     referenceSet.References)
            {
                try
                {
                    var identity = new AssemblyName(descriptor.AssemblyName);
                    string name = identity.Name ?? string.Empty;
                    if (!result.TryGetValue(name, out HashSet<string> values))
                        result.Add(name, values = new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase));
                    values.Add(CanonicalIdentity(identity));
                }
                catch (Exception exception) when (exception is ArgumentException ||
                                                   exception is FileLoadException)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC510",
                        "Identità reference non valida: " + exception.Message));
                }
            }
            return result.ToDictionary(pair => pair.Key,
                pair => (IReadOnlySet<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        private static AssemblyName ReferenceIdentity(MetadataReader metadata,
            AssemblyReference reference)
        {
            var identity = new AssemblyName
            {
                Name = metadata.GetString(reference.Name),
                Version = reference.Version,
                CultureName = reference.Culture.IsNil
                    ? null
                    : metadata.GetString(reference.Culture)
            };
            byte[] key = reference.PublicKeyOrToken.IsNil
                ? Array.Empty<byte>()
                : metadata.GetBlobBytes(reference.PublicKeyOrToken);
            if ((reference.Flags & AssemblyFlags.PublicKey) != 0)
                identity.SetPublicKey(key);
            else
                identity.SetPublicKeyToken(key);
            return identity;
        }

        private static string CanonicalIdentity(AssemblyName identity) =>
            (identity.Name ?? string.Empty) + "|" +
            (identity.Version?.ToString() ?? string.Empty) + "|" +
            (identity.CultureName ?? string.Empty) + "|" +
            Convert.ToHexString(identity.GetPublicKeyToken() ??
                Array.Empty<byte>()).ToLowerInvariant();
    }

    public sealed class DdfDotNetLinkedBinaryArtifact
    {
        private readonly byte[] assemblyBytes;
        private readonly byte[] portablePdbBytes;
        private readonly byte[] sourceMapBytes;
        private readonly byte[] manifestBytes;

        internal DdfDotNetLinkedBinaryArtifact(
            DdfDotNetEmittedArtifact artifact)
        {
            Identity = artifact.ModuleIdentity;
            AssemblyName = artifact.Manifest.AssemblyName;
            AssemblyPath = artifact.AssemblyPath;
            PortablePdbPath = artifact.PortablePdbPath;
            SourceMapPath = artifact.SourceMapPath;
            ManifestPath = artifact.ManifestPath;
            assemblyBytes = artifact.AssemblyBytes.ToArray();
            portablePdbBytes = artifact.PortablePdbBytes.ToArray();
            sourceMapBytes = artifact.SourceMapBytes.ToArray();
            manifestBytes = artifact.ManifestBytes.ToArray();
            AssemblySha256 = Hash(assemblyBytes);
            PortablePdbSha256 = Hash(portablePdbBytes);
            SourceMapSha256 = Hash(sourceMapBytes);
            ManifestSha256 = Hash(manifestBytes);
        }

        public string Identity { get; }
        public string AssemblyName { get; }
        public string AssemblyPath { get; }
        public string PortablePdbPath { get; }
        public string SourceMapPath { get; }
        public string ManifestPath { get; }
        public string AssemblySha256 { get; }
        public string PortablePdbSha256 { get; }
        public string SourceMapSha256 { get; }
        public string ManifestSha256 { get; }
        public byte[] GetAssemblyBytes() => assemblyBytes.ToArray();
        public byte[] GetPortablePdbBytes() => portablePdbBytes.ToArray();
        public byte[] GetSourceMapBytes() => sourceMapBytes.ToArray();
        public byte[] GetManifestBytes() => manifestBytes.ToArray();

        private static string Hash(byte[] value)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(value))
                .ToLowerInvariant();
        }
    }

    public sealed class DdfDotNetLinkedBundle
    {
        internal DdfDotNetLinkedBundle(DdfDotNetLinkedProgram program,
            IEnumerable<DdfDotNetLinkedBinaryArtifact> artifacts,
            DdfDotNetReferenceSet referenceSet,
            IEnumerable<DdfDotNetPackagedReference> packagedReferences = null)
        {
            EntryModuleIdentity = program.EntryModuleIdentity;
            ProgramFingerprint = program.Fingerprint;
            RequiredHostCapabilities = program.RequiredHostCapabilities;
            Artifacts = (artifacts ??
                    Array.Empty<DdfDotNetLinkedBinaryArtifact>())
                .ToList().AsReadOnly();
            ReferenceSetFingerprint = referenceSet?.Fingerprint ??
                throw new ArgumentNullException(nameof(referenceSet));
            PackagedReferences = (packagedReferences ?? Array.Empty<
                    DdfDotNetPackagedReference>()).OrderBy(item =>
                    item.AssemblyIdentity, StringComparer.Ordinal).ToList()
                .AsReadOnly();
            var packagedIdentities = new HashSet<string>(PackagedReferences
                .Select(item => item.AssemblyIdentity), StringComparer.Ordinal);
            ManagedReferences = referenceSet.References.Select(item =>
                    new DdfDotNetLinkedPackageReference
                    {
                        AssemblyIdentity = item.AssemblyName,
                        Sha256 = item.Sha256,
                        Size = item.Size,
                        Packaged = packagedIdentities.Contains(item.AssemblyName)
                    }).OrderBy(item => item.AssemblyIdentity,
                        StringComparer.Ordinal).ToList().AsReadOnly();
            Fingerprint = ComputeFingerprint();
            DdfDotNetLinkedPackageManifest packageManifest =
                DdfDotNetLinkedPackageContract.Create(this, referenceSet);
            packageManifestBytes = Encoding.UTF8.GetBytes(
                DdfDotNetLinkedPackageContract.Serialize(packageManifest));
            PackageManifestSha256 = DdfDotNetArtifactContract.Hash(
                packageManifestBytes);
        }

        private readonly byte[] packageManifestBytes;
        public const string FormatVersion = "1.2";
        public string EntryModuleIdentity { get; }
        public string ProgramFingerprint { get; }
        public IReadOnlyList<string> RequiredHostCapabilities { get; }
        public IReadOnlyList<DdfDotNetLinkedBinaryArtifact> Artifacts { get; }
        public string ReferenceSetFingerprint { get; }
        public IReadOnlyList<DdfDotNetLinkedPackageReference> ManagedReferences
            { get; }
        public IReadOnlyList<DdfDotNetPackagedReference> PackagedReferences
            { get; }
        public string Fingerprint { get; }
        public string PackageManifestSha256 { get; }
        public byte[] GetPackageManifestBytes() => packageManifestBytes.ToArray();

        private string ComputeFingerprint()
        {
            var value = new StringBuilder();
            value.Append(FormatVersion).Append('\n')
                .Append(ProgramFingerprint).Append('\n')
                .Append(EntryModuleIdentity).Append('\n');
            foreach (string capability in RequiredHostCapabilities)
                value.Append("capability:").Append(capability).Append('\n');
            foreach (DdfDotNetLinkedBinaryArtifact artifact in Artifacts)
                value.Append(artifact.Identity).Append(':')
                    .Append(artifact.AssemblySha256).Append(':')
                    .Append(artifact.PortablePdbSha256).Append(':')
                    .Append(artifact.SourceMapSha256).Append(':')
                    .Append(artifact.ManifestSha256).Append('\n');
            value.Append("references:").Append(ReferenceSetFingerprint)
                .Append('\n');
            foreach (DdfDotNetLinkedPackageReference reference in
                     ManagedReferences)
                value.Append(reference.AssemblyIdentity).Append(':')
                    .Append(reference.Sha256).Append(':')
                    .Append(reference.Size).Append(':')
                    .Append(reference.Packaged ? "packaged" : "platform")
                    .Append('\n');
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                value.ToString()))).ToLowerInvariant();
        }
    }

    public sealed class DdfDotNetLinkedBundleResult
    {
        internal DdfDotNetLinkedBundleResult(DdfDotNetLinkedBundle bundle,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            Bundle = bundle;
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public DdfDotNetLinkedBundle Bundle { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Bundle != null && Issues.Count == 0;
    }

    public static class DdfDotNetLinkedBundleBuilder
    {
        public static DdfDotNetLinkedBundleResult Create(
            DdfDotNetLinkedProgram program,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetReferenceSet referenceSet,
            DdfDotNetPeVerificationLimits limits = null)
        {
            return CreateCore(program, emission, referenceSet, null, limits);
        }

        public static DdfDotNetLinkedBundleResult CreateWithPackagedReferences(
            DdfDotNetLinkedProgram program,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetReferenceSet referenceSet,
            IEnumerable<string> packagedReferencePaths,
            DdfDotNetPeVerificationLimits limits = null)
        {
            var issues = new List<DdfDotNetContractIssue>();
            var packaged = new List<DdfDotNetPackagedReference>();
            var requested = new HashSet<string>((packagedReferencePaths ??
                Array.Empty<string>()).Select(path => Path.GetFullPath(path)),
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase :
                StringComparer.Ordinal);
            foreach (string path in requested.OrderBy(value => value,
                         StringComparer.OrdinalIgnoreCase))
            {
                DdfDotNetReferenceDescriptor descriptor = referenceSet?
                    .References.SingleOrDefault(reference => string.Equals(
                        Path.GetFullPath(reference.Path), path,
                        OperatingSystem.IsWindows() ?
                            StringComparison.OrdinalIgnoreCase :
                            StringComparison.Ordinal));
                if (descriptor == null)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC518",
                        "Reference da includere non presente nel set verificato: " +
                        path + "."));
                    continue;
                }
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.LongLength != descriptor.Size ||
                    DdfDotNetArtifactContract.Hash(bytes) != descriptor.Sha256)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC518",
                        "Reference modificata prima del packaging: " + path +
                        "."));
                    continue;
                }
                packaged.Add(new DdfDotNetPackagedReference(
                    descriptor.AssemblyName, descriptor.Sha256,
                    descriptor.Size, bytes));
            }
            if (issues.Count != 0)
                return new DdfDotNetLinkedBundleResult(null, issues);
            return CreateCore(program, emission, referenceSet, packaged, limits);
        }

        internal static DdfDotNetLinkedBundleResult CreateFromPackagedReferences(
            DdfDotNetLinkedProgram program,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetReferenceSet referenceSet,
            IEnumerable<DdfDotNetPackagedReference> packagedReferences,
            DdfDotNetPeVerificationLimits limits = null) => CreateCore(program,
                emission, referenceSet, packagedReferences, limits);

        private static DdfDotNetLinkedBundleResult CreateCore(
            DdfDotNetLinkedProgram program,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetReferenceSet referenceSet,
            IEnumerable<DdfDotNetPackagedReference> packagedReferences,
            DdfDotNetPeVerificationLimits limits)
        {
            var issues = new List<DdfDotNetContractIssue>();
            if (program == null || emission?.Success != true)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC506",
                    "Programma collegato ed emissione completa sono richiesti."));
                return new DdfDotNetLinkedBundleResult(null, issues);
            }
            DdfDotNetPeVerificationResult pe = DdfDotNetPeVerifier.Verify(program,
                emission.Artifacts, referenceSet, limits);
            issues.AddRange(pe.Issues);
            var byIdentity = emission.Artifacts.GroupBy(artifact =>
                    artifact.ModuleIdentity, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            foreach (DdfDotNetLinkedArtifact linked in program.Artifacts)
            {
                if (!byIdentity.TryGetValue(linked.Identity,
                        out DdfDotNetEmittedArtifact artifact))
                    continue;
                try
                {
                    byte[] canonicalManifest = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeManifest(
                            artifact.Manifest));
                    byte[] canonicalMap = Encoding.UTF8.GetBytes(
                        DdfDotNetArtifactContract.SerializeSourceMap(
                            artifact.SourceMap));
                    if (!canonicalManifest.SequenceEqual(
                            artifact.ManifestBytes) ||
                        !canonicalMap.SequenceEqual(artifact.SourceMapBytes) ||
                        !MatchesPayload(artifact,
                            DdfDotNetPayloadKind.Assembly,
                            artifact.AssemblyPath, artifact.AssemblyBytes) ||
                        !MatchesPayload(artifact,
                            DdfDotNetPayloadKind.PortablePdb,
                            artifact.PortablePdbPath,
                            artifact.PortablePdbBytes) ||
                        !MatchesPayload(artifact,
                            DdfDotNetPayloadKind.SourceMap,
                            artifact.SourceMapPath, artifact.SourceMapBytes) ||
                        DdfDotNetArtifactContract.ValidateBundle(
                            artifact.Manifest, artifact.SourceMap).Count != 0)
                        issues.Add(new DdfDotNetContractIssue("DDFC506",
                            linked.Identity +
                            ": manifest o source map non canonici."));
                }
                catch (ArgumentException exception)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC506",
                        linked.Identity + ": bundle non coerente: " +
                        exception.Message));
                }
            }
            if (issues.Count != 0)
                return new DdfDotNetLinkedBundleResult(null, issues);
            var binaries = program.Artifacts.Select(linked =>
                new DdfDotNetLinkedBinaryArtifact(
                    byIdentity[linked.Identity])).ToList();
            List<DdfDotNetPackagedReference> packaged = (packagedReferences ??
                Array.Empty<DdfDotNetPackagedReference>()).ToList();
            foreach (DdfDotNetPackagedReference reference in packaged)
            {
                DdfDotNetReferenceDescriptor locked = referenceSet.References
                    .SingleOrDefault(item => item.AssemblyName ==
                        reference.AssemblyIdentity);
                if (locked == null || locked.Sha256 != reference.Sha256 ||
                    locked.Size != reference.Size ||
                    DdfDotNetArtifactContract.Hash(
                        reference.GetAssemblyBytes()) != reference.Sha256)
                    issues.Add(new DdfDotNetContractIssue("DDFC518",
                        "Payload reference gestita non coerente con il lock: " +
                        reference.AssemblyIdentity + "."));
            }
            if (packaged.Select(item => item.AssemblyIdentity).Distinct(
                    StringComparer.Ordinal).Count() != packaged.Count)
                issues.Add(new DdfDotNetContractIssue("DDFC518",
                    "Payload reference gestite duplicate."));
            if (issues.Count != 0)
                return new DdfDotNetLinkedBundleResult(null, issues);
            return new DdfDotNetLinkedBundleResult(
                new DdfDotNetLinkedBundle(program, binaries, referenceSet,
                    packaged), null);
        }

        private static bool MatchesPayload(DdfDotNetEmittedArtifact artifact,
            DdfDotNetPayloadKind kind, string path, byte[] bytes)
        {
            DdfDotNetPayloadDescriptor expected = artifact.Manifest.Payloads
                .SingleOrDefault(payload => payload.Kind == kind);
            DdfDotNetPayloadDescriptor actual =
                DdfDotNetPayloadDescriptor.FromBytes(kind,
                    Path.GetFileName(path), bytes);
            return expected != null && expected.Path == actual.Path &&
                expected.Size == actual.Size &&
                expected.Sha256 == actual.Sha256;
        }
    }
}
