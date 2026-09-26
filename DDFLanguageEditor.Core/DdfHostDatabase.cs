using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfHostDatabaseOperationKind
    {
        Query,
        Command
    }

    public sealed class DdfHostDatabaseRequest
    {
        internal DdfHostDatabaseRequest(string databaseId, string statementId,
            DdfHostDatabaseOperationKind kind,
            IReadOnlyDictionary<string, object> parameters,
            string transactionId, DdfHostInvocationContext invocation)
        {
            DatabaseId = databaseId;
            StatementId = statementId;
            Kind = kind;
            Parameters = parameters;
            TransactionId = transactionId ?? string.Empty;
            Invocation = invocation;
        }

        public string DatabaseId { get; }
        public string StatementId { get; }
        public DdfHostDatabaseOperationKind Kind { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }
        public string TransactionId { get; }
        public bool HasTransaction => TransactionId.Length > 0;
        public DdfHostInvocationContext Invocation { get; }
    }

    /// <summary>
    /// Implemented by the embedding application. Connections, credentials,
    /// commands and transactions never cross this boundary into the script.
    /// </summary>
    public interface IDdfHostDatabaseService
    {
        ValueTask<object> ExecuteAsync(DdfHostDatabaseRequest request);
    }

    public sealed class DdfHostDatabaseOperationDefinition
    {
        public DdfHostDatabaseOperationDefinition(string functionName,
            string statementId, DdfHostDatabaseOperationKind kind,
            string returnType, IEnumerable<DdfHostParameterDefinition> parameters,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null,
            DdfHostValueAdapter resultAdapter = null,
            bool supportsSimulation = false)
        {
            if (!Enum.IsDefined(typeof(DdfHostDatabaseOperationKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            FunctionName = DdfHostCatalogValidation.Identifier(functionName,
                nameof(functionName));
            StatementId = DdfHostCatalogValidation.Identifier(statementId,
                nameof(statementId));
            Kind = kind;
            ReturnType = DdfHostCatalogValidation.TypeName(returnType,
                nameof(returnType));
            Parameters = DdfHostCatalogValidation.Unique(parameters,
                item => item?.Name, nameof(parameters));
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
            ResultAdapter = resultAdapter ?? DdfHostValueAdapter.Identity;
            SupportsSimulation = kind == DdfHostDatabaseOperationKind.Query ||
                supportsSimulation;
        }

        public string FunctionName { get; }
        public string StatementId { get; }
        public DdfHostDatabaseOperationKind Kind { get; }
        public string ReturnType { get; }
        public IReadOnlyList<DdfHostParameterDefinition> Parameters { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public DdfHostValueAdapter ResultAdapter { get; }
        public bool SupportsSimulation { get; }
    }

    public sealed class DdfHostDatabaseDefinition
    {
        public DdfHostDatabaseDefinition(string id,
            IDdfHostDatabaseService service,
            IEnumerable<DdfHostDatabaseOperationDefinition> operations,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null)
        {
            Id = DdfHostCatalogValidation.Identifier(id, nameof(id));
            Service = service ?? throw new ArgumentNullException(nameof(service));
            Operations = DdfHostCatalogValidation.Unique(operations,
                item => item?.FunctionName, nameof(operations));
            if (Operations.Count == 0) throw new ArgumentException(
                "È richiesta almeno un'operazione database.", nameof(operations));
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
        }

        public string Id { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public IReadOnlyList<DdfHostDatabaseOperationDefinition> Operations { get; }
        public IDdfHostDatabaseService Service { get; }

        internal DdfHostFunctionDefinition CreateFunction(
            DdfHostDatabaseOperationDefinition operation)
        {
            IReadOnlyList<string> permissions = RequiredPermissions
                .Concat(operation.RequiredPermissions)
                .Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
            Func<DdfHostInvocationContext, IReadOnlyList<object>,
                ValueTask<object>> invoke = (context, arguments) =>
            {
                var values = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int index = 0; index < operation.Parameters.Count; index++)
                    values.Add(operation.Parameters[index].Name, arguments[index]);
                context.TryGetDatabaseTransaction(Id, out string transactionId);
                var request = new DdfHostDatabaseRequest(Id, operation.StatementId,
                    operation.Kind, new ReadOnlyDictionary<string, object>(values),
                    transactionId, context);
                return Service.ExecuteAsync(request);
            };
            bool mutating = operation.Kind == DdfHostDatabaseOperationKind.Command;
            return new DdfHostFunctionDefinition(operation.FunctionName,
                operation.ReturnType, operation.Parameters, invoke: null,
                documentation: operation.Documentation,
                requiredPermissions: permissions,
                returnValueAdapter: operation.ResultAdapter,
                effect: mutating ? DdfHostFunctionEffect.HostMutation :
                    DdfHostFunctionEffect.Pure,
                invokeAsync: invoke,
                simulateAsync: mutating && operation.SupportsSimulation
                    ? invoke : null);
        }
    }
}
