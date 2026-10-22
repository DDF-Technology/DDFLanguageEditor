using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfHostErrorException : Exception
    {
        public DdfHostErrorException(DdfErrorDefinition definition,
            string message, Exception innerException = null)
            : base(message ?? string.Empty, innerException)
        {
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (!definition.IsRecoverable)
                throw new ArgumentException(
                    "Un adapter host può produrre soltanto errori recuperabili dichiarati.",
                    nameof(definition));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Il messaggio è obbligatorio.",
                    nameof(message));
        }

        public DdfErrorDefinition Definition { get; }
    }

    public enum DdfHostObjectLifetime
    {
        ExecutionSnapshot,
        Live
    }

    public enum DdfHostValueAccess
    {
        ReadOnly,
        ReadWrite
    }

    public enum DdfHostFunctionEffect
    {
        Pure,
        HostMutation
    }

    public enum DdfHostFunctionDispatchTarget
    {
        Worker,
        Ui
    }

    public enum DdfHostMemberKind
    {
        Field,
        Property
    }

    public enum DdfHostValueAdapterKind
    {
        Legacy,
        Identity,
        Scalar,
        Enumeration
    }

    /// <summary>
    /// Defines the explicit boundary conversion between host values and script values.
    /// The VM never exposes its internal cells to these delegates.
    /// </summary>
    public sealed class DdfHostValueAdapter
    {
        private static readonly DdfHostValueAdapter identity = new DdfHostValueAdapter(
            value => value, value => value, DdfHostValueAdapterKind.Identity,
            string.Empty);

        [DdfEditorDeprecatedApi(
            DdfEditorApiLifecycleCatalog.LegacyAdapterConstructorId,
            "0.41", DdfEditorApiLifecycleCatalog.EarliestBreakingContractVersion,
            "DDFLanguageEditor.Core.DdfHostValueAdapter.Create<THost,TScript>(string,Func<THost,TScript>,Func<TScript,THost>)",
            "Usare Create per scalari tipizzati oppure Enumeration<TEnum> e dichiarare il tipo DDF nominale.")]
        public DdfHostValueAdapter(Func<object, object> toScriptValue,
            Func<object, object> fromScriptValue)
            : this(toScriptValue, fromScriptValue,
                DdfHostValueAdapterKind.Legacy, string.Empty)
        {
        }

        private DdfHostValueAdapter(Func<object, object> toScriptValue,
            Func<object, object> fromScriptValue,
            DdfHostValueAdapterKind kind, string scriptTypeName)
        {
            ToScriptValue = toScriptValue ?? throw new ArgumentNullException(nameof(toScriptValue));
            FromScriptValue = fromScriptValue ?? throw new ArgumentNullException(nameof(fromScriptValue));
            Kind = kind;
            ScriptTypeName = scriptTypeName ?? string.Empty;
        }

        public static DdfHostValueAdapter Identity => identity;
        public DdfHostValueAdapterKind Kind { get; }
        public string ScriptTypeName { get; }
        public bool IsTyped => Kind == DdfHostValueAdapterKind.Scalar ||
                               Kind == DdfHostValueAdapterKind.Enumeration;

        public static DdfHostValueAdapter Create<THost, TScript>(
            string scriptTypeName, Func<THost, TScript> toScriptValue,
            Func<TScript, THost> fromScriptValue)
        {
            string validatedTypeName = DdfHostCatalogValidation.TypeName(
                scriptTypeName, nameof(scriptTypeName));
            if (toScriptValue == null)
                throw new ArgumentNullException(nameof(toScriptValue));
            if (fromScriptValue == null)
                throw new ArgumentNullException(nameof(fromScriptValue));
            return new DdfHostValueAdapter(
                value => toScriptValue(RequireExact<THost>(value, "host")),
                value => fromScriptValue(RequireExact<TScript>(value, "script")),
                DdfHostValueAdapterKind.Scalar, validatedTypeName);
        }

        public static DdfHostValueAdapter Enumeration<TEnum>(
            string scriptTypeName, IReadOnlyDictionary<TEnum, string> members)
            where TEnum : struct, Enum
        {
            string validatedTypeName = DdfHostCatalogValidation.TypeName(
                scriptTypeName, nameof(scriptTypeName));
            if (members == null) throw new ArgumentNullException(nameof(members));
            if (members.Count == 0) throw new ArgumentException(
                "La tabella enum deve dichiarare almeno un membro.", nameof(members));

            var hostToScript = new Dictionary<TEnum, string>();
            var scriptToHost = new Dictionary<string, TEnum>(StringComparer.Ordinal);
            foreach (KeyValuePair<TEnum, string> member in members)
            {
                string memberName = DdfHostCatalogValidation.Identifier(
                    member.Value, nameof(members));
                if (memberName.Contains(".", StringComparison.Ordinal) ||
                    !scriptToHost.TryAdd(memberName, member.Key))
                    throw new ArgumentException(
                        "La tabella enum contiene nomi DDF non validi o duplicati.",
                        nameof(members));
                hostToScript.Add(member.Key, memberName);
            }

            return new DdfHostValueAdapter(
                value =>
                {
                    TEnum hostValue = RequireExact<TEnum>(value, "host enum");
                    if (!hostToScript.TryGetValue(hostValue, out string memberName))
                        throw new InvalidOperationException(
                            "Valore enum host non registrato per '" +
                            validatedTypeName + "'.");
                    return new DdfEnumValue(validatedTypeName, memberName);
                },
                value =>
                {
                    DdfEnumValue scriptValue = RequireExact<DdfEnumValue>(
                        value, "script enum");
                    if (!string.Equals(scriptValue.TypeName, validatedTypeName,
                            StringComparison.Ordinal) ||
                        !scriptToHost.TryGetValue(scriptValue.MemberName,
                            out TEnum hostValue))
                        throw new InvalidOperationException(
                            "Valore enum DDF non registrato per '" +
                            validatedTypeName + "'.");
                    return hostValue;
                }, DdfHostValueAdapterKind.Enumeration, validatedTypeName);
        }

        private static T RequireExact<T>(object value, string boundary)
        {
            if (value is T typedValue) return typedValue;
            string actualType = value == null ? "null" : value.GetType().FullName;
            throw new InvalidOperationException("L'adapter tipizzato richiede '" +
                typeof(T).FullName + "' al confine " + boundary +
                ", ma ha ricevuto '" + actualType + "'.");
        }

        internal Func<object, object> ToScriptValue { get; }
        internal Func<object, object> FromScriptValue { get; }
    }

    public sealed class DdfHostPermissionDefinition
    {
        public DdfHostPermissionDefinition(string id, string displayName,
            string documentation = null)
        {
            Id = DdfHostCatalogValidation.Identifier(id, nameof(id));
            DisplayName = DdfHostCatalogValidation.Text(displayName,
                nameof(displayName));
            Documentation = documentation ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Documentation { get; }
    }

    public sealed class DdfHostFieldDefinition
    {
        public DdfHostFieldDefinition(string name, string typeName,
            string documentation = null,
            DdfHostValueAccess access = DdfHostValueAccess.ReadOnly,
            DdfHostMemberKind memberKind = DdfHostMemberKind.Field)
        {
            if (!Enum.IsDefined(typeof(DdfHostValueAccess), access))
                throw new ArgumentOutOfRangeException(nameof(access));
            if (!Enum.IsDefined(typeof(DdfHostMemberKind), memberKind))
                throw new ArgumentOutOfRangeException(nameof(memberKind));
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            TypeName = DdfHostCatalogValidation.TypeName(typeName, nameof(typeName));
            Documentation = documentation ?? string.Empty;
            Access = access;
            MemberKind = memberKind;
        }

        public string Name { get; }
        public string TypeName { get; }
        public string Documentation { get; }
        public DdfHostValueAccess Access { get; }
        public DdfHostMemberKind MemberKind { get; }
        public bool CanWrite => Access == DdfHostValueAccess.ReadWrite;
    }

    public sealed class DdfHostEventDefinition
    {
        public DdfHostEventDefinition(string name, string handlerSignature = null,
            string documentation = null)
        {
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            HandlerSignature = handlerSignature ?? string.Empty;
            Documentation = documentation ?? string.Empty;
        }

        public string Name { get; }
        public string HandlerSignature { get; }
        public string Documentation { get; }
    }

    public sealed class DdfHostTypeDefinition
    {
        public DdfHostTypeDefinition(string name,
            IEnumerable<DdfHostFieldDefinition> fields = null,
            string documentation = null,
            IEnumerable<DdfHostEventDefinition> events = null)
        {
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            Fields = DdfHostCatalogValidation.Unique(fields,
                item => item?.Name, nameof(fields));
            Documentation = documentation ?? string.Empty;
            Events = DdfHostCatalogValidation.Unique(events,
                item => item?.Name, nameof(events));
        }

        public string Name { get; }
        public IReadOnlyList<DdfHostFieldDefinition> Fields { get; }
        public string Documentation { get; }
        public IReadOnlyList<DdfHostEventDefinition> Events { get; }
    }

    public sealed class DdfHostParameterDefinition
    {
        public DdfHostParameterDefinition(string name, string typeName,
            string documentation = null, DdfHostValueAdapter valueAdapter = null)
        {
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            TypeName = DdfHostCatalogValidation.TypeName(typeName, nameof(typeName));
            Documentation = documentation ?? string.Empty;
            ValueAdapter = DdfHostCatalogValidation.Adapter(TypeName,
                valueAdapter, nameof(valueAdapter));
        }

        public string Name { get; }
        public string TypeName { get; }
        public string Documentation { get; }
        public DdfHostValueAdapter ValueAdapter { get; }
    }

    public sealed class DdfHostFunctionDefinition
    {
        public DdfHostFunctionDefinition(string name, string returnType,
            IEnumerable<DdfHostParameterDefinition> parameters,
            Func<IReadOnlyList<object>, object> invoke,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null,
            DdfHostValueAdapter returnValueAdapter = null,
            DdfHostFunctionEffect effect = DdfHostFunctionEffect.Pure,
            Func<IReadOnlyList<object>, object> simulate = null,
            Func<DdfHostInvocationContext, IReadOnlyList<object>, ValueTask<object>> invokeAsync = null,
            Func<DdfHostInvocationContext, IReadOnlyList<object>, ValueTask<object>> simulateAsync = null,
            DdfHostFunctionDispatchTarget dispatchTarget =
                DdfHostFunctionDispatchTarget.Worker)
        {
            if (!Enum.IsDefined(typeof(DdfHostFunctionEffect), effect))
                throw new ArgumentOutOfRangeException(nameof(effect));
            if (!Enum.IsDefined(typeof(DdfHostFunctionDispatchTarget),
                dispatchTarget)) throw new ArgumentOutOfRangeException(
                    nameof(dispatchTarget));
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            ReturnType = DdfHostCatalogValidation.TypeName(returnType,
                nameof(returnType));
            Parameters = DdfHostCatalogValidation.Unique(parameters,
                item => item?.Name, nameof(parameters));
            if (invoke == null && invokeAsync == null)
                throw new ArgumentException(
                    "È richiesto un delegate host sincrono oppure asincrono.", nameof(invoke));
            Invoke = invoke;
            InvokeAsync = invokeAsync;
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
            if (dispatchTarget == DdfHostFunctionDispatchTarget.Ui &&
                RequiredPermissions.Count(permission => permission.StartsWith(
                    "ui.", StringComparison.Ordinal)) != 1)
                throw new ArgumentException(
                    "Una funzione UI deve dichiarare esattamente una capability nel namespace 'ui.'.",
                    nameof(requiredPermissions));
            ReturnValueAdapter = DdfHostCatalogValidation.Adapter(ReturnType,
                returnValueAdapter, nameof(returnValueAdapter));
            Effect = effect;
            Simulate = simulate;
            SimulateAsync = simulateAsync;
            DispatchTarget = dispatchTarget;
            RecoverableErrors = Array.Empty<DdfErrorDefinition>();
        }

        private DdfHostFunctionDefinition(DdfHostFunctionDefinition source,
            IEnumerable<DdfErrorDefinition> recoverableErrors)
        {
            Name = source.Name;
            ReturnType = source.ReturnType;
            Parameters = source.Parameters;
            Invoke = source.Invoke;
            InvokeAsync = source.InvokeAsync;
            Documentation = source.Documentation;
            RequiredPermissions = source.RequiredPermissions;
            ReturnValueAdapter = source.ReturnValueAdapter;
            Effect = source.Effect;
            Simulate = source.Simulate;
            SimulateAsync = source.SimulateAsync;
            DispatchTarget = source.DispatchTarget;
            RecoverableErrors = DdfErrorCatalog.ValidateRecoverable(
                recoverableErrors, nameof(recoverableErrors));
        }

        public string Name { get; }
        public string ReturnType { get; }
        public IReadOnlyList<DdfHostParameterDefinition> Parameters { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public DdfHostValueAdapter ReturnValueAdapter { get; }
        public DdfHostFunctionEffect Effect { get; }
        public DdfHostFunctionDispatchTarget DispatchTarget { get; }
        public IReadOnlyList<DdfErrorDefinition> RecoverableErrors { get; }
        public bool CanSimulate => Effect == DdfHostFunctionEffect.Pure ||
                                   Simulate != null || SimulateAsync != null;
        internal Func<IReadOnlyList<object>, object> Invoke { get; }
        internal Func<IReadOnlyList<object>, object> Simulate { get; }
        internal Func<DdfHostInvocationContext, IReadOnlyList<object>, ValueTask<object>> InvokeAsync { get; }
        internal Func<DdfHostInvocationContext, IReadOnlyList<object>, ValueTask<object>> SimulateAsync { get; }
        public string Signature => Name + "(" + string.Join(", ",
            Parameters.Select(item => item.TypeName + " " + item.Name)) +
            ") out " + ReturnType;

        public DdfHostFunctionDefinition DeclaresRecoverableErrors(
            params DdfErrorDefinition[] errors) => new(this, errors);
    }

    public sealed class DdfHostObjectDefinition
    {
        public DdfHostObjectDefinition(string name, string typeName,
            Func<object> readValue, string documentation = null,
            IEnumerable<string> requiredPermissions = null,
            Action<object> writeValue = null,
            DdfHostObjectLifetime lifetime = DdfHostObjectLifetime.ExecutionSnapshot,
            DdfHostValueAdapter valueAdapter = null)
        {
            if (!Enum.IsDefined(typeof(DdfHostObjectLifetime), lifetime))
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            Name = DdfHostCatalogValidation.Identifier(name, nameof(name));
            TypeName = DdfHostCatalogValidation.TypeName(typeName, nameof(typeName));
            ReadValue = readValue ?? throw new ArgumentNullException(nameof(readValue));
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
            WriteValue = writeValue;
            Lifetime = lifetime;
            ValueAdapter = DdfHostCatalogValidation.Adapter(TypeName,
                valueAdapter, nameof(valueAdapter));
        }

        public string Name { get; }
        public string TypeName { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public bool CanWrite => WriteValue != null;
        public DdfHostValueAccess Access => CanWrite
            ? DdfHostValueAccess.ReadWrite
            : DdfHostValueAccess.ReadOnly;
        public DdfHostObjectLifetime Lifetime { get; }
        public DdfHostValueAdapter ValueAdapter { get; }
        internal Func<object> ReadValue { get; }
        internal Action<object> WriteValue { get; }
    }

    public sealed class DdfHostCatalogSnapshot
    {
        internal DdfHostCatalogSnapshot(long revision,
            IEnumerable<DdfHostPermissionDefinition> permissions,
            IEnumerable<DdfHostTypeDefinition> types,
            IEnumerable<DdfHostObjectDefinition> objects,
            IEnumerable<DdfHostFunctionDefinition> functions,
            IEnumerable<DdfHostFunctionLibraryDefinition> functionLibraries,
            IEnumerable<DdfHostDatabaseDefinition> databases,
            IEnumerable<string> grantedPermissions)
        {
            Revision = revision;
            Permissions = (permissions ?? Array.Empty<DdfHostPermissionDefinition>())
                .ToList().AsReadOnly();
            Types = (types ?? Array.Empty<DdfHostTypeDefinition>()).ToList().AsReadOnly();
            Objects = (objects ?? Array.Empty<DdfHostObjectDefinition>()).ToList().AsReadOnly();
            Functions = (functions ?? Array.Empty<DdfHostFunctionDefinition>()).ToList().AsReadOnly();
            FunctionLibraries = (functionLibraries ??
                Array.Empty<DdfHostFunctionLibraryDefinition>()).ToList().AsReadOnly();
            Databases = (databases ?? Array.Empty<DdfHostDatabaseDefinition>()).ToList().AsReadOnly();
            GrantedPermissions = (grantedPermissions ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        public long Revision { get; }
        public IReadOnlyList<DdfHostPermissionDefinition> Permissions { get; }
        public IReadOnlyList<DdfHostTypeDefinition> Types { get; }
        public IReadOnlyList<DdfHostObjectDefinition> Objects { get; }
        public IReadOnlyList<DdfHostFunctionDefinition> Functions { get; }
        public IReadOnlyList<DdfHostFunctionLibraryDefinition> FunctionLibraries { get; }
        public IReadOnlyList<DdfHostDatabaseDefinition> Databases { get; }
        public IReadOnlyList<string> GrantedPermissions { get; }

        public bool IsGranted(string permissionId) => GrantedPermissions.Contains(
            permissionId ?? string.Empty, StringComparer.Ordinal);

        internal bool HasPermissions(IEnumerable<string> required) =>
            (required ?? Array.Empty<string>()).All(IsGranted);

        internal bool TryGetFunction(string name, out DdfHostFunctionDefinition function)
        {
            function = Functions.FirstOrDefault(item => string.Equals(
                item.Name, name, StringComparison.Ordinal));
            return function != null;
        }

        internal bool TryGetObject(string name, out DdfHostObjectDefinition value)
        {
            value = Objects.FirstOrDefault(item => string.Equals(
                item.Name, name, StringComparison.Ordinal));
            return value != null;
        }
    }

    public sealed class DdfHostCatalogChangedEventArgs : EventArgs
    {
        public DdfHostCatalogChangedEventArgs(DdfHostCatalogSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public DdfHostCatalogSnapshot Snapshot { get; }
    }

    public interface IDdfHostCatalog
    {
        DdfHostCatalogSnapshot Snapshot { get; }
        event EventHandler<DdfHostCatalogChangedEventArgs> Changed;
    }

    /// <summary>
    /// Explicit per-host registry. It stores descriptors and delegates supplied by
    /// the embedding application and never discovers members through reflection.
    /// </summary>
    public sealed class DdfEditorHostCatalog : IDdfHostCatalog
    {
        private readonly List<DdfHostPermissionDefinition> permissions = new List<DdfHostPermissionDefinition>();
        private readonly List<DdfHostTypeDefinition> types = new List<DdfHostTypeDefinition>();
        private readonly List<DdfHostObjectDefinition> objects = new List<DdfHostObjectDefinition>();
        private readonly List<DdfHostFunctionDefinition> functions = new List<DdfHostFunctionDefinition>();
        private readonly List<DdfHostFunctionLibraryDefinition> functionLibraries = new List<DdfHostFunctionLibraryDefinition>();
        private readonly List<DdfHostDatabaseDefinition> databases = new List<DdfHostDatabaseDefinition>();
        private readonly HashSet<string> grantedPermissions = new HashSet<string>(StringComparer.Ordinal);
        private long revision;

        public DdfHostCatalogSnapshot Snapshot => new DdfHostCatalogSnapshot(revision,
            permissions, types, objects, functions, functionLibraries, databases,
            grantedPermissions);

        public event EventHandler<DdfHostCatalogChangedEventArgs> Changed;

        public void RegisterPermission(DdfHostPermissionDefinition definition) =>
            Register(permissions, definition, item => item.Id, nameof(definition));

        public void RegisterType(DdfHostTypeDefinition definition) =>
            Register(types, definition, item => item.Name, nameof(definition));

        public void RegisterObject(DdfHostObjectDefinition definition)
        {
            ValidatePermissions(definition?.RequiredPermissions, nameof(definition));
            if (definition != null && functions.Any(item => item.Name == definition.Name))
                throw new ArgumentException("Il nome host è già usato da una funzione: " +
                    definition.Name + ".", nameof(definition));
            Register(objects, definition, item => item.Name, nameof(definition));
        }

        public void RegisterFunction(DdfHostFunctionDefinition definition)
        {
            ValidatePermissions(definition?.RequiredPermissions, nameof(definition));
            if (definition != null && objects.Any(item => item.Name == definition.Name))
                throw new ArgumentException("Il nome host è già usato da un oggetto: " +
                    definition.Name + ".", nameof(definition));
            Register(functions, definition, item => item.Name, nameof(definition));
        }

        public void RegisterFunctionLibrary(DdfHostFunctionLibraryDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (functionLibraries.Any(item => item.Id == definition.Id))
                throw new ArgumentException("Libreria di funzioni host già registrata: " +
                    definition.Id + ".", nameof(definition));
            ValidatePermissions(definition.RequiredPermissions, nameof(definition));
            foreach (DdfHostFunctionDefinition function in definition.Functions)
                ValidatePermissions(function.RequiredPermissions, nameof(definition));
            bool conflict = definition.Functions.Any(candidate =>
                functions.Any(item => item.Name == candidate.Name) ||
                objects.Any(item => item.Name == candidate.Name));
            if (conflict) throw new ArgumentException(
                "Una funzione della libreria usa un nome host già registrato.",
                nameof(definition));
            functionLibraries.Add(definition);
            functions.AddRange(definition.Functions);
            Publish();
        }

        public void RegisterDatabase(DdfHostDatabaseDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (databases.Any(item => item.Id == definition.Id))
                throw new ArgumentException("Database host già registrato: " +
                    definition.Id + ".", nameof(definition));
            ValidatePermissions(definition.RequiredPermissions, nameof(definition));
            foreach (DdfHostDatabaseOperationDefinition operation in definition.Operations)
                ValidatePermissions(operation.RequiredPermissions, nameof(definition));
            List<DdfHostFunctionDefinition> generated = definition.Operations
                .Select(definition.CreateFunction).ToList();
            string duplicate = generated.Select(item => item.Name)
                .GroupBy(item => item, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicate != null || generated.Any(candidate =>
                functions.Any(item => item.Name == candidate.Name) ||
                objects.Any(item => item.Name == candidate.Name)))
                throw new ArgumentException(
                    "Un'operazione database usa un nome host già registrato.",
                    nameof(definition));
            databases.Add(definition);
            functions.AddRange(generated);
            Publish();
        }

        public bool SetPermission(string permissionId, bool granted)
        {
            string id = DdfHostCatalogValidation.Identifier(permissionId,
                nameof(permissionId));
            if (!permissions.Any(item => item.Id == id))
                throw new ArgumentException("Permesso host non registrato: " + id + ".",
                    nameof(permissionId));
            bool changed = granted ? grantedPermissions.Add(id) :
                grantedPermissions.Remove(id);
            if (changed) Publish();
            return changed;
        }

        private void Register<T>(ICollection<T> target, T definition,
            Func<T, string> key, string parameterName) where T : class
        {
            if (definition == null) throw new ArgumentNullException(parameterName);
            string id = key(definition);
            if (target.Any(item => string.Equals(key(item), id, StringComparison.Ordinal)))
                throw new ArgumentException("Voce host già registrata: " + id + ".",
                    parameterName);
            target.Add(definition);
            Publish();
        }

        private void ValidatePermissions(IEnumerable<string> required,
            string parameterName)
        {
            string missing = (required ?? Array.Empty<string>()).FirstOrDefault(id =>
                !permissions.Any(permission => permission.Id == id));
            if (missing != null) throw new ArgumentException(
                "Permesso host non registrato: " + missing + ".", parameterName);
        }

        private void Publish()
        {
            revision++;
            Changed?.Invoke(this, new DdfHostCatalogChangedEventArgs(Snapshot));
        }
    }

    internal static class DdfHostCatalogValidation
    {
        internal static string Text(string value, string parameterName)
        {
            string result = (value ?? string.Empty).Trim();
            if (result.Length == 0) throw new ArgumentException(
                "Il valore è obbligatorio.", parameterName);
            return result;
        }

        internal static string Identifier(string value, string parameterName)
        {
            string result = Text(value, parameterName);
            if (!(char.IsLetter(result[0]) || result[0] == '_') ||
                result.Skip(1).Any(character => !(char.IsLetterOrDigit(character) ||
                    character == '_' || character == '.')))
                throw new ArgumentException("Identificatore host non valido: " + result + ".",
                    parameterName);
            return result;
        }

        internal static string TypeName(string value, string parameterName)
        {
            string result = Text(value, parameterName);
            string baseName = result;
            while (baseName.EndsWith("[]", StringComparison.Ordinal))
                baseName = baseName.Substring(0, baseName.Length - 2);
            Identifier(baseName, parameterName);
            return result;
        }

        internal static DdfHostValueAdapter Adapter(string typeName,
            DdfHostValueAdapter adapter, string parameterName)
        {
            DdfHostValueAdapter result = adapter ?? DdfHostValueAdapter.Identity;
            if (result.IsTyped && !string.Equals(result.ScriptTypeName,
                    typeName, StringComparison.Ordinal))
                throw new ArgumentException("L'adapter tipizzato per '" +
                    result.ScriptTypeName + "' non può essere associato al tipo DDF '" +
                    typeName + "'.", parameterName);
            return result;
        }

        internal static IReadOnlyList<T> Unique<T>(IEnumerable<T> values,
            Func<T, string> key, string parameterName) where T : class
        {
            var result = new List<T>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                if (value == null || !seen.Add(key(value)))
                    throw new ArgumentException("La raccolta contiene valori nulli o duplicati.",
                        parameterName);
                result.Add(value);
            }
            return result.AsReadOnly();
        }

        internal static IReadOnlyList<string> PermissionIds(IEnumerable<string> values,
            string parameterName)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                string id = Identifier(value, parameterName);
                if (!seen.Add(id)) throw new ArgumentException(
                    "Permesso duplicato: " + id + ".", parameterName);
                result.Add(id);
            }
            return result.AsReadOnly();
        }
    }
}
