using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Storage
{
    public enum DdfLogicalFileFormat
    {
        Utf8Text,
        Json,
        Csv
    }

    public enum DdfLogicalFileAccess
    {
        ReadOnly,
        ReadWrite
    }

    public enum DdfLogicalFileOperationKind
    {
        Read,
        Write
    }

    public sealed class DdfLogicalFileDefinition
    {
        public DdfLogicalFileDefinition(string id, string relativePath,
            DdfLogicalFileFormat format = DdfLogicalFileFormat.Utf8Text,
            DdfLogicalFileAccess access = DdfLogicalFileAccess.ReadOnly,
            int maxBytes = 1024 * 1024)
        {
            Id = Identifier(id, nameof(id));
            RelativePath = ValidateRelativePath(relativePath);
            if (!Enum.IsDefined(typeof(DdfLogicalFileFormat), format))
                throw new ArgumentOutOfRangeException(nameof(format));
            if (!Enum.IsDefined(typeof(DdfLogicalFileAccess), access))
                throw new ArgumentOutOfRangeException(nameof(access));
            if (maxBytes <= 0 || maxBytes > 16 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            Format = format;
            Access = access;
            MaxBytes = maxBytes;
        }

        public string Id { get; }
        public string RelativePath { get; }
        public DdfLogicalFileFormat Format { get; }
        public DdfLogicalFileAccess Access { get; }
        public int MaxBytes { get; }

        internal static string Identifier(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0 ||
                !(char.IsLetter(result[0]) || result[0] == '_') ||
                result.Skip(1).Any(character =>
                    !(char.IsLetterOrDigit(character) || character == '_' ||
                      character == '.')))
                throw new ArgumentException("Identificatore file logico non valido: " +
                    result + ".", parameterName);
            return result;
        }

        private static string ValidateRelativePath(string value)
        {
            string result = (value ?? string.Empty).Trim()
                .Replace(Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar);
            if (result.Length == 0 || Path.IsPathRooted(result) ||
                result.Split(Path.DirectorySeparatorChar).Any(segment =>
                    segment.Length == 0 || segment == "." || segment == "..") ||
                result.StartsWith(".ddf-file-staging" +
                    Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result, ".ddf-file-staging",
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Il percorso host deve essere relativo e confinato.",
                    nameof(value));
            return result;
        }
    }

    public sealed class DdfLogicalFileOperationDefinition
    {
        public DdfLogicalFileOperationDefinition(string functionName,
            string bindingId, string resourceId,
            DdfLogicalFileOperationKind kind, string documentation = null,
            IEnumerable<string> requiredPermissions = null)
        {
            FunctionName = DdfLogicalFileDefinition.Identifier(functionName,
                nameof(functionName));
            BindingId = DdfLogicalFileDefinition.Identifier(bindingId,
                nameof(bindingId));
            ResourceId = DdfLogicalFileDefinition.Identifier(resourceId,
                nameof(resourceId));
            if (!Enum.IsDefined(typeof(DdfLogicalFileOperationKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = (requiredPermissions ?? Array.Empty<string>())
                .Select(item => DdfLogicalFileDefinition.Identifier(item,
                    nameof(requiredPermissions)))
                .Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public string FunctionName { get; }
        public string BindingId { get; }
        public string ResourceId { get; }
        public DdfLogicalFileOperationKind Kind { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }

        internal DdfHostFunctionDefinition ToHostFunction(
            DdfGovernedFileSystemAdapter adapter) => new(FunctionName,
                Kind == DdfLogicalFileOperationKind.Read ? "string" : "bool",
                Kind == DdfLogicalFileOperationKind.Read
                    ? Array.Empty<DdfHostParameterDefinition>()
                    : new[] { new DdfHostParameterDefinition("content", "string") },
                null, Documentation, RequiredPermissions,
                effect: Kind == DdfLogicalFileOperationKind.Read
                    ? DdfHostFunctionEffect.Pure
                    : DdfHostFunctionEffect.HostMutation,
                invokeAsync: (context, arguments) => adapter.InvokeAsync(context,
                    BindingId, arguments));

        internal DdfExternalApiFunctionDefinition ToExternalFunction(
            IEnumerable<string> inheritedPermissions) => new(FunctionName,
                BindingId,
                Kind == DdfLogicalFileOperationKind.Read ? "string" : "bool",
                Kind == DdfLogicalFileOperationKind.Read
                    ? Array.Empty<DdfExternalApiParameterDefinition>()
                    : new[] { new DdfExternalApiParameterDefinition("content", "string") },
                Documentation, (inheritedPermissions ?? Array.Empty<string>())
                    .Concat(RequiredPermissions).Distinct(StringComparer.Ordinal),
                Kind == DdfLogicalFileOperationKind.Read
                    ? DdfHostFunctionEffect.Pure
                    : DdfHostFunctionEffect.HostMutation);
    }

    /// <summary>
    /// Maps host-declared operations to logical files beneath one private root.
    /// Scripts cannot supply, enumerate or observe physical paths.
    /// </summary>
    public sealed class DdfGovernedFileSystemAdapter :
        IDdfExternalApiRuntimeAdapter, IDisposable
    {
        private sealed class TransactionState
        {
            internal TransactionState(string id, string directory)
            {
                Id = id;
                Directory = directory;
            }

            internal string Id { get; }
            internal string Directory { get; }
            internal Dictionary<string, string> StagedPaths { get; } =
                new(StringComparer.Ordinal);
            internal long TotalBytes { get; set; }
        }

        private readonly string rootPath;
        private readonly string stagingRoot;
        private readonly IReadOnlyDictionary<string, DdfLogicalFileDefinition>
            resources;
        private readonly IReadOnlyDictionary<string,
            DdfLogicalFileOperationDefinition> operations;
        private readonly Dictionary<string, TransactionState> transactions =
            new(StringComparer.Ordinal);
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly int maxTransactionBytes;
        private int disposed;

        public DdfGovernedFileSystemAdapter(string adapterId, string rootPath,
            IEnumerable<DdfLogicalFileDefinition> resources,
            IEnumerable<DdfLogicalFileOperationDefinition> operations,
            int maxTransactionBytes = 16 * 1024 * 1024)
        {
            AdapterId = DdfLogicalFileDefinition.Identifier(adapterId,
                nameof(adapterId));
            if (string.IsNullOrWhiteSpace(rootPath) ||
                !Path.IsPathFullyQualified(rootPath))
                throw new ArgumentException(
                    "La radice filesystem deve essere assoluta.", nameof(rootPath));
            this.rootPath = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            stagingRoot = Path.Combine(this.rootPath, ".ddf-file-staging");
            if (maxTransactionBytes <= 0 ||
                maxTransactionBytes > 64 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maxTransactionBytes));
            this.maxTransactionBytes = maxTransactionBytes;

            DdfLogicalFileDefinition[] resourceArray = (resources ??
                Array.Empty<DdfLogicalFileDefinition>()).ToArray();
            DdfLogicalFileOperationDefinition[] operationArray = (operations ??
                Array.Empty<DdfLogicalFileOperationDefinition>()).ToArray();
            if (resourceArray.Length == 0 || resourceArray.Any(item => item == null))
                throw new ArgumentException(
                    "È richiesta almeno una risorsa file valida.", nameof(resources));
            if (operationArray.Length == 0 || operationArray.Any(item => item == null))
                throw new ArgumentException(
                    "È richiesta almeno un'operazione file valida.", nameof(operations));
            EnsureUnique(resourceArray.Select(item => item.Id), nameof(resources));
            EnsureUnique(resourceArray.Select(item => item.RelativePath),
                nameof(resources), StringComparer.OrdinalIgnoreCase);
            EnsureUnique(operationArray.Select(item => item.BindingId),
                nameof(operations));
            EnsureUnique(operationArray.Select(item => item.FunctionName),
                nameof(operations));
            this.resources = new ReadOnlyDictionary<string,
                DdfLogicalFileDefinition>(resourceArray.ToDictionary(item =>
                    item.Id, StringComparer.Ordinal));
            foreach (DdfLogicalFileOperationDefinition operation in operationArray)
            {
                if (!this.resources.TryGetValue(operation.ResourceId,
                        out DdfLogicalFileDefinition resource))
                    throw new ArgumentException(
                        "Risorsa file logica non dichiarata: " +
                        operation.ResourceId + ".", nameof(operations));
                if (operation.Kind == DdfLogicalFileOperationKind.Write &&
                    resource.Access != DdfLogicalFileAccess.ReadWrite)
                    throw new ArgumentException(
                        "La risorsa file è di sola lettura: " + resource.Id + ".",
                        nameof(operations));
                ResolvePath(resource);
            }
            this.operations = new ReadOnlyDictionary<string,
                DdfLogicalFileOperationDefinition>(operationArray.ToDictionary(
                    item => item.BindingId, StringComparer.Ordinal));
        }

        public string AdapterId { get; }
        public IReadOnlyCollection<DdfLogicalFileDefinition> Resources =>
            resources.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<DdfLogicalFileOperationDefinition> Operations =>
            operations.Values.ToList().AsReadOnly();

        public DdfHostFunctionLibraryDefinition CreateHostLibrary(string id,
            string displayName, string documentation = null,
            IEnumerable<string> requiredPermissions = null) => new(id,
                displayName, operations.Values.Select(item =>
                    item.ToHostFunction(this)), documentation,
                requiredPermissions);

        public DdfExternalApiDefinition CreateExternalApiDefinition(
            string referenceId, string referenceVersion,
            IEnumerable<DdfHostPermissionDefinition> permissions = null,
            IEnumerable<string> requiredPermissions = null) => new(referenceId,
                referenceVersion, permissions: permissions,
                functions: operations.Values.Select(item =>
                    item.ToExternalFunction(requiredPermissions)));

        public IDdfHostEffectTransactionFactory CreateTransactionFactory() =>
            new TransactionFactory(this);

        object IDdfExternalApiRuntimeAdapter.ReadObject(string bindingId) =>
            throw new NotSupportedException(
                "L'adapter filesystem non espone oggetti globali.");

        void IDdfExternalApiRuntimeAdapter.WriteObject(string bindingId,
            object value) => throw new NotSupportedException(
                "L'adapter filesystem non espone oggetti globali.");

        public async ValueTask<object> InvokeAsync(DdfHostInvocationContext context,
            string bindingId, IReadOnlyList<object> arguments)
        {
            ThrowIfDisposed();
            if (!operations.TryGetValue(bindingId ?? string.Empty,
                    out DdfLogicalFileOperationDefinition operation))
                throw new InvalidOperationException(
                    "Operazione file non autorizzata: " + bindingId + ".");
            if (context == null) throw new ArgumentNullException(nameof(context));
            DdfLogicalFileDefinition resource = resources[operation.ResourceId];
            int expectedArguments = operation.Kind ==
                DdfLogicalFileOperationKind.Read ? 0 : 1;
            if ((arguments?.Count ?? 0) != expectedArguments)
                throw new ArgumentException(
                    "Numero di argomenti file non valido.", nameof(arguments));
            using CancellationTokenSource timeout = CreateTimeout(context);
            if (operation.Kind == DdfLogicalFileOperationKind.Read)
                return await ReadAsync(resource, context.HostEffectTransactionId,
                    timeout.Token);
            string content = Convert.ToString(arguments[0]) ?? string.Empty;
            byte[] payload = EncodeAndValidate(resource, content);
            if (context.HasHostEffectTransaction)
                await StageAsync(context.HostEffectTransactionId, resource,
                    payload, timeout.Token);
            else
                await WriteAtomicAsync(resource, payload, timeout.Token);
            return true;
        }

        private async Task<string> ReadAsync(DdfLogicalFileDefinition resource,
            string transactionId, CancellationToken cancellationToken)
        {
            string path = null;
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(transactionId) &&
                    transactions.TryGetValue(transactionId, out TransactionState state))
                    state.StagedPaths.TryGetValue(resource.Id, out path);
            }
            finally { gate.Release(); }
            path ??= ResolvePath(resource);
            byte[] payload;
            try
            {
                payload = await File.ReadAllBytesAsync(path, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                throw new InvalidOperationException(
                    "Risorsa file logica non disponibile: " + resource.Id + ".");
            }
            catch (DirectoryNotFoundException)
            {
                throw new InvalidOperationException(
                    "Risorsa file logica non disponibile: " + resource.Id + ".");
            }
            catch (Exception exception) when (IsFileSystemError(exception))
            {
                throw new InvalidOperationException(
                    "Impossibile leggere la risorsa file logica: " +
                    resource.Id + ".");
            }
            if (payload.Length > resource.MaxBytes)
                throw new InvalidOperationException(
                    "Risorsa file oltre la quota autorizzata: " + resource.Id + ".");
            string content = DecodeUtf8(resource.Id, payload);
            return NormalizeAndValidate(resource, content);
        }

        private async Task StageAsync(string transactionId,
            DdfLogicalFileDefinition resource, byte[] payload,
            CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (!transactions.TryGetValue(transactionId,
                        out TransactionState state))
                    throw new InvalidOperationException(
                        "Transazione filesystem non riconosciuta.");
                try
                {
                    long previous = 0;
                    if (state.StagedPaths.TryGetValue(resource.Id,
                            out string existing) && File.Exists(existing))
                        previous = new FileInfo(existing).Length;
                    if (state.TotalBytes - previous + payload.Length >
                        maxTransactionBytes)
                        throw new InvalidOperationException(
                            "La transazione filesystem supera la quota autorizzata.");
                    Directory.CreateDirectory(state.Directory);
                    string staged = Path.Combine(state.Directory,
                        resource.Id.Replace('.', '_') + ".stage");
                    await File.WriteAllBytesAsync(staged, payload,
                        cancellationToken);
                    state.StagedPaths[resource.Id] = staged;
                    state.TotalBytes = state.TotalBytes - previous + payload.Length;
                }
                catch (Exception exception) when (IsFileSystemError(exception))
                {
                    throw new InvalidOperationException(
                        "Impossibile preparare la risorsa file logica: " +
                        resource.Id + ".");
                }
            }
            finally { gate.Release(); }
        }

        private async Task WriteAtomicAsync(DdfLogicalFileDefinition resource,
            byte[] payload, CancellationToken cancellationToken)
        {
            string target = ResolvePath(resource);
            string directory = Path.GetDirectoryName(target);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".ddf-write-" +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, payload,
                    cancellationToken);
                File.Move(temporary, target, true);
            }
            catch (Exception exception) when (IsFileSystemError(exception))
            {
                throw new InvalidOperationException(
                    "Impossibile scrivere la risorsa file logica: " +
                    resource.Id + ".");
            }
            finally
            {
                SafeDeleteFile(temporary);
            }
        }

        private async ValueTask<IDdfHostEffectTransaction> BeginAsync(
            DdfHostEffectTransactionContext context)
        {
            ThrowIfDisposed();
            if (context == null) throw new ArgumentNullException(nameof(context));
            string id = "fs-" + context.ExecutionId.ToString("N") + "-" +
                context.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string directory = Path.Combine(stagingRoot, id);
            await gate.WaitAsync(context.CancellationToken);
            try
            {
                if (transactions.ContainsKey(id))
                    throw new InvalidOperationException(
                        "Transazione filesystem già attiva.");
                try
                {
                    Directory.CreateDirectory(directory);
                    transactions.Add(id, new TransactionState(id, directory));
                }
                catch (Exception exception) when (IsFileSystemError(exception))
                {
                    throw new InvalidOperationException(
                        "Impossibile iniziare la transazione filesystem.");
                }
            }
            finally { gate.Release(); }
            return new FileTransaction(this, id);
        }

        private async ValueTask CommitAsync(string id)
        {
            ThrowIfDisposed();
            await gate.WaitAsync();
            try
            {
                if (!transactions.TryGetValue(id, out TransactionState state))
                    throw new InvalidOperationException(
                        "Transazione filesystem non riconosciuta.");
                string backupDirectory = Path.Combine(state.Directory, "backup");
                var applied = new List<(string Target, string Backup,
                    bool Existed)>();
                try
                {
                    foreach (KeyValuePair<string, string> entry in
                             state.StagedPaths.OrderBy(item => item.Key,
                                 StringComparer.Ordinal))
                    {
                        DdfLogicalFileDefinition resource = resources[entry.Key];
                        string target = ResolvePath(resource);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        Directory.CreateDirectory(backupDirectory);
                        string backup = Path.Combine(backupDirectory,
                            resource.Id.Replace('.', '_') + ".bak");
                        bool existed = File.Exists(target);
                        if (existed) File.Copy(target, backup, true);
                        File.Move(entry.Value, target, true);
                        applied.Add((target, backup, existed));
                    }
                }
                catch (Exception exception)
                {
                    foreach (var item in applied.AsEnumerable().Reverse())
                    {
                        try
                        {
                            if (item.Existed)
                                File.Copy(item.Backup, item.Target, true);
                            else SafeDeleteFile(item.Target);
                        }
                        catch (Exception restoreError) when (
                            IsFileSystemError(restoreError)) { }
                    }
                    if (IsFileSystemError(exception))
                        throw new InvalidOperationException(
                            "Commit della transazione filesystem non riuscito.");
                    throw;
                }
                finally
                {
                    transactions.Remove(id);
                    SafeDeleteDirectory(state.Directory);
                }
            }
            finally { gate.Release(); }
        }

        private async ValueTask RollbackAsync(string id)
        {
            await gate.WaitAsync();
            try
            {
                if (!transactions.Remove(id, out TransactionState state)) return;
                try { DeleteTransactionDirectory(state.Directory); }
                catch (Exception exception) when (IsFileSystemError(exception))
                {
                    throw new InvalidOperationException(
                        "Rollback della transazione filesystem non riuscito.");
                }
            }
            finally { gate.Release(); }
        }

        private byte[] EncodeAndValidate(DdfLogicalFileDefinition resource,
            string content)
        {
            string normalized = NormalizeAndValidate(resource, content);
            byte[] payload = new UTF8Encoding(false, true).GetBytes(normalized);
            if (payload.Length > resource.MaxBytes)
                throw new InvalidOperationException(
                    "Contenuto file oltre la quota autorizzata: " +
                    resource.Id + ".");
            return payload;
        }

        private static string NormalizeAndValidate(
            DdfLogicalFileDefinition resource, string content)
        {
            if (resource.Format == DdfLogicalFileFormat.Json)
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(content,
                        new JsonDocumentOptions
                        {
                            MaxDepth = 32,
                            CommentHandling = JsonCommentHandling.Disallow,
                            AllowTrailingCommas = false
                        });
                    return JsonSerializer.Serialize(document.RootElement);
                }
                catch (JsonException exception)
                {
                    throw new InvalidOperationException(
                        "JSON non valido per la risorsa logica " + resource.Id +
                        ".", exception);
                }
            }
            if (resource.Format == DdfLogicalFileFormat.Csv)
                ValidateCsv(resource.Id, content);
            return content;
        }

        private static void ValidateCsv(string resourceId, string content)
        {
            int columns = -1;
            int currentColumns = 1;
            bool quoted = false;
            bool afterQuote = false;
            bool hasData = false;
            for (int index = 0; index < content.Length; index++)
            {
                char character = content[index];
                if (character == '\0')
                    throw CsvError(resourceId);
                if (quoted)
                {
                    if (character != '"') continue;
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        index++;
                        continue;
                    }
                    quoted = false;
                    afterQuote = true;
                    continue;
                }
                if (afterQuote && character != ',' && character != '\r' &&
                    character != '\n')
                    throw CsvError(resourceId);
                if (character == '"')
                {
                    if (hasData) throw CsvError(resourceId);
                    quoted = true;
                    continue;
                }
                if (character == ',')
                {
                    currentColumns++;
                    hasData = false;
                    afterQuote = false;
                    continue;
                }
                if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < content.Length &&
                        content[index + 1] == '\n') index++;
                    CompleteCsvRow(resourceId, ref columns, currentColumns);
                    currentColumns = 1;
                    hasData = false;
                    afterQuote = false;
                    continue;
                }
                hasData = true;
            }
            if (quoted) throw CsvError(resourceId);
            if (content.Length > 0 && content[^1] != '\n' && content[^1] != '\r')
                CompleteCsvRow(resourceId, ref columns, currentColumns);
        }

        private static void CompleteCsvRow(string resourceId, ref int columns,
            int currentColumns)
        {
            if (columns < 0) columns = currentColumns;
            else if (columns != currentColumns) throw CsvError(resourceId);
        }

        private static InvalidOperationException CsvError(string resourceId) =>
            new("CSV non valido per la risorsa logica " + resourceId + ".");

        private string ResolvePath(DdfLogicalFileDefinition resource)
        {
            string resolved = Path.GetFullPath(Path.Combine(rootPath,
                resource.RelativePath));
            string prefix = rootPath + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "La risorsa file esce dalla radice autorizzata.");
            return resolved;
        }

        private static string DecodeUtf8(string resourceId, byte[] payload)
        {
            try { return new UTF8Encoding(false, true).GetString(payload); }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidOperationException(
                    "La risorsa logica non contiene UTF-8 valido: " +
                    resourceId + ".", exception);
            }
        }

        private static CancellationTokenSource CreateTimeout(
            DdfHostInvocationContext context)
        {
            var result = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken);
            if (context.Deadline.HasValue)
            {
                TimeSpan remaining = context.Deadline.Value -
                    DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) result.Cancel();
                else result.CancelAfter(remaining);
            }
            return result;
        }

        private static void EnsureUnique(IEnumerable<string> values,
            string parameterName, IEqualityComparer<string> comparer = null)
        {
            var known = new HashSet<string>(comparer ?? StringComparer.Ordinal);
            if (values.Any(value => !known.Add(value)))
                throw new ArgumentException(
                    "Identificatori o percorsi duplicati.", parameterName);
        }

        private static void DeleteTransactionDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static void SafeDeleteDirectory(string path)
        {
            try { DeleteTransactionDirectory(path); }
            catch (Exception exception) when (IsFileSystemError(exception)) { }
        }

        private static void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception) when (IsFileSystemError(exception)) { }
        }

        private static bool IsFileSystemError(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException;

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(
                    DdfGovernedFileSystemAdapter));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            gate.Wait();
            try
            {
                foreach (TransactionState state in transactions.Values)
                    SafeDeleteDirectory(state.Directory);
                transactions.Clear();
            }
            finally
            {
                gate.Release();
                gate.Dispose();
            }
        }

        private sealed class TransactionFactory :
            IDdfHostEffectTransactionFactory
        {
            private readonly DdfGovernedFileSystemAdapter owner;

            internal TransactionFactory(DdfGovernedFileSystemAdapter owner) =>
                this.owner = owner;

            public ValueTask<IDdfHostEffectTransaction> BeginAsync(
                DdfHostEffectTransactionContext context) =>
                owner.BeginAsync(context);
        }

        private sealed class FileTransaction : IDdfHostEffectTransaction
        {
            private readonly DdfGovernedFileSystemAdapter owner;
            private int completed;

            internal FileTransaction(DdfGovernedFileSystemAdapter owner,
                string id)
            {
                this.owner = owner;
                Id = id;
            }

            public string Id { get; }

            public async ValueTask CommitAsync()
            {
                if (Interlocked.CompareExchange(ref completed, 1, 0) != 0)
                    throw new InvalidOperationException(
                        "La transazione filesystem è già terminata.");
                await owner.CommitAsync(Id);
            }

            public async ValueTask RollbackAsync()
            {
                if (Interlocked.CompareExchange(ref completed, 1, 0) != 0)
                    return;
                await owner.RollbackAsync(Id);
            }

            public async ValueTask DisposeAsync()
            {
                if (Interlocked.CompareExchange(ref completed, 1, 0) == 0)
                    await owner.RollbackAsync(Id);
            }
        }
    }
}
