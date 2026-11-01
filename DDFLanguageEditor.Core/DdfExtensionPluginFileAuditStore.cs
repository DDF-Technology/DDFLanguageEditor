using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // Host-owned, append-only local audit. A protected key and path are supplied by the host.
    public sealed class DdfExtensionPluginFileAuditStore : IDisposable
    {
        private const int MaxJournalBytes = 32 * 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly object gate = new object();
        private readonly string path;
        private readonly byte[] key;
        private readonly IDdfExtensionPluginAuditAnchor anchor;
        private bool disposed;

        public DdfExtensionPluginFileAuditStore(string path, byte[] authenticationKey,
            bool createNew = false, IDdfExtensionPluginAuditAnchor anchor = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("Percorso audit assoluto richiesto.", nameof(path));
            if (authenticationKey == null || authenticationKey.Length < 32 ||
                authenticationKey.Length > 64)
                throw new ArgumentException("Chiave audit di 32-64 byte richiesta.",
                    nameof(authenticationKey));
            this.path = Path.GetFullPath(path);
            this.anchor = anchor;
            if (createNew)
            {
                if (!Directory.Exists(Path.GetDirectoryName(this.path)))
                    throw new DirectoryNotFoundException("Directory audit non disponibile.");
                using var stream = new FileStream(this.path, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                stream.Flush(true);
                if (anchor != null && !anchor.TryInitialize(
                        new DdfExtensionPluginAuditCheckpoint(0, new byte[32])))
                    throw new InvalidDataException("Ancora audit già inizializzata o indisponibile.");
            }
            key = (byte[])authenticationKey.Clone();
        }

        internal DdfExtensionPluginSecurityEvent Append(
            DdfExtensionPluginManifest manifest, DdfExtensionPluginOperationResult result,
            DateTimeOffset observedAtUtc)
        {
            DdfExtensionPluginSecurityEvent entry = null;
            AppendRecord(sequence =>
            {
                entry = new DdfExtensionPluginSecurityEvent(sequence,
                    observedAtUtc, manifest, result);
                return JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = "1.0",
                    eventType = "decision",
                    sequence = entry.Sequence,
                    observedAtUtc = entry.ObservedAtUtc,
                    pluginId = entry.PluginId,
                    publisher = entry.Publisher,
                    version = entry.Version,
                    assemblySha256 = entry.AssemblySha256,
                    capability = entry.Capability,
                    resourceId = entry.ResourceId,
                    decision = entry.Decision.ToString(),
                    admissionDecision = entry.AdmissionDecision.ToString(),
                    code = entry.Code,
                    domain = entry.Domain.ToString()
                });
            });
            return entry;
        }

        internal DdfExtensionPluginConsentEvent AppendConsent(
            DdfExtensionPluginManifest manifest, DdfExtensionPluginConsentAction action,
            string capability, string resourceId, string actorId,
            DateTimeOffset observedAtUtc)
        {
            DdfExtensionPluginConsentEvent entry = null;
            AppendRecord(sequence =>
            {
                entry = new DdfExtensionPluginConsentEvent(sequence, observedAtUtc,
                    manifest, action, capability, resourceId, actorId);
                return JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = "1.0",
                    eventType = "consent",
                    sequence = entry.Sequence,
                    observedAtUtc = entry.ObservedAtUtc,
                    pluginId = entry.PluginId,
                    publisher = entry.Publisher,
                    version = entry.Version,
                    assemblySha256 = entry.AssemblySha256,
                    capability = entry.Capability,
                    resourceId = entry.ResourceId,
                    action = entry.Action.ToString(),
                    actorId = entry.ActorId
                });
            });
            return entry;
        }

        private void AppendRecord(Func<long, byte[]> createPayload)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(DdfExtensionPluginFileAuditStore));
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    throw new DirectoryNotFoundException("Directory audit non disponibile.");
                if (File.Exists(path) &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Il journal audit non può essere un link.");
                using var stream = new FileStream(path, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
                if (stream.Length > MaxJournalBytes)
                    throw new InvalidDataException("Journal audit oltre quota.");
                byte[] existing = new byte[(int)stream.Length];
                stream.ReadExactly(existing);
                (long sequence, byte[] previousMac) = Verify(existing);
                DdfExtensionPluginAuditCheckpoint checkpoint =
                    new DdfExtensionPluginAuditCheckpoint(sequence, previousMac);
                if (anchor != null && !checkpoint.Matches(anchor.Read()))
                    throw new InvalidDataException("Journal e ancora audit divergenti.");
                if (sequence == long.MaxValue)
                    throw new InvalidDataException("Sequenza audit esaurita.");
                byte[] payload = createPayload(sequence + 1);
                byte[] mac = ComputeMac(previousMac, payload);
                byte[] line = StrictUtf8.GetBytes(Convert.ToBase64String(payload) + " " +
                    Convert.ToHexString(mac).ToLowerInvariant() + "\n");
                if (stream.Length + line.Length > MaxJournalBytes)
                    throw new InvalidDataException("Journal audit oltre quota.");
                stream.Seek(0, SeekOrigin.End);
                stream.Write(line);
                stream.Flush(true);
                if (anchor != null && !anchor.TryAdvance(checkpoint,
                        new DdfExtensionPluginAuditCheckpoint(sequence + 1, mac)))
                    throw new InvalidDataException("Avanzamento ancora audit non riuscito.");
            }
        }

        private (long, byte[]) Verify(byte[] existing)
        {
            if (existing.Length == 0) return (0, new byte[32]);
            if (existing[existing.Length - 1] != (byte)'\n')
                throw new InvalidDataException("Ultima voce audit incompleta.");
            string content = StrictUtf8.GetString(existing);
            string[] lines = content.Split('\n');
            long sequence = 0;
            byte[] previousMac = new byte[32];
            foreach (string line in lines.Take(lines.Length - 1))
            {
                int separator = line.IndexOf(' ');
                if (separator < 1 || separator != line.LastIndexOf(' ') ||
                    line.Length - separator - 1 != 64)
                    throw new InvalidDataException("Voce audit malformata.");
                byte[] payload;
                byte[] actualMac;
                try
                {
                    payload = Convert.FromBase64String(line.Substring(0, separator));
                    actualMac = Convert.FromHexString(line.Substring(separator + 1));
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException("Codifica audit non valida.", exception);
                }
                byte[] expectedMac = ComputeMac(previousMac, payload);
                if (!CryptographicOperations.FixedTimeEquals(actualMac, expectedMac))
                    throw new InvalidDataException("Autenticazione audit non valida.");
                try
                {
                    using JsonDocument document = JsonDocument.Parse(payload);
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object ||
                        root.GetProperty("schemaVersion").GetString() != "1.0" ||
                        root.GetProperty("sequence").GetInt64() != sequence + 1)
                        throw new InvalidDataException("Sequenza audit non valida.");
                }
                catch (Exception exception) when (exception is JsonException ||
                    exception is KeyNotFoundException || exception is InvalidOperationException)
                {
                    throw new InvalidDataException("Payload audit non valido.", exception);
                }
                sequence++;
                previousMac = actualMac;
            }
            return (sequence, previousMac);
        }

        private byte[] ComputeMac(byte[] previousMac, byte[] payload)
        {
            byte[] input = new byte[previousMac.Length + payload.Length];
            Buffer.BlockCopy(previousMac, 0, input, 0, previousMac.Length);
            Buffer.BlockCopy(payload, 0, input, previousMac.Length, payload.Length);
            return HMACSHA256.HashData(key, input);
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                CryptographicOperations.ZeroMemory(key);
                disposed = true;
            }
        }
    }
}
