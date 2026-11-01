using System;
using System.Security.Cryptography;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfExtensionPluginAuditCheckpoint
    {
        private readonly byte[] lastMac;

        public DdfExtensionPluginAuditCheckpoint(long sequence, byte[] lastMac)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (lastMac == null || lastMac.Length != 32)
                throw new ArgumentException("HMAC checkpoint di 32 byte richiesto.",
                    nameof(lastMac));
            Sequence = sequence;
            this.lastMac = (byte[])lastMac.Clone();
        }

        public long Sequence { get; }
        public byte[] LastMac => (byte[])lastMac.Clone();

        public bool Matches(DdfExtensionPluginAuditCheckpoint other) =>
            other != null && Sequence == other.Sequence &&
            CryptographicOperations.FixedTimeEquals(lastMac, other.lastMac);
    }

    // Implementations must be durable, monotonic, protected from file rollback,
    // and atomic across processes. The Core does not provide such a backing store.
    public interface IDdfExtensionPluginAuditAnchor
    {
        bool TryInitialize(DdfExtensionPluginAuditCheckpoint initial);
        DdfExtensionPluginAuditCheckpoint Read();
        bool TryAdvance(DdfExtensionPluginAuditCheckpoint expected,
            DdfExtensionPluginAuditCheckpoint next);
    }
}
