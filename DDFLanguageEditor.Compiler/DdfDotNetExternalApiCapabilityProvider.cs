using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Exact binding between a descriptor pinned by the project lock and its
    /// current runtime adapter. Any identity, version, fingerprint or adapter
    /// drift is rejected before compiled code can execute.
    /// </summary>
    public sealed class DdfDotNetExternalApiBinding
    {
        public DdfDotNetExternalApiBinding(string expectedReferenceId,
            string expectedReferenceVersion, string expectedFingerprint,
            DdfExternalApiDefinition definition, string expectedAdapterId,
            IDdfExternalApiRuntimeAdapter adapter,
            Func<string, bool> isPermissionGranted)
        {
            if (definition == null) throw new ArgumentNullException(
                nameof(definition));
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (string.IsNullOrWhiteSpace(expectedReferenceId) ||
                string.IsNullOrWhiteSpace(expectedReferenceVersion) ||
                string.IsNullOrWhiteSpace(expectedFingerprint) ||
                string.IsNullOrWhiteSpace(expectedAdapterId))
                throw new ArgumentException(
                    "Identità, versione, fingerprint e adapter attesi sono obbligatori.");
            if (!string.Equals(definition.ReferenceId, expectedReferenceId,
                    StringComparison.Ordinal) ||
                !string.Equals(definition.ReferenceVersion,
                    expectedReferenceVersion, StringComparison.Ordinal) ||
                !string.Equals(definition.Fingerprint, expectedFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Il descrittore API esterno non coincide con il lock compilato.");
            if (!string.Equals(adapter.AdapterId, expectedAdapterId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "L'adapter API esterno non coincide con il binding compilato.");
            Definition = definition;
            Adapter = adapter;
            ExpectedAdapterId = expectedAdapterId;
            IsPermissionGranted = isPermissionGranted ?? throw new
                ArgumentNullException(nameof(isPermissionGranted));
        }

        public DdfExternalApiDefinition Definition { get; }
        public IDdfExternalApiRuntimeAdapter Adapter { get; }
        public string ExpectedAdapterId { get; }
        internal Func<string, bool> IsPermissionGranted { get; }
    }

    /// <summary>
    /// Projects external API functions into the stable capability IDs emitted
    /// by the Roslyn backend. No CLR discovery is performed at runtime.
    /// </summary>
    public sealed class DdfDotNetExternalApiCapabilityProvider :
        IDdfRuntimeAsyncCapabilityProvider
    {
        private sealed class FunctionBinding
        {
            internal FunctionBinding(DdfDotNetExternalApiBinding owner,
                DdfExternalApiFunctionDefinition function)
            {
                Owner = owner;
                Function = function;
            }

            internal DdfDotNetExternalApiBinding Owner { get; }
            internal DdfExternalApiFunctionDefinition Function { get; }
        }

        private readonly IReadOnlyDictionary<string, FunctionBinding> functions;

        public DdfDotNetExternalApiCapabilityProvider(
            IEnumerable<DdfDotNetExternalApiBinding> bindings)
        {
            DdfDotNetExternalApiBinding[] source = (bindings ?? throw new
                ArgumentNullException(nameof(bindings))).ToArray();
            if (source.Length == 0 || source.Any(item => item == null))
                throw new ArgumentException(
                    "È richiesto almeno un binding API esterno valido.",
                    nameof(bindings));
            var result = new Dictionary<string, FunctionBinding>(
                StringComparer.Ordinal);
            foreach (DdfDotNetExternalApiBinding binding in source)
            foreach (DdfExternalApiFunctionDefinition function in
                     binding.Definition.Functions)
            {
                string capability = DdfDotNetHostCapabilities.ForFunction(
                    function.Name);
                if (!result.TryAdd(capability,
                        new FunctionBinding(binding, function)))
                    throw new ArgumentException(
                        "Capability API esterna duplicata: " + capability + ".",
                        nameof(bindings));
            }
            functions = result;
        }

        public IReadOnlyCollection<string> CapabilityIds =>
            functions.Keys.OrderBy(item => item, StringComparer.Ordinal)
                .ToList().AsReadOnly();

        public object Invoke(string capabilityId,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken)
        {
            var resources = new List<object>();
            var context = new DdfExternalApiInvocationContext(Guid.NewGuid(),
                cancellationToken, DateTimeOffset.UtcNow,
                registerResource: item => resources.Add(item),
                registerAsyncResource: item => resources.Add(item));
            try
            {
                return InvokeCoreAsync(capabilityId, arguments, context)
                    .AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                for (int index = resources.Count - 1; index >= 0; index--)
                {
                    if (resources[index] is IAsyncDisposable asynchronous)
                        asynchronous.DisposeAsync().AsTask().GetAwaiter()
                            .GetResult();
                    else (resources[index] as IDisposable)?.Dispose();
                }
            }
        }

        public ValueTask<object> InvokeAsync(string capabilityId,
            IReadOnlyList<object> arguments,
            DdfRuntimeCapabilityInvocationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var externalContext = new DdfExternalApiInvocationContext(
                context.ExecutionId, context.CancellationToken,
                context.StartedAt, context.Deadline,
                context.RegisterResource, context.RegisterAsyncResource,
                hostEffectTransactionId: context.TransactionId);
            return InvokeCoreAsync(capabilityId, arguments, externalContext);
        }

        private ValueTask<object> InvokeCoreAsync(string capabilityId,
            IReadOnlyList<object> arguments,
            DdfExternalApiInvocationContext context)
        {
            if (!functions.TryGetValue(capabilityId ?? string.Empty,
                    out FunctionBinding binding))
                throw new InvalidOperationException(
                    "Capability API esterna non autorizzata: " + capabilityId +
                    ".");
            string denied = binding.Function.RequiredPermissions.FirstOrDefault(
                permission => !binding.Owner.IsPermissionGranted(permission));
            if (denied != null)
                throw new InvalidOperationException(
                    "Permesso API esterno revocato: " + denied + ".");
            int expected = binding.Function.Parameters.Count;
            if ((arguments?.Count ?? 0) != expected)
                throw new ArgumentException(
                    "Numero di argomenti API esterna non valido.",
                    nameof(arguments));
            return DdfExternalApiRuntimeInvoker.InvokeAsync(
                binding.Owner.Adapter, binding.Function.BindingId,
                arguments ?? Array.Empty<object>(), context);
        }
    }
}
