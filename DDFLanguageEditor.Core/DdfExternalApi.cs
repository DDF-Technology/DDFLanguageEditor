using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExternalApiParameterDefinition
    {
        public DdfExternalApiParameterDefinition(string name, string typeName,
            string documentation = null)
        {
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            TypeName = DdfHostCatalogValidation.TypeName(typeName,
                nameof(typeName));
            Documentation = documentation ?? string.Empty;
        }

        public string Name { get; }
        public string TypeName { get; }
        public string Documentation { get; }
    }

    public sealed class DdfExternalApiObjectDefinition
    {
        public DdfExternalApiObjectDefinition(string name, string bindingId,
            string typeName, string documentation = null,
            DdfHostValueAccess access = DdfHostValueAccess.ReadOnly,
            DdfHostObjectLifetime lifetime =
                DdfHostObjectLifetime.ExecutionSnapshot,
            IEnumerable<string> requiredPermissions = null)
        {
            if (!Enum.IsDefined(typeof(DdfHostValueAccess), access))
                throw new ArgumentOutOfRangeException(nameof(access));
            if (!Enum.IsDefined(typeof(DdfHostObjectLifetime), lifetime))
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            BindingId = DdfHostCatalogValidation.Identifier(bindingId,
                nameof(bindingId));
            TypeName = DdfHostCatalogValidation.TypeName(typeName,
                nameof(typeName));
            Documentation = documentation ?? string.Empty;
            Access = access;
            Lifetime = lifetime;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
        }

        public string Name { get; }
        public string BindingId { get; }
        public string TypeName { get; }
        public string Documentation { get; }
        public DdfHostValueAccess Access { get; }
        public DdfHostObjectLifetime Lifetime { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
    }

    public sealed class DdfExternalApiFunctionDefinition
    {
        public DdfExternalApiFunctionDefinition(string name, string bindingId,
            string returnType,
            IEnumerable<DdfExternalApiParameterDefinition> parameters = null,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null,
            DdfHostFunctionEffect effect = DdfHostFunctionEffect.Pure)
        {
            if (!Enum.IsDefined(typeof(DdfHostFunctionEffect), effect))
                throw new ArgumentOutOfRangeException(nameof(effect));
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            BindingId = DdfHostCatalogValidation.Identifier(bindingId,
                nameof(bindingId));
            ReturnType = DdfHostCatalogValidation.TypeName(returnType,
                nameof(returnType));
            Parameters = DdfHostCatalogValidation.Unique(parameters,
                item => item?.Name, nameof(parameters));
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
            Effect = effect;
        }

        public string Name { get; }
        public string BindingId { get; }
        public string ReturnType { get; }
        public IReadOnlyList<DdfExternalApiParameterDefinition> Parameters {
            get;
        }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public DdfHostFunctionEffect Effect { get; }
    }

    /// <summary>
    /// Immutable, executable-code-free description of the DDF surface exposed
    /// by one resolved external reference. No CLR type or reflection metadata
    /// crosses this boundary.
    /// </summary>
    public sealed class DdfExternalApiDefinition
    {
        public const string FormatVersion = "1.0";

        public DdfExternalApiDefinition(string referenceId,
            string referenceVersion,
            IEnumerable<DdfHostPermissionDefinition> permissions = null,
            IEnumerable<DdfHostTypeDefinition> types = null,
            IEnumerable<DdfExternalApiObjectDefinition> objects = null,
            IEnumerable<DdfExternalApiFunctionDefinition> functions = null)
        {
            ReferenceId = LogicalId(referenceId, nameof(referenceId));
            ReferenceVersion = DdfHostCatalogValidation.Text(referenceVersion,
                nameof(referenceVersion));
            if (!Version.TryParse(ReferenceVersion, out _))
                throw new ArgumentException("Versione API esterna non valida: " +
                    ReferenceVersion + ".", nameof(referenceVersion));
            Permissions = DdfHostCatalogValidation.Unique(permissions,
                item => item?.Id, nameof(permissions));
            Types = DdfHostCatalogValidation.Unique(types,
                item => item?.Name, nameof(types));
            Objects = DdfHostCatalogValidation.Unique(objects,
                item => item?.Name, nameof(objects));
            Functions = DdfHostCatalogValidation.Unique(functions,
                item => item?.Name, nameof(functions));
            Validate();
            Fingerprint = ComputeFingerprint();
        }

        public string ReferenceId { get; }
        public string ReferenceVersion { get; }
        public IReadOnlyList<DdfHostPermissionDefinition> Permissions { get; }
        public IReadOnlyList<DdfHostTypeDefinition> Types { get; }
        public IReadOnlyList<DdfExternalApiObjectDefinition> Objects { get; }
        public IReadOnlyList<DdfExternalApiFunctionDefinition> Functions { get; }
        public string Fingerprint { get; }
        public bool IsEmpty => Types.Count == 0 && Objects.Count == 0 &&
                               Functions.Count == 0;

        public static DdfExternalApiDefinition Empty(string referenceId,
            string referenceVersion) => new(referenceId, referenceVersion);

        private static string LogicalId(string value, string parameterName)
        {
            string result = DdfHostCatalogValidation.Text(value,
                parameterName);
            if (!Regex.IsMatch(result, "^[a-z][a-z0-9.-]*$"))
                throw new ArgumentException(
                    "Identità API esterna non valida: " + result + ".",
                    parameterName);
            return result;
        }

        private void Validate()
        {
            string duplicateName = Objects.Select(item => item.Name).Intersect(
                Functions.Select(item => item.Name), StringComparer.Ordinal)
                .FirstOrDefault();
            if (duplicateName != null) throw new ArgumentException(
                "Nome API esterna duplicato tra oggetto e funzione: " +
                duplicateName + ".");
            string duplicateBinding = Objects.Select(item => item.BindingId)
                .Concat(Functions.Select(item => item.BindingId))
                .GroupBy(item => item, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateBinding != null) throw new ArgumentException(
                "Binding API esterno duplicato: " + duplicateBinding + ".");
            var permissionIds = new HashSet<string>(Permissions.Select(item =>
                item.Id), StringComparer.Ordinal);
            string missingPermission = Objects.SelectMany(item =>
                    item.RequiredPermissions)
                .Concat(Functions.SelectMany(item => item.RequiredPermissions))
                .FirstOrDefault(item => !permissionIds.Contains(item));
            if (missingPermission != null) throw new ArgumentException(
                "Permesso API esterno non dichiarato: " + missingPermission +
                ".");
            var knownTypes = new HashSet<string>(new[]
            {
                "void", "bool", "int", "float", "string"
            }, StringComparer.Ordinal);
            foreach (DdfHostTypeDefinition type in Types)
                knownTypes.Add(type.Name);
            string unknownType = Objects.Select(item => item.TypeName)
                .Concat(Functions.Select(item => item.ReturnType))
                .Concat(Functions.SelectMany(item => item.Parameters)
                    .Select(item => item.TypeName))
                .Concat(Types.SelectMany(item => item.Fields)
                    .Select(item => item.TypeName))
                .FirstOrDefault(item => !IsKnownType(item, knownTypes));
            if (unknownType != null) throw new ArgumentException(
                "Tipo API esterno non dichiarato: " + unknownType + ".");
        }

        private static bool IsKnownType(string typeName,
            ISet<string> knownTypes)
        {
            string current = typeName;
            while (current.EndsWith("[]", StringComparison.Ordinal))
                current = current.Substring(0, current.Length - 2);
            return knownTypes.Contains(current);
        }

        private string ComputeFingerprint()
        {
            var text = new StringBuilder();
            text.Append(FormatVersion).Append('\n').Append(ReferenceId)
                .Append('@').Append(ReferenceVersion).Append('\n');
            foreach (DdfHostPermissionDefinition permission in Permissions
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
                text.Append("permission:").Append(permission.Id).Append(':')
                    .Append(permission.DisplayName).Append(':')
                    .Append(permission.Documentation).Append('\n');
            foreach (DdfHostTypeDefinition type in Types.OrderBy(item =>
                         item.Name, StringComparer.Ordinal))
            {
                text.Append("type:").Append(type.Name).Append(':')
                    .Append(type.Documentation).Append('\n');
                foreach (DdfHostFieldDefinition field in type.Fields.OrderBy(
                             item => item.Name, StringComparer.Ordinal))
                    text.Append("field:").Append(field.Name).Append(':')
                        .Append(field.TypeName).Append(':').Append(field.Access)
                        .Append(':').Append(field.MemberKind).Append(':')
                        .Append(field.Documentation).Append('\n');
                foreach (DdfHostEventDefinition eventDefinition in type.Events
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                    text.Append("event:").Append(eventDefinition.Name)
                        .Append(':').Append(eventDefinition.HandlerSignature)
                        .Append(':').Append(eventDefinition.Documentation)
                        .Append('\n');
            }
            foreach (DdfExternalApiObjectDefinition value in Objects.OrderBy(
                         item => item.Name, StringComparer.Ordinal))
                text.Append("object:").Append(value.Name).Append(':')
                    .Append(value.BindingId).Append(':').Append(value.TypeName)
                    .Append(':').Append(value.Access).Append(':')
                    .Append(value.Lifetime).Append(':')
                    .Append(string.Join(",", value.RequiredPermissions))
                    .Append(':').Append(value.Documentation).Append('\n');
            foreach (DdfExternalApiFunctionDefinition function in Functions
                         .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                text.Append("function:").Append(function.Name).Append(':')
                    .Append(function.BindingId).Append(':')
                    .Append(function.ReturnType).Append(':')
                    .Append(function.Effect).Append(':')
                    .Append(string.Join(",", function.RequiredPermissions))
                    .Append(':').Append(function.Documentation).Append('\n');
                foreach (DdfExternalApiParameterDefinition parameter in
                         function.Parameters)
                    text.Append("parameter:").Append(parameter.Name).Append(':')
                        .Append(parameter.TypeName).Append(':')
                        .Append(parameter.Documentation).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                text.ToString()))).ToLowerInvariant();
        }
    }

    public interface IDdfExternalApiRuntimeAdapter
    {
        string AdapterId { get; }
        object ReadObject(string bindingId);
        void WriteObject(string bindingId, object value);
        ValueTask<object> InvokeAsync(DdfHostInvocationContext context,
            string bindingId, IReadOnlyList<object> arguments);
    }

    public sealed class DdfExternalApiUnavailableException :
        InvalidOperationException
    {
        public DdfExternalApiUnavailableException(string referenceId) : base(
            "L'API esterna '" + (referenceId ?? string.Empty) +
            "' è disponibile soltanto per l'analisi: manca un adapter runtime.")
        {
            ReferenceId = referenceId ?? string.Empty;
        }

        public string ReferenceId { get; }
    }

    /// <summary>
    /// Backend-neutral execution context used by trusted bridges. It keeps the
    /// internal host invocation context out of adapter consumers while
    /// preserving cancellation, deadlines, transactions and resource cleanup.
    /// </summary>
    public sealed class DdfExternalApiInvocationContext
    {
        private readonly Action<IDisposable> registerResource;
        private readonly Action<IAsyncDisposable> registerAsyncResource;

        public DdfExternalApiInvocationContext(Guid executionId,
            CancellationToken cancellationToken, DateTimeOffset startedAt,
            DateTimeOffset? deadline = null,
            Action<IDisposable> registerResource = null,
            Action<IAsyncDisposable> registerAsyncResource = null,
            IReadOnlyDictionary<string, string> databaseTransactions = null,
            string hostEffectTransactionId = null,
            DdfEditorExecutionMode mode = DdfEditorExecutionMode.HostEffects)
        {
            ExecutionId = executionId;
            CancellationToken = cancellationToken;
            StartedAt = startedAt;
            Deadline = deadline;
            this.registerResource = registerResource;
            this.registerAsyncResource = registerAsyncResource;
            DatabaseTransactions = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(databaseTransactions ??
                    new Dictionary<string, string>(), StringComparer.Ordinal));
            HostEffectTransactionId = hostEffectTransactionId ?? string.Empty;
            if (!Enum.IsDefined(typeof(DdfEditorExecutionMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
        }

        public Guid ExecutionId { get; }
        public CancellationToken CancellationToken { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? Deadline { get; }
        public IReadOnlyDictionary<string, string> DatabaseTransactions { get; }
        public string HostEffectTransactionId { get; }
        public DdfEditorExecutionMode Mode { get; }

        internal void Register(object resource)
        {
            if (resource is IAsyncDisposable asynchronous)
            {
                if (registerAsyncResource == null)
                    throw new InvalidOperationException(
                        "Il bridge API esterno non accetta risorse asincrone.");
                registerAsyncResource(asynchronous);
                return;
            }
            if (resource is IDisposable disposable)
            {
                if (registerResource == null)
                    throw new InvalidOperationException(
                        "Il bridge API esterno non accetta risorse sincrone.");
                registerResource(disposable);
                return;
            }
            throw new ArgumentException(
                "La risorsa API esterna deve essere rilasciabile.",
                nameof(resource));
        }
    }

    public static class DdfExternalApiRuntimeInvoker
    {
        public static ValueTask<object> InvokeAsync(
            IDdfExternalApiRuntimeAdapter adapter, string bindingId,
            IReadOnlyList<object> arguments,
            DdfExternalApiInvocationContext context)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var hostContext = new DdfHostInvocationContext(context.ExecutionId,
                context.Mode, context.CancellationToken, context.StartedAt,
                context.Deadline, context.Register,
                context.DatabaseTransactions,
                context.HostEffectTransactionId);
            return adapter.InvokeAsync(hostContext, bindingId,
                arguments ?? Array.Empty<object>());
        }
    }

    /// <summary>
    /// Projects the same declarative API into the existing shared host catalog.
    /// Analysis may use a metadata-only projection; execution requires an exact
    /// runtime adapter and never performs discovery or reflection.
    /// </summary>
    public static class DdfExternalApiCatalogProjector
    {
        public static DdfHostCatalogSnapshot CreateAnalysisCatalog(
            DdfExternalApiDefinition definition,
            IEnumerable<string> grantedPermissions = null) => Create(definition,
                null, null, grantedPermissions, false);

        public static DdfHostCatalogSnapshot CreateRuntimeCatalog(
            DdfExternalApiDefinition definition, string expectedAdapterId,
            IDdfExternalApiRuntimeAdapter adapter,
            IEnumerable<string> grantedPermissions = null) => Create(definition,
                expectedAdapterId, adapter, grantedPermissions, true);

        public static void RegisterAnalysisCatalog(DdfEditorHostCatalog catalog,
            DdfExternalApiDefinition definition,
            IEnumerable<string> grantedPermissions = null) => Register(catalog,
                definition, null, null, grantedPermissions, false);

        public static void RegisterRuntimeCatalog(DdfEditorHostCatalog catalog,
            DdfExternalApiDefinition definition, string expectedAdapterId,
            IDdfExternalApiRuntimeAdapter adapter,
            IEnumerable<string> grantedPermissions = null) => Register(catalog,
                definition, expectedAdapterId, adapter, grantedPermissions,
                true);

        private static DdfHostCatalogSnapshot Create(
            DdfExternalApiDefinition definition, string expectedAdapterId,
            IDdfExternalApiRuntimeAdapter adapter,
            IEnumerable<string> grantedPermissions, bool runtime)
        {
            if (definition == null) throw new ArgumentNullException(
                nameof(definition));
            if (runtime)
            {
                if (adapter == null) throw new ArgumentNullException(
                    nameof(adapter));
                string expected = DdfHostCatalogValidation.Identifier(
                    expectedAdapterId, nameof(expectedAdapterId));
                if (!string.Equals(expected, adapter.AdapterId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "L'adapter runtime non coincide con il binding risolto.",
                        nameof(adapter));
            }
            var catalog = new DdfEditorHostCatalog();
            Register(catalog, definition, expectedAdapterId, adapter,
                grantedPermissions, runtime);
            return catalog.Snapshot;
        }

        private static void Register(DdfEditorHostCatalog catalog,
            DdfExternalApiDefinition definition, string expectedAdapterId,
            IDdfExternalApiRuntimeAdapter adapter,
            IEnumerable<string> grantedPermissions, bool runtime)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (DdfHostPermissionDefinition permission in
                     definition.Permissions)
                catalog.RegisterPermission(permission);
            foreach (DdfHostTypeDefinition type in definition.Types)
                catalog.RegisterType(type);
            foreach (DdfExternalApiObjectDefinition value in definition.Objects)
                catalog.RegisterObject(new DdfHostObjectDefinition(value.Name,
                    value.TypeName,
                    runtime ? () => adapter.ReadObject(value.BindingId) :
                        () => throw Unavailable(definition.ReferenceId),
                    value.Documentation, value.RequiredPermissions,
                    value.Access == DdfHostValueAccess.ReadWrite
                        ? runtime
                            ? new Action<object>(item => adapter.WriteObject(
                                value.BindingId, item))
                            : item => throw Unavailable(definition.ReferenceId)
                        : null,
                    value.Lifetime));
            foreach (DdfExternalApiFunctionDefinition function in
                     definition.Functions)
                catalog.RegisterFunction(new DdfHostFunctionDefinition(
                    function.Name, function.ReturnType,
                    function.Parameters.Select(item =>
                        new DdfHostParameterDefinition(item.Name,
                            item.TypeName, item.Documentation)), null,
                    function.Documentation, function.RequiredPermissions,
                    effect: function.Effect,
                    invokeAsync: runtime
                        ? (context, arguments) => adapter.InvokeAsync(context,
                            function.BindingId, arguments)
                        : (context, arguments) => ValueTask.FromException<object>(
                            Unavailable(definition.ReferenceId))));
            foreach (string permission in grantedPermissions ??
                     Array.Empty<string>())
                catalog.SetPermission(permission, true);
        }

        private static DdfExternalApiUnavailableException Unavailable(
            string id) => new(id);
    }
}
