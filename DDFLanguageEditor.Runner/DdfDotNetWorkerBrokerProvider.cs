using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Compiler;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Runner
{
    internal sealed class DdfDotNetWorkerBrokerProvider :
        IDdfRuntimeAsyncCapabilityProvider
    {
        private sealed class BrokerFile
        {
            internal string Content { get; set; }
            internal string OriginalSha256 { get; init; }
            internal bool Writable { get; init; }
            internal int MaxWriteBytes { get; init; }
            internal bool Changed { get; set; }
        }

        private readonly IReadOnlyDictionary<string, BrokerFile> files;
        private readonly IReadOnlyDictionary<string, string> networkResponses;
        private readonly HashSet<string> liveNetworkResources;
        private readonly DdfDotNetWorkerDuplexChannel duplexChannel;

        private DdfDotNetWorkerBrokerProvider(
            IEnumerable<DdfDotNetWorkerFileSnapshot> files,
            IEnumerable<DdfDotNetWorkerNetworkSnapshot> networkResponses,
            IEnumerable<string> liveNetworkResources,
            DdfDotNetWorkerDuplexChannel duplexChannel)
        {
            this.files = files.ToDictionary(item => item.LogicalName,
                item => new BrokerFile
                {
                    Content = item.Content ?? string.Empty,
                    OriginalSha256 = item.Sha256,
                    Writable = item.Writable,
                    MaxWriteBytes = item.MaxWriteBytes
                }, StringComparer.Ordinal);
            this.networkResponses = networkResponses.ToDictionary(
                item => item.LogicalName, item => item.Content ?? string.Empty,
                StringComparer.Ordinal);
            this.liveNetworkResources = new HashSet<string>(
                liveNetworkResources ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            this.duplexChannel = duplexChannel;
        }

        internal static DdfDotNetWorkerBrokerProvider Create(
            DdfDotNetWorkerPackage package,
            DdfDotNetWorkerDuplexChannel duplexChannel = null) =>
                package.BrokerFiles.Count == 0 &&
                package.BrokerNetworkResponses.Count == 0 &&
                package.LiveNetworkResources.Count == 0
                ? null
                : new DdfDotNetWorkerBrokerProvider(package.BrokerFiles,
                    package.BrokerNetworkResponses,
                    package.LiveNetworkResources, duplexChannel);

        internal IReadOnlyList<DdfDotNetWorkerFileWrite> GetWrites() => files
            .Where(item => item.Value.Changed)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(item.Value.Content);
                return new DdfDotNetWorkerFileWrite
                {
                    LogicalName = item.Key,
                    OriginalSha256 = item.Value.OriginalSha256,
                    Content = item.Value.Content,
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                        .ToLowerInvariant()
                };
            }).ToList().AsReadOnly();

        public object Invoke(string capabilityId,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.ReadText,
                    StringComparison.Ordinal) && arguments?.Count == 1 &&
                arguments[0] is string readName &&
                files.TryGetValue(readName, out BrokerFile readFile))
                return readFile.Content;
            if (string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.WriteText,
                    StringComparison.Ordinal) && arguments?.Count == 2 &&
                arguments[0] is string writeName &&
                arguments[1] is string content &&
                files.TryGetValue(writeName, out BrokerFile writeFile) &&
                writeFile.Writable)
            {
                if (Encoding.UTF8.GetByteCount(content) >
                    writeFile.MaxWriteBytes)
                    throw new InvalidOperationException(
                        "Scrittura file broker oltre il budget autorizzato.");
                writeFile.Content = content;
                writeFile.Changed = true;
                return true;
            }
            if (string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.ReadNetworkResponse,
                    StringComparison.Ordinal) && arguments?.Count == 1 &&
                arguments[0] is string responseName &&
                networkResponses.TryGetValue(responseName,
                    out string responseContent))
                return responseContent;
            if (string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.FetchNetworkResponse,
                    StringComparison.Ordinal) && arguments?.Count == 1 &&
                arguments[0] is string liveName &&
                liveNetworkResources.Contains(liveName) &&
                duplexChannel != null)
                return duplexChannel.Invoke(capabilityId, arguments,
                    cancellationToken);
            else
                throw new InvalidOperationException(
                    "Capability o risorsa broker non autorizzata.");
        }

        public ValueTask<object> InvokeAsync(string capabilityId,
            IReadOnlyList<object> arguments,
            DdfRuntimeCapabilityInvocationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(capabilityId,
                    DdfDotNetWorkerBrokerCapabilities.FetchNetworkResponse,
                    StringComparison.Ordinal) && arguments?.Count == 1 &&
                arguments[0] is string liveName &&
                liveNetworkResources.Contains(liveName) &&
                duplexChannel != null)
                return duplexChannel.InvokeAsync(capabilityId, arguments,
                    context.CancellationToken);
            return ValueTask.FromResult(Invoke(capabilityId, arguments,
                context.CancellationToken));
        }
    }
}
