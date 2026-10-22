using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    /// <summary>
    /// Internal foundation for DDF-0042. SDK 0.41 exposes only its immutable,
    /// backend-neutral projection and keeps tokens and continuation details here.
    /// </summary>
    internal enum DdfCooperativeExecutionState
    {
        Idle,
        Running,
        AwaitingCapability,
        Cancelling,
        CleaningUp
    }

    internal enum DdfCooperativeSuspensionResultKind
    {
        Succeeded,
        Faulted,
        TimedOut,
        Cancelled
    }

    internal sealed class DdfCooperativeSuspensionRequest
    {
        internal const string SchemaVersion = "1.0";

        internal DdfCooperativeSuspensionRequest(Guid executionId,
            long generation, Guid correlationId, string capabilityId,
            string operationId, IReadOnlyList<object> arguments,
            string sourceDocumentId, int sourceStart, int sourceLength,
            string transactionId, DateTimeOffset createdAt,
            DateTimeOffset? deadline, CancellationToken cancellationToken,
            DdfRuntimeStateSnapshot runtimeState = null)
        {
            if (executionId == Guid.Empty)
                throw new ArgumentException("L'identità esecuzione è obbligatoria.",
                    nameof(executionId));
            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Il correlation id è obbligatorio.",
                    nameof(correlationId));
            if (sourceStart < 0) throw new ArgumentOutOfRangeException(nameof(sourceStart));
            if (sourceLength < 0) throw new ArgumentOutOfRangeException(nameof(sourceLength));
            if (deadline.HasValue && deadline.Value <= createdAt)
                throw new ArgumentOutOfRangeException(nameof(deadline));

            ExecutionId = executionId;
            Generation = generation;
            CorrelationId = correlationId;
            CapabilityId = DdfHostCatalogValidation.Identifier(capabilityId,
                nameof(capabilityId));
            OperationId = DdfHostCatalogValidation.Identifier(operationId,
                nameof(operationId));
            Arguments = DdfCooperativeValueSnapshot.FreezeList(arguments);
            SourceDocumentId = (sourceDocumentId ?? string.Empty).Trim();
            SourceStart = sourceStart;
            SourceLength = sourceLength;
            TransactionId = (transactionId ?? string.Empty).Trim();
            CreatedAt = createdAt;
            Deadline = deadline;
            CancellationToken = cancellationToken;
            RuntimeState = runtimeState;
        }

        internal Guid ExecutionId { get; }
        internal long Generation { get; }
        internal Guid CorrelationId { get; }
        internal string CapabilityId { get; }
        internal string OperationId { get; }
        internal IReadOnlyList<object> Arguments { get; }
        internal string SourceDocumentId { get; }
        internal int SourceStart { get; }
        internal int SourceLength { get; }
        internal string TransactionId { get; }
        internal DateTimeOffset CreatedAt { get; }
        internal DateTimeOffset? Deadline { get; }
        internal CancellationToken CancellationToken { get; }
        internal DdfRuntimeStateSnapshot RuntimeState { get; }
    }

    internal sealed class DdfCooperativeSuspensionResult
    {
        internal DdfCooperativeSuspensionResult(Guid executionId,
            long generation, Guid correlationId,
            DdfCooperativeSuspensionResultKind kind, object value = null,
            string errorCode = null, string errorMessage = null)
        {
            if (executionId == Guid.Empty)
                throw new ArgumentException("L'identità esecuzione è obbligatoria.",
                    nameof(executionId));
            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Il correlation id è obbligatorio.",
                    nameof(correlationId));
            if (!Enum.IsDefined(typeof(DdfCooperativeSuspensionResultKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == DdfCooperativeSuspensionResultKind.Succeeded &&
                (!string.IsNullOrWhiteSpace(errorCode) ||
                 !string.IsNullOrWhiteSpace(errorMessage)))
                throw new ArgumentException(
                    "Un risultato riuscito non può contenere un errore.",
                    nameof(errorCode));
            if (kind != DdfCooperativeSuspensionResultKind.Succeeded &&
                string.IsNullOrWhiteSpace(errorCode))
                throw new ArgumentException(
                    "Un risultato non riuscito richiede un codice stabile.",
                    nameof(errorCode));

            ExecutionId = executionId;
            Generation = generation;
            CorrelationId = correlationId;
            Kind = kind;
            Value = DdfCooperativeValueSnapshot.Freeze(value);
            ErrorCode = (errorCode ?? string.Empty).Trim();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        internal Guid ExecutionId { get; }
        internal long Generation { get; }
        internal Guid CorrelationId { get; }
        internal DdfCooperativeSuspensionResultKind Kind { get; }
        internal object Value { get; }
        internal string ErrorCode { get; }
        internal string ErrorMessage { get; }
    }

    internal sealed class DdfCooperativeExecutionSnapshot
    {
        internal DdfCooperativeExecutionSnapshot(Guid executionId,
            long generation, DdfCooperativeExecutionState state,
            DdfCooperativeSuspensionRequest pendingSuspension)
        {
            ExecutionId = executionId;
            Generation = generation;
            State = state;
            PendingSuspension = pendingSuspension;
        }

        internal Guid ExecutionId { get; }
        internal long Generation { get; }
        internal DdfCooperativeExecutionState State { get; }
        internal DdfCooperativeSuspensionRequest PendingSuspension { get; }
    }

    /// <summary>
    /// Thread-safe transition guard for a single DDF script instance. It stores
    /// no provider or CLR continuation and therefore cannot execute callbacks.
    /// </summary>
    internal sealed class DdfCooperativeExecutionCoordinator
    {
        private readonly object syncRoot = new object();
        private DdfCooperativeExecutionState state;
        private Guid executionId;
        private long generation;
        private DdfCooperativeSuspensionRequest pendingSuspension;

        internal DdfCooperativeExecutionSnapshot Snapshot
        {
            get
            {
                lock (syncRoot)
                    return new DdfCooperativeExecutionSnapshot(executionId,
                        generation, state, pendingSuspension);
            }
        }

        internal DdfCooperativeExecutionSnapshot Begin(Guid nextExecutionId,
            long? requestedGeneration = null)
        {
            if (nextExecutionId == Guid.Empty)
                throw new ArgumentException("L'identità esecuzione è obbligatoria.",
                    nameof(nextExecutionId));
            lock (syncRoot)
            {
                RequireState(DdfCooperativeExecutionState.Idle);
                executionId = nextExecutionId;
                if (requestedGeneration.HasValue)
                {
                    if (requestedGeneration.Value <= generation)
                        throw new ArgumentOutOfRangeException(
                            nameof(requestedGeneration));
                    generation = requestedGeneration.Value;
                }
                else generation++;
                state = DdfCooperativeExecutionState.Running;
                pendingSuspension = null;
                return CreateSnapshot();
            }
        }

        internal DdfCooperativeSuspensionRequest Suspend(string capabilityId,
            string operationId, IReadOnlyList<object> arguments,
            string sourceDocumentId, int sourceStart, int sourceLength,
            string transactionId, DateTimeOffset createdAt,
            DateTimeOffset? deadline, CancellationToken cancellationToken,
            DdfRuntimeStateSnapshot runtimeState = null)
        {
            lock (syncRoot)
            {
                RequireState(DdfCooperativeExecutionState.Running);
                pendingSuspension = new DdfCooperativeSuspensionRequest(
                    executionId, generation, Guid.NewGuid(), capabilityId,
                    operationId, arguments, sourceDocumentId, sourceStart,
                    sourceLength, transactionId, createdAt, deadline,
                    cancellationToken, runtimeState);
                state = DdfCooperativeExecutionState.AwaitingCapability;
                return pendingSuspension;
            }
        }

        internal bool TryResume(DdfCooperativeSuspensionResult result,
            out DdfCooperativeExecutionSnapshot snapshot)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            lock (syncRoot)
            {
                if (state != DdfCooperativeExecutionState.AwaitingCapability ||
                    pendingSuspension == null ||
                    result.ExecutionId != executionId ||
                    result.Generation != generation ||
                    result.CorrelationId != pendingSuspension.CorrelationId)
                {
                    snapshot = CreateSnapshot();
                    return false;
                }

                pendingSuspension = null;
                state = result.Kind == DdfCooperativeSuspensionResultKind.Cancelled
                    ? DdfCooperativeExecutionState.Cancelling
                    : DdfCooperativeExecutionState.Running;
                snapshot = CreateSnapshot();
                return true;
            }
        }

        internal bool RequestCancellation(long expectedGeneration)
        {
            lock (syncRoot)
            {
                if (expectedGeneration != generation ||
                    state is DdfCooperativeExecutionState.Idle or
                    DdfCooperativeExecutionState.Cancelling or
                    DdfCooperativeExecutionState.CleaningUp)
                    return false;
                pendingSuspension = null;
                state = DdfCooperativeExecutionState.Cancelling;
                return true;
            }
        }

        internal void BeginCleanup(long expectedGeneration)
        {
            lock (syncRoot)
            {
                RequireGeneration(expectedGeneration);
                if (state is not (DdfCooperativeExecutionState.Running or
                    DdfCooperativeExecutionState.Cancelling))
                    throw new InvalidOperationException(
                        "Il cleanup richiede un'esecuzione attiva o in cancellazione.");
                pendingSuspension = null;
                state = DdfCooperativeExecutionState.CleaningUp;
            }
        }

        internal void Complete(long expectedGeneration)
        {
            lock (syncRoot)
            {
                RequireGeneration(expectedGeneration);
                if (state is DdfCooperativeExecutionState.Idle or
                    DdfCooperativeExecutionState.AwaitingCapability)
                    throw new InvalidOperationException(
                        "L'esecuzione non può essere completata nello stato corrente.");
                pendingSuspension = null;
                state = DdfCooperativeExecutionState.Idle;
                executionId = Guid.Empty;
            }
        }

        internal static DateTimeOffset? EarliestDeadline(
            params DateTimeOffset?[] deadlines)
        {
            DateTimeOffset? result = null;
            foreach (DateTimeOffset? deadline in deadlines ??
                Array.Empty<DateTimeOffset?>())
            {
                if (deadline.HasValue &&
                    (!result.HasValue || deadline.Value < result.Value))
                    result = deadline;
            }
            return result;
        }

        private DdfCooperativeExecutionSnapshot CreateSnapshot() =>
            new(executionId, generation, state, pendingSuspension);

        private void RequireGeneration(long expectedGeneration)
        {
            if (expectedGeneration != generation)
                throw new InvalidOperationException(
                    "La generazione dell'esecuzione non corrisponde.");
        }

        private void RequireState(DdfCooperativeExecutionState expected)
        {
            if (state != expected)
                throw new InvalidOperationException("Transizione non valida da " +
                    state + " a partire dallo stato richiesto " + expected + ".");
        }
    }

    internal static class DdfCooperativeValueSnapshot
    {
        internal static IReadOnlyList<object> FreezeList(
            IReadOnlyList<object> values) => (values ?? Array.Empty<object>())
            .Select(Freeze).ToList().AsReadOnly();

        internal static object Freeze(object value)
        {
            if (value == null || value is string || value is char ||
                value is bool || value is int || value is long ||
                value is float || value is double || value is decimal ||
                value is DdfEnumValue)
                return value;
            if (value is IReadOnlyDictionary<string, object> readOnly)
                return FreezeDictionary(readOnly);
            if (value is IDictionary<string, object> dictionary)
                return FreezeDictionary(dictionary);
            if (value is IEnumerable sequence)
                return sequence.Cast<object>().Select(Freeze).ToList().AsReadOnly();
            throw new ArgumentException("Il valore della continuazione non è un " +
                "valore DDF serializzabile.", nameof(value));
        }

        private static IReadOnlyDictionary<string, object> FreezeDictionary(
            IEnumerable<KeyValuePair<string, object>> values)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in values)
            {
                string key = item.Key ?? throw new ArgumentException(
                    "Una chiave della continuazione è nulla.", nameof(values));
                if (!result.TryAdd(key, Freeze(item.Value)))
                    throw new ArgumentException(
                        "Una chiave della continuazione è duplicata: " + key + ".",
                        nameof(values));
            }
            return new ReadOnlyDictionary<string, object>(result);
        }
    }
}
