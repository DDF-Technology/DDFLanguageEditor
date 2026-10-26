using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Optional embedded-SDK service. It performs no work until the host calls
    /// CheckAsync and delegates network and trust policy to the selected provider.
    /// </summary>
    public interface IDdfEditorProductUpdateService
    {
        string ComponentId { get; }
        string CurrentVersion { get; }
        Task<DdfProductUpdateResult> CheckAsync(DdfProductUpdateChannel channel,
            CancellationToken cancellationToken = default);
    }

    public sealed class DdfEditorProductUpdateService :
        IDdfEditorProductUpdateService
    {
        private readonly IDdfProductUpdateProvider provider;

        public DdfEditorProductUpdateService(IDdfProductUpdateProvider provider,
            string componentId, string currentVersion)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            ComponentId = DdfProductUpdateResult.RequireComponentId(componentId);
            CurrentVersion = DdfProductUpdateResult.RequireVersion(currentVersion,
                nameof(currentVersion));
        }

        public string ComponentId { get; }
        public string CurrentVersion { get; }

        public static DdfEditorProductUpdateService ForEmbeddedComponent(
            IDdfProductUpdateProvider provider, string componentId)
        {
            if (string.IsNullOrWhiteSpace(componentId))
                throw new ArgumentException("Il componente SDK è obbligatorio.",
                    nameof(componentId));
            DdfProductComponentVersion component =
                DdfProductVersionCatalog.Current.EmbeddedComponents.SingleOrDefault(
                    item => string.Equals(item.Id, componentId,
                        StringComparison.Ordinal));
            if (component == null)
                throw new ArgumentException(
                    "Il componente non appartiene all'SDK embedded corrente: " +
                    componentId, nameof(componentId));
            return new DdfEditorProductUpdateService(provider, component.Id,
                component.Version);
        }

        public async Task<DdfProductUpdateResult> CheckAsync(
            DdfProductUpdateChannel channel,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new DdfProductUpdateRequest(
                DdfProductUpdateComponentKind.EmbeddedSdk, ComponentId,
                CurrentVersion, channel);
            DdfProductUpdateResult result = await provider.CheckAsync(request,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null || result.ComponentKind != request.ComponentKind ||
                !string.Equals(result.ComponentId, request.ComponentId,
                    StringComparison.Ordinal) ||
                !string.Equals(result.CurrentVersion, request.CurrentVersion,
                    StringComparison.Ordinal) || result.Channel != request.Channel)
            {
                return DdfProductUpdateResult.Unknown(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion, request.Channel,
                    DateTimeOffset.UtcNow,
                    "Il provider ha restituito un risultato non correlato alla richiesta SDK.");
            }
            return result;
        }
    }
}
