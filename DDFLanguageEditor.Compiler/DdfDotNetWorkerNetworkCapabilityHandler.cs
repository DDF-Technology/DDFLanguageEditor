using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerNetworkCapabilityHandler :
        IDdfDotNetWorkerDuplexCapabilityHandler
    {
        private readonly DdfDotNetWorkerNetworkBroker broker;
        private readonly IReadOnlyDictionary<string,
            DdfDotNetWorkerNetworkGrant> grants;
        private readonly IReadOnlyDictionary<string,
            Action<HttpRequestMessage>> authorizers;
        private readonly List<DdfDotNetWorkerNetworkReceipt> receipts = new();
        private readonly object synchronization = new();

        public DdfDotNetWorkerNetworkCapabilityHandler(
            IEnumerable<DdfDotNetWorkerNetworkGrant> grants,
            DdfDotNetWorkerNetworkBroker broker = null,
            IReadOnlyDictionary<string, Action<HttpRequestMessage>>
                authorizers = null)
        {
            this.broker = broker ?? new DdfDotNetWorkerNetworkBroker();
            DdfDotNetWorkerNetworkGrant[] values = (grants ??
                Array.Empty<DdfDotNetWorkerNetworkGrant>()).ToArray();
            if (values.Any(value => value == null) || values.GroupBy(value =>
                    value.LogicalName, StringComparer.Ordinal).Any(group =>
                    group.Count() != 1))
                throw new ArgumentException(
                    "I grant HTTP live devono avere nomi logici univoci.",
                    nameof(grants));
            this.grants = values.ToDictionary(value => value.LogicalName,
                StringComparer.Ordinal);
            this.authorizers = authorizers ?? new Dictionary<string,
                Action<HttpRequestMessage>>(StringComparer.Ordinal);
        }

        public IReadOnlyList<DdfDotNetWorkerNetworkReceipt> Receipts
        {
            get
            {
                lock (synchronization) return receipts.ToList().AsReadOnly();
            }
        }

        public async ValueTask<DdfDotNetWorkerDuplexCapabilityResult>
            InvokeAsync(string capabilityId,
                IReadOnlyList<string> argumentsJson,
                CancellationToken cancellationToken)
        {
            if (!string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.FetchNetworkResponse,
                    StringComparison.Ordinal) || argumentsJson?.Count != 1 ||
                !TryReadString(argumentsJson[0], out string logicalName) ||
                !grants.TryGetValue(logicalName,
                    out DdfDotNetWorkerNetworkGrant grant))
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    "DDFC548", "Grant HTTP live non autorizzato.");
            authorizers.TryGetValue(logicalName,
                out Action<HttpRequestMessage> authorize);
            DdfDotNetWorkerNetworkAcquisitionResult result =
                await broker.AcquireAsync(grant, authorize,
                    cancellationToken);
            if (!result.Success)
            {
                DdfDotNetContractIssue issue = result.Issues.FirstOrDefault();
                return DdfDotNetWorkerDuplexCapabilityResult.Failed(
                    issue?.Code ?? "DDFC545", issue?.Message ??
                    "Acquisizione HTTP live governata fallita.");
            }
            lock (synchronization) receipts.Add(result.Receipt);
            return DdfDotNetWorkerDuplexCapabilityResult.Succeeded(
                JsonSerializer.Serialize(result.Snapshot.Content));
        }

        private static bool TryReadString(string json, out string value)
        {
            value = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.String)
                    return false;
                value = document.RootElement.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }
            catch (JsonException) { return false; }
        }
    }
}
