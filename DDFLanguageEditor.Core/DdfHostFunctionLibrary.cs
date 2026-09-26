using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Groups a coherent set of typed host functions. Libraries are registered
    /// atomically so IntelliSense, type checking, Run and Debug always observe
    /// the same complete API surface.
    /// </summary>
    public sealed class DdfHostFunctionLibraryDefinition
    {
        public DdfHostFunctionLibraryDefinition(string id, string displayName,
            IEnumerable<DdfHostFunctionDefinition> functions,
            string documentation = null,
            IEnumerable<string> requiredPermissions = null)
        {
            Id = DdfHostCatalogValidation.Identifier(id, nameof(id));
            DisplayName = DdfHostCatalogValidation.Text(displayName,
                nameof(displayName));
            Documentation = documentation ?? string.Empty;
            RequiredPermissions = DdfHostCatalogValidation.PermissionIds(
                requiredPermissions, nameof(requiredPermissions));
            IReadOnlyList<DdfHostFunctionDefinition> source =
                DdfHostCatalogValidation.Unique(functions,
                    item => item?.Name, nameof(functions));
            if (source.Count == 0) throw new ArgumentException(
                "È richiesta almeno una funzione host.", nameof(functions));
            Functions = source.Select(AddLibraryPermissions).ToList().AsReadOnly();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Documentation { get; }
        public IReadOnlyList<string> RequiredPermissions { get; }
        public IReadOnlyList<DdfHostFunctionDefinition> Functions { get; }

        private DdfHostFunctionDefinition AddLibraryPermissions(
            DdfHostFunctionDefinition function)
        {
            IReadOnlyList<string> permissions = RequiredPermissions
                .Concat(function.RequiredPermissions)
                .Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
            return new DdfHostFunctionDefinition(function.Name, function.ReturnType,
                function.Parameters, function.Invoke, function.Documentation,
                permissions, function.ReturnValueAdapter, function.Effect,
                function.Simulate, function.InvokeAsync, function.SimulateAsync);
        }
    }
}
