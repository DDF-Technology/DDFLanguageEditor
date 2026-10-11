using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Runtime
{
    public static class DdfRuntimeAbi
    {
        public const string Version = "1.6.0";
    }

    public enum DdfRuntimeDebugVariableKind
    {
        Parameter,
        Local,
        Global,
        Field,
        Element
    }

    public sealed class DdfRuntimeDebugMember
    {
        public DdfRuntimeDebugMember(string symbolId, string typeName,
            object value)
        {
            if (string.IsNullOrWhiteSpace(symbolId))
                throw new ArgumentException("Symbol id is required.",
                    nameof(symbolId));
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Type name is required.",
                    nameof(typeName));
            SymbolId = symbolId;
            TypeName = typeName;
            Value = value;
        }

        public string SymbolId { get; }
        public string TypeName { get; }
        public object Value { get; }
    }

    public interface IDdfRuntimeDebugStructure
    {
        IReadOnlyList<DdfRuntimeDebugMember> GetDebugMembers();
    }

    public sealed class DdfRuntimeDebugValue
    {
        internal DdfRuntimeDebugValue(string symbolId, string typeName,
            DdfRuntimeDebugVariableKind kind, string displayValue,
            IReadOnlyList<DdfRuntimeDebugValue> children, bool isTruncated)
        {
            SymbolId = symbolId;
            TypeName = typeName;
            Kind = kind;
            DisplayValue = displayValue;
            Children = children;
            IsTruncated = isTruncated;
        }

        public string SymbolId { get; }
        public string TypeName { get; }
        public DdfRuntimeDebugVariableKind Kind { get; }
        public string DisplayValue { get; }
        public IReadOnlyList<DdfRuntimeDebugValue> Children { get; }
        public bool IsTruncated { get; }
    }

    public sealed class DdfRuntimeDebugFrame
    {
        internal DdfRuntimeDebugFrame(string functionSymbolId,
            DdfRuntimeSourceLocation declaration,
            DdfRuntimeSourceLocation location,
            IReadOnlyList<DdfRuntimeDebugValue> variables)
        {
            FunctionSymbolId = functionSymbolId ?? string.Empty;
            Declaration = declaration;
            Location = location;
            Variables = variables;
        }

        public string FunctionSymbolId { get; }
        public DdfRuntimeSourceLocation Declaration { get; }
        public DdfRuntimeSourceLocation Location { get; }
        public IReadOnlyList<DdfRuntimeDebugValue> Variables { get; }
    }

    public sealed class DdfRuntimeDebugSnapshot
    {
        internal DdfRuntimeDebugSnapshot(DdfRuntimeSourceLocation location,
            long instructions, IReadOnlyList<DdfRuntimeDebugFrame> frames)
        {
            Location = location;
            Instructions = instructions;
            Frames = frames;
        }

        public DdfRuntimeSourceLocation Location { get; }
        public long Instructions { get; }
        public IReadOnlyList<DdfRuntimeDebugFrame> Frames { get; }
    }

    public interface IDdfRuntimeDebugObserver
    {
        void OnSequencePoint(DdfRuntimeDebugSnapshot snapshot,
            CancellationToken cancellationToken);
    }

    public readonly struct DdfRuntimeSourceLocation : IEquatable<DdfRuntimeSourceLocation>
    {
        public DdfRuntimeSourceLocation(string documentPath, int start, int length,
            string symbolId = null)
        {
            DocumentPath = (documentPath ?? string.Empty).Replace('\\', '/');
            Start = Math.Max(0, start);
            Length = Math.Max(0, length);
            SymbolId = symbolId ?? string.Empty;
        }

        public string DocumentPath { get; }
        public int Start { get; }
        public int Length { get; }
        public string SymbolId { get; }
        public bool Equals(DdfRuntimeSourceLocation other) =>
            DocumentPath == other.DocumentPath && Start == other.Start &&
            Length == other.Length && SymbolId == other.SymbolId;
        public override bool Equals(object obj) =>
            obj is DdfRuntimeSourceLocation other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(DocumentPath, Start, Length, SymbolId);
    }

    public sealed class DdfRuntimeException : Exception
    {
        public DdfRuntimeException(string code, string message,
            DdfRuntimeSourceLocation location, Exception innerException = null)
            : base(message ?? string.Empty, innerException)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Runtime error code is required.", nameof(code));
            Code = code;
            Location = location;
        }

        public string Code { get; }
        public DdfRuntimeSourceLocation Location { get; }
    }

    public interface IDdfRuntimeCapabilityProvider
    {
        object Invoke(string capabilityId, IReadOnlyList<object> arguments,
            CancellationToken cancellationToken);
    }

    public interface IDdfRuntimeAsyncCapabilityProvider :
        IDdfRuntimeCapabilityProvider
    {
        ValueTask<object> InvokeAsync(string capabilityId,
            IReadOnlyList<object> arguments,
            DdfRuntimeCapabilityInvocationContext context);
    }

    public sealed class DdfRuntimeCapabilityInvocationContext
    {
        private readonly Action<object> registerResource;

        internal DdfRuntimeCapabilityInvocationContext(Guid executionId,
            CancellationToken cancellationToken, DateTimeOffset startedAt,
            DateTimeOffset? deadline, Action<object> registerResource,
            string transactionId)
        {
            ExecutionId = executionId;
            CancellationToken = cancellationToken;
            StartedAt = startedAt;
            Deadline = deadline;
            this.registerResource = registerResource ??
                throw new ArgumentNullException(nameof(registerResource));
            TransactionId = transactionId ?? string.Empty;
        }

        public Guid ExecutionId { get; }
        public CancellationToken CancellationToken { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? Deadline { get; }
        public string TransactionId { get; }
        public bool HasTransaction => TransactionId.Length != 0;

        public void RegisterResource(IDisposable resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            registerResource(resource);
        }

        public void RegisterAsyncResource(IAsyncDisposable resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            registerResource(resource);
        }
    }

    public enum DdfRuntimeCapabilityEffectPolicy
    {
        Immediate,
        RollbackOnFailure
    }

    public enum DdfRuntimeCapabilityEffectKind
    {
        Function,
        ObjectWrite
    }

    public enum DdfRuntimeCapabilityEffectStatus
    {
        Started,
        Applied,
        Failed
    }

    public enum DdfRuntimeCapabilityEffectDisposition
    {
        None,
        Applied,
        PartiallyApplied,
        Committed,
        RolledBack,
        BeginFailed,
        CommitFailed,
        RollbackFailed
    }

    public sealed class DdfRuntimeCapabilityEffectRecord
    {
        internal DdfRuntimeCapabilityEffectRecord(int sequence,
            string capabilityId, DdfRuntimeCapabilityEffectKind kind,
            DdfRuntimeCapabilityEffectStatus status, string error)
        {
            Sequence = sequence;
            CapabilityId = capabilityId ?? string.Empty;
            Kind = kind;
            Status = status;
            Error = error ?? string.Empty;
        }

        public int Sequence { get; }
        public string CapabilityId { get; }
        public DdfRuntimeCapabilityEffectKind Kind { get; }
        public DdfRuntimeCapabilityEffectStatus Status { get; }
        public string Error { get; }
    }

    public sealed class DdfRuntimeCapabilityEffectReceipt
    {
        internal DdfRuntimeCapabilityEffectReceipt(
            DdfRuntimeCapabilityEffectPolicy policy,
            DdfRuntimeCapabilityEffectDisposition disposition,
            IEnumerable<DdfRuntimeCapabilityEffectRecord> effects,
            string transactionId = null, string error = null)
        {
            Policy = policy;
            Disposition = disposition;
            Effects = (effects ?? Array.Empty<
                    DdfRuntimeCapabilityEffectRecord>()).ToList().AsReadOnly();
            TransactionId = transactionId ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public DdfRuntimeCapabilityEffectPolicy Policy { get; }
        public DdfRuntimeCapabilityEffectDisposition Disposition { get; }
        public IReadOnlyList<DdfRuntimeCapabilityEffectRecord> Effects { get; }
        public string TransactionId { get; }
        public string Error { get; }
        public bool HasEffects => Effects.Count != 0;
    }

    public sealed class DdfRuntimeCapabilityTransactionContext
    {
        internal DdfRuntimeCapabilityTransactionContext(Guid executionId,
            DateTimeOffset startedAt, DateTimeOffset? deadline,
            CancellationToken cancellationToken)
        {
            ExecutionId = executionId;
            StartedAt = startedAt;
            Deadline = deadline;
            CancellationToken = cancellationToken;
        }

        public Guid ExecutionId { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? Deadline { get; }
        public CancellationToken CancellationToken { get; }
    }

    public interface IDdfRuntimeCapabilityTransaction : IAsyncDisposable
    {
        string Id { get; }
        ValueTask CommitAsync(CancellationToken cancellationToken);
        ValueTask RollbackAsync(CancellationToken cancellationToken);
    }

    public interface IDdfRuntimeCapabilityTransactionFactory
    {
        ValueTask<IDdfRuntimeCapabilityTransaction> BeginAsync(
            DdfRuntimeCapabilityTransactionContext context);
    }

    public sealed class DdfRuntimeExecutionResult
    {
        internal DdfRuntimeExecutionResult(object value, bool ended, long instructions)
        {
            Value = value;
            Ended = ended;
            Instructions = instructions;
            CapabilityEffects = new DdfRuntimeCapabilityEffectReceipt(
                DdfRuntimeCapabilityEffectPolicy.Immediate,
                DdfRuntimeCapabilityEffectDisposition.None,
                Array.Empty<DdfRuntimeCapabilityEffectRecord>());
        }

        public object Value { get; }
        public bool Ended { get; }
        public long Instructions { get; }
        public DdfRuntimeCapabilityEffectReceipt CapabilityEffects {
            get; internal set;
        }
    }

    public sealed class DdfRuntimeContext
    {
        public const int DebugMaximumDepth = 8;
        public const int DebugMaximumChildren = 256;
        public const int DebugMaximumValues = 1024;

        private sealed class EndSignal : Exception
        {
            internal EndSignal(object value) { Value = value; }
            internal object Value { get; }
        }

        private sealed class CapabilityEffectEntry
        {
            internal int Sequence { get; set; }
            internal string CapabilityId { get; set; }
            internal DdfRuntimeCapabilityEffectKind Kind { get; set; }
            internal DdfRuntimeCapabilityEffectStatus Status { get; set; }
            internal string Error { get; set; }
        }

        private readonly Action<string> output;
        private readonly Func<string> input;
        private readonly IDdfRuntimeCapabilityProvider capabilityProvider;
        private readonly IDdfRuntimeDebugObserver debugObserver;
        private readonly IDdfRuntimeCapabilityTransactionFactory
            capabilityTransactionFactory;
        private readonly object capabilityResourcesSync = new();
        private readonly List<object> capabilityResources = new();
        private readonly List<CapabilityEffectEntry> capabilityEffects = new();
        private readonly List<MutableDebugFrame> debugFrames = new();
        private readonly Dictionary<string, DebugRegistration> debugGlobals =
            new(StringComparer.Ordinal);
        private long instructions;
        private int capabilityCalls;
        private bool executing;
        private bool capabilityResourcesClosed = true;
        private Guid executionId;
        private DateTimeOffset executionStartedAt;
        private DdfRuntimeSourceLocation lastCapabilityLocation;
        private IDdfRuntimeCapabilityTransaction capabilityTransaction;
        private bool capabilityTransactionBeginAttempted;
        private string capabilityTransactionBeginError = string.Empty;

        public DdfRuntimeContext(int maxInstructions,
            CancellationToken cancellationToken = default,
            Action<string> output = null, Func<string> input = null,
            IDdfRuntimeCapabilityProvider capabilityProvider = null,
            IDdfRuntimeDebugObserver debugObserver = null,
            TimeSpan? capabilityTimeout = null,
            int maxCapabilityCalls = 1024,
            int maxCapabilityResources = 128,
            DateTimeOffset? executionDeadline = null,
            DdfRuntimeCapabilityEffectPolicy capabilityEffectPolicy =
                DdfRuntimeCapabilityEffectPolicy.Immediate,
            IDdfRuntimeCapabilityTransactionFactory
                capabilityTransactionFactory = null)
        {
            if (maxInstructions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxInstructions));
            if (capabilityTimeout.HasValue && capabilityTimeout.Value <=
                TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(capabilityTimeout));
            if (maxCapabilityCalls <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCapabilityCalls));
            if (maxCapabilityResources <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCapabilityResources));
            if (!Enum.IsDefined(capabilityEffectPolicy))
                throw new ArgumentOutOfRangeException(
                    nameof(capabilityEffectPolicy));
            if (capabilityEffectPolicy ==
                    DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure &&
                capabilityTransactionFactory == null)
                throw new ArgumentException(
                    "RollbackOnFailure richiede una factory transazionale.",
                    nameof(capabilityTransactionFactory));
            MaxInstructions = maxInstructions;
            CancellationToken = cancellationToken;
            this.output = output;
            this.input = input;
            this.capabilityProvider = capabilityProvider;
            this.debugObserver = debugObserver;
            CapabilityTimeout = capabilityTimeout ?? TimeSpan.FromSeconds(30);
            MaxCapabilityCalls = maxCapabilityCalls;
            MaxCapabilityResources = maxCapabilityResources;
            ExecutionDeadline = executionDeadline;
            CapabilityEffectPolicy = capabilityEffectPolicy;
            this.capabilityTransactionFactory = capabilityTransactionFactory;
            LastCapabilityEffects = EmptyCapabilityEffects();
        }

        public int MaxInstructions { get; }
        public CancellationToken CancellationToken { get; }
        public long Instructions => instructions;
        public TimeSpan CapabilityTimeout { get; }
        public int MaxCapabilityCalls { get; }
        public int MaxCapabilityResources { get; }
        public DateTimeOffset? ExecutionDeadline { get; }
        public DdfRuntimeCapabilityEffectPolicy CapabilityEffectPolicy { get; }
        public DdfRuntimeCapabilityEffectReceipt LastCapabilityEffects {
            get; private set;
        }

        public DdfRuntimeExecutionResult Execute(Func<DdfRuntimeContext, object> entryPoint)
        {
            if (entryPoint == null) throw new ArgumentNullException(nameof(entryPoint));
            if (executing) throw new InvalidOperationException(
                "A runtime context cannot execute nested entry points.");
            executing = true;
            instructions = 0;
            capabilityCalls = 0;
            executionId = Guid.NewGuid();
            executionStartedAt = DateTimeOffset.UtcNow;
            lastCapabilityLocation = default;
            capabilityEffects.Clear();
            capabilityTransaction = null;
            capabilityTransactionBeginAttempted = false;
            capabilityTransactionBeginError = string.Empty;
            LastCapabilityEffects = EmptyCapabilityEffects();
            lock (capabilityResourcesSync)
            {
                capabilityResources.Clear();
                capabilityResourcesClosed = false;
            }
            DdfRuntimeExecutionResult result = null;
            Exception executionFailure = null;
            try
            {
                CancellationToken.ThrowIfCancellationRequested();
                BeginCapabilityTransaction();
                object value = entryPoint(this);
                result = new DdfRuntimeExecutionResult(value, false, instructions);
            }
            catch (EndSignal signal)
            {
                result = new DdfRuntimeExecutionResult(signal.Value, true,
                    instructions);
            }
            catch (Exception exception) { executionFailure = exception; }
            finally
            {
                debugFrames.Clear();
                debugGlobals.Clear();
                executing = false;
            }
            DdfRuntimeException cleanupFailure = ReleaseCapabilityResources();
            DdfRuntimeException lifecycleFailure = FinalizeCapabilityEffects(
                executionFailure == null && cleanupFailure == null);
            if (result != null)
                result.CapabilityEffects = LastCapabilityEffects;
            if (executionFailure != null)
                ExceptionDispatchInfo.Capture(executionFailure).Throw();
            if (cleanupFailure != null) throw cleanupFailure;
            if (lifecycleFailure != null) throw lifecycleFailure;
            return result;
        }

        public void EnterFunction(string functionSymbolId,
            DdfRuntimeSourceLocation declaration)
        {
            if (string.IsNullOrWhiteSpace(functionSymbolId))
                throw new ArgumentException("Function symbol id is required.",
                    nameof(functionSymbolId));
            debugFrames.Add(new MutableDebugFrame(functionSymbolId, declaration));
        }

        public void ExitFunction(string functionSymbolId)
        {
            if (debugFrames.Count == 0 || debugFrames[^1].FunctionSymbolId !=
                functionSymbolId)
                throw new InvalidOperationException(
                    "The compiled DDF call stack is not balanced.");
            debugFrames.RemoveAt(debugFrames.Count - 1);
        }

        public void EnterDebugScope()
        {
            RequireDebugFrame().Scopes.Add(new Dictionary<string,
                DebugRegistration>(StringComparer.Ordinal));
        }

        public void ExitDebugScope()
        {
            MutableDebugFrame frame = RequireDebugFrame();
            if (frame.Scopes.Count == 0)
                throw new InvalidOperationException(
                    "The compiled DDF debug scope is not balanced.");
            frame.Scopes.RemoveAt(frame.Scopes.Count - 1);
        }

        public void RegisterDebugParameter(string symbolId, string typeName,
            Func<object> reader) => RequireDebugFrame().Parameters[symbolId] =
            Registration(symbolId, typeName,
                DdfRuntimeDebugVariableKind.Parameter, reader);

        public void RegisterDebugLocal(string symbolId, string typeName,
            Func<object> reader)
        {
            MutableDebugFrame frame = RequireDebugFrame();
            if (frame.Scopes.Count == 0)
                throw new InvalidOperationException(
                    "A debug scope is required before registering a local.");
            frame.Scopes[^1][symbolId] = Registration(symbolId, typeName,
                DdfRuntimeDebugVariableKind.Local, reader);
        }

        public void RegisterDebugGlobal(string symbolId, string typeName,
            Func<object> reader) => debugGlobals[symbolId] = Registration(
            symbolId, typeName, DdfRuntimeDebugVariableKind.Global, reader);

        public T AtCall<T>(DdfRuntimeSourceLocation location,
            Func<T> invocation)
        {
            if (invocation == null) throw new ArgumentNullException(nameof(invocation));
            if (debugFrames.Count == 0) return invocation();
            MutableDebugFrame frame = debugFrames[^1];
            DdfRuntimeSourceLocation previous = frame.Location;
            frame.Location = location;
            try { return invocation(); }
            finally { frame.Location = previous; }
        }

        public void AtCall(DdfRuntimeSourceLocation location, Action invocation)
        {
            if (invocation == null) throw new ArgumentNullException(nameof(invocation));
            if (debugFrames.Count == 0)
            {
                invocation();
                return;
            }
            MutableDebugFrame frame = debugFrames[^1];
            DdfRuntimeSourceLocation previous = frame.Location;
            frame.Location = location;
            try { invocation(); }
            finally { frame.Location = previous; }
        }

        public void Tick(DdfRuntimeSourceLocation location)
        {
            CancellationToken.ThrowIfCancellationRequested();
            instructions++;
            if (instructions > MaxInstructions)
                throw Error("DDF407", "Limite di " + MaxInstructions +
                    " istruzioni superato.", location);
            if (debugFrames.Count > 0)
                debugFrames[^1].Location = location;
            if (debugObserver != null)
                debugObserver.OnSequencePoint(new DdfRuntimeDebugSnapshot(
                    location, instructions, debugFrames.AsEnumerable().Reverse()
                        .Select(frame => new DdfRuntimeDebugFrame(
                            frame.FunctionSymbolId, frame.Declaration,
                            frame.Location, CaptureVariables(frame)))
                        .ToList().AsReadOnly()),
                    CancellationToken);
        }

        public void End(object value = null) => throw new EndSignal(value);

        public void Write(object value)
        {
            output?.Invoke(Format(value));
        }

        public T WriteValue<T>(T value)
        {
            Write(value);
            return value;
        }

        public string ReadLine(DdfRuntimeSourceLocation location)
        {
            if (input == null)
                throw Error("DDF408", "Canale Input non disponibile.", location);
            try { return input() ?? string.Empty; }
            catch (DdfRuntimeException) { throw; }
            catch (Exception exception)
            {
                throw Error("DDF408", "Canale Input non disponibile.",
                    location, exception);
            }
        }

        public object InvokeCapability(string capabilityId,
            IReadOnlyList<object> arguments, DdfRuntimeSourceLocation location)
            => InvokeCapabilityCore(capabilityId, arguments, location, null);

        public object InvokeCapability(string capabilityId,
            IReadOnlyList<object> arguments, DdfRuntimeSourceLocation location,
            DdfRuntimeCapabilityEffectKind effectKind)
            => InvokeCapabilityCore(capabilityId, arguments, location,
                effectKind);

        private object InvokeCapabilityCore(string capabilityId,
            IReadOnlyList<object> arguments, DdfRuntimeSourceLocation location,
            DdfRuntimeCapabilityEffectKind? effectKind)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(capabilityId) || capabilityProvider == null)
                throw Error("DDF409", "Capacità runtime non disponibile: '" +
                    (capabilityId ?? string.Empty) + "'.", location);
            capabilityCalls++;
            if (capabilityCalls > MaxCapabilityCalls)
                throw Error("DDF413", "Limite di " + MaxCapabilityCalls +
                    " chiamate capability superato.", location);
            lastCapabilityLocation = location;
            CapabilityEffectEntry effect = effectKind.HasValue
                ? BeginCapabilityEffect(capabilityId, effectKind.Value) : null;
            using var callCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(CancellationToken);
            callCancellation.CancelAfter(CapabilityTimeout);
            var context = new DdfRuntimeCapabilityInvocationContext(executionId,
                callCancellation.Token, executionStartedAt, ExecutionDeadline,
                RegisterCapabilityResource, capabilityTransaction?.Id);
            try
            {
                IReadOnlyList<object> values = arguments ?? Array.Empty<object>();
                Task<object> operation = capabilityProvider is
                    IDdfRuntimeAsyncCapabilityProvider asyncProvider
                    ? asyncProvider.InvokeAsync(capabilityId, values, context)
                        .AsTask()
                    : Task.Run(() => capabilityProvider.Invoke(capabilityId,
                        values, callCancellation.Token), callCancellation.Token);
                object result = operation.WaitAsync(callCancellation.Token)
                    .GetAwaiter().GetResult();
                CompleteCapabilityEffect(effect);
                return result;
            }
            catch (OperationCanceledException exception)
            {
                FailCapabilityEffect(effect, exception);
                if (CancellationToken.IsCancellationRequested) throw;
                throw Error("DDF414", "Timeout della capability '" +
                    capabilityId + "' dopo " +
                    CapabilityTimeout.TotalMilliseconds.ToString(
                        CultureInfo.InvariantCulture) + " ms.", location);
            }
            catch (DdfRuntimeException exception)
            {
                FailCapabilityEffect(effect, exception);
                throw;
            }
            catch (CapabilityResourceLimitException exception)
            {
                FailCapabilityEffect(effect, exception);
                throw Error("DDF413", exception.Message, location, exception);
            }
            catch (Exception exception)
            {
                FailCapabilityEffect(effect, exception);
                throw Error("DDF409", "La capacità runtime '" + capabilityId +
                    "' non è riuscita.", location, exception);
            }
        }

        public T InvokeCapabilityValue<T>(string capabilityId,
            IReadOnlyList<object> arguments, DdfRuntimeSourceLocation location)
        {
            object value = InvokeCapability(capabilityId, arguments, location);
            return ConvertCapabilityValue<T>(capabilityId, value, location);
        }

        public T InvokeCapabilityValue<T>(string capabilityId,
            IReadOnlyList<object> arguments, DdfRuntimeSourceLocation location,
            DdfRuntimeCapabilityEffectKind effectKind)
        {
            object value = InvokeCapability(capabilityId, arguments, location,
                effectKind);
            return ConvertCapabilityValue<T>(capabilityId, value, location);
        }

        private static T ConvertCapabilityValue<T>(string capabilityId,
            object value, DdfRuntimeSourceLocation location)
        {
            if (value is T typed) return typed;
            if (value == null && default(T) is null) return default;
            throw Error("DDF409", "La capacità runtime '" + capabilityId +
                "' ha restituito un tipo incompatibile.", location);
        }

        public T WriteCapabilityValue<T>(string capabilityId, T value,
            DdfRuntimeSourceLocation location)
        {
            InvokeCapability(capabilityId, new object[] { value }, location,
                DdfRuntimeCapabilityEffectKind.ObjectWrite);
            return value;
        }

        public T ReadCapabilityElement<T>(string capabilityId, int index,
            DdfRuntimeSourceLocation location)
        {
            try
            {
                return InvokeCapabilityValue<T>(capabilityId,
                    new object[] { index }, location);
            }
            catch (DdfRuntimeException exception) when (
                IsCapabilityIndexFailure(exception))
            {
                throw Error("DDF405", "Indice array fuori intervallo: " +
                    index + ".", location, exception.InnerException);
            }
        }

        public T WriteCapabilityElement<T>(string capabilityId, int index,
            T value, DdfRuntimeSourceLocation location)
        {
            try
            {
                InvokeCapability(capabilityId, new object[] { index, value },
                    location, DdfRuntimeCapabilityEffectKind.ObjectWrite);
                return value;
            }
            catch (DdfRuntimeException exception) when (
                IsCapabilityIndexFailure(exception))
            {
                throw Error("DDF405", "Indice array fuori intervallo: " +
                    index + ".", location, exception.InnerException);
            }
        }

        private static bool IsCapabilityIndexFailure(
            DdfRuntimeException exception) => exception?.Code == "DDF409" &&
            (exception.InnerException is IndexOutOfRangeException ||
             exception.InnerException is ArgumentOutOfRangeException);

        private DdfRuntimeCapabilityEffectReceipt EmptyCapabilityEffects() =>
            new DdfRuntimeCapabilityEffectReceipt(CapabilityEffectPolicy,
                DdfRuntimeCapabilityEffectDisposition.None,
                Array.Empty<DdfRuntimeCapabilityEffectRecord>());

        private CapabilityEffectEntry BeginCapabilityEffect(string capabilityId,
            DdfRuntimeCapabilityEffectKind kind)
        {
            var entry = new CapabilityEffectEntry
            {
                Sequence = capabilityEffects.Count + 1,
                CapabilityId = capabilityId ?? string.Empty,
                Kind = kind,
                Status = DdfRuntimeCapabilityEffectStatus.Started,
                Error = string.Empty
            };
            capabilityEffects.Add(entry);
            return entry;
        }

        private static void CompleteCapabilityEffect(
            CapabilityEffectEntry entry)
        {
            if (entry != null)
                entry.Status = DdfRuntimeCapabilityEffectStatus.Applied;
        }

        private static void FailCapabilityEffect(CapabilityEffectEntry entry,
            Exception exception)
        {
            if (entry == null || entry.Status ==
                DdfRuntimeCapabilityEffectStatus.Applied) return;
            entry.Status = DdfRuntimeCapabilityEffectStatus.Failed;
            entry.Error = exception?.Message ?? string.Empty;
        }

        private IReadOnlyList<DdfRuntimeCapabilityEffectRecord>
            SnapshotCapabilityEffects() => capabilityEffects.Select(entry =>
                new DdfRuntimeCapabilityEffectRecord(entry.Sequence,
                    entry.CapabilityId, entry.Kind, entry.Status,
                    entry.Error)).ToList().AsReadOnly();

        private void BeginCapabilityTransaction()
        {
            if (CapabilityEffectPolicy !=
                DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure) return;
            capabilityTransactionBeginAttempted = true;
            try
            {
                var context = new DdfRuntimeCapabilityTransactionContext(
                    executionId, executionStartedAt, ExecutionDeadline,
                    CancellationToken);
                capabilityTransaction = capabilityTransactionFactory
                    .BeginAsync(context).AsTask().WaitAsync(CapabilityTimeout,
                        CancellationToken).GetAwaiter().GetResult();
                if (capabilityTransaction == null)
                    throw new InvalidOperationException(
                        "La factory ha restituito una transazione nulla.");
                if (string.IsNullOrWhiteSpace(capabilityTransaction.Id))
                    throw new InvalidOperationException(
                        "La transazione deve avere un identificatore.");
            }
            catch (OperationCanceledException) when
                (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                capabilityTransactionBeginError = exception.Message;
                throw Error("DDF420",
                    "Avvio della transazione capability non riuscito: " +
                    exception.Message, lastCapabilityLocation, exception);
            }
        }

        private DdfRuntimeException FinalizeCapabilityEffects(bool succeeded)
        {
            IReadOnlyList<DdfRuntimeCapabilityEffectRecord> effects =
                SnapshotCapabilityEffects();
            if (CapabilityEffectPolicy ==
                DdfRuntimeCapabilityEffectPolicy.Immediate)
            {
                LastCapabilityEffects = new DdfRuntimeCapabilityEffectReceipt(
                    CapabilityEffectPolicy,
                    effects.Count == 0
                        ? DdfRuntimeCapabilityEffectDisposition.None
                        : succeeded
                            ? DdfRuntimeCapabilityEffectDisposition.Applied
                            : DdfRuntimeCapabilityEffectDisposition
                                .PartiallyApplied,
                    effects);
                return null;
            }

            if (capabilityTransaction == null)
            {
                LastCapabilityEffects = new DdfRuntimeCapabilityEffectReceipt(
                    CapabilityEffectPolicy,
                    capabilityTransactionBeginAttempted
                        ? DdfRuntimeCapabilityEffectDisposition.BeginFailed
                        : DdfRuntimeCapabilityEffectDisposition.None,
                    effects, error: capabilityTransactionBeginError);
                return null;
            }

            string transactionId = capabilityTransaction.Id;
            DdfRuntimeCapabilityEffectDisposition disposition;
            Exception lifecycleFailure = null;
            string lifecycleError = string.Empty;
            try
            {
                using var operationCancellation =
                    new CancellationTokenSource(CapabilityTimeout);
                if (succeeded)
                {
                    capabilityTransaction.CommitAsync(
                            operationCancellation.Token).AsTask()
                        .WaitAsync(operationCancellation.Token)
                        .GetAwaiter().GetResult();
                    disposition =
                        DdfRuntimeCapabilityEffectDisposition.Committed;
                }
                else
                {
                    capabilityTransaction.RollbackAsync(
                            operationCancellation.Token).AsTask()
                        .WaitAsync(operationCancellation.Token)
                        .GetAwaiter().GetResult();
                    disposition =
                        DdfRuntimeCapabilityEffectDisposition.RolledBack;
                }
            }
            catch (Exception exception)
            {
                lifecycleFailure = exception;
                lifecycleError = exception.Message;
                disposition = succeeded
                    ? DdfRuntimeCapabilityEffectDisposition.CommitFailed
                    : DdfRuntimeCapabilityEffectDisposition.RollbackFailed;
            }
            finally
            {
                try
                {
                    capabilityTransaction.DisposeAsync().AsTask()
                        .WaitAsync(CapabilityTimeout).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    lifecycleError = AppendCapabilityError(lifecycleError,
                        exception.Message);
                    lifecycleFailure ??= exception;
                }
                capabilityTransaction = null;
            }
            LastCapabilityEffects = new DdfRuntimeCapabilityEffectReceipt(
                CapabilityEffectPolicy, disposition, effects, transactionId,
                lifecycleError);
            if (lifecycleFailure == null) return null;
            string code = disposition ==
                DdfRuntimeCapabilityEffectDisposition.CommitFailed ||
                disposition ==
                    DdfRuntimeCapabilityEffectDisposition.RollbackFailed
                ? "DDF420" : "DDF415";
            return Error(code, "Finalizzazione della transazione capability " +
                "non riuscita: " + lifecycleError, lastCapabilityLocation,
                lifecycleFailure);
        }

        private static string AppendCapabilityError(string current,
            string additional) => string.IsNullOrEmpty(current)
            ? additional ?? string.Empty
            : current + "; " + (additional ?? string.Empty);

        private sealed class CapabilityResourceLimitException : Exception
        {
            internal CapabilityResourceLimitException(string message)
                : base(message) { }
        }

        private void RegisterCapabilityResource(object resource)
        {
            lock (capabilityResourcesSync)
            {
                if (capabilityResourcesClosed)
                {
                    DisposeCapabilityResource(resource);
                    throw new ObjectDisposedException(
                        nameof(DdfRuntimeCapabilityInvocationContext),
                        "La sessione capability è già terminata.");
                }
                if (capabilityResources.Any(item =>
                        ReferenceEquals(item, resource))) return;
                if (capabilityResources.Count >= MaxCapabilityResources)
                {
                    DisposeCapabilityResource(resource);
                    throw new CapabilityResourceLimitException("Limite di " +
                        MaxCapabilityResources +
                        " risorse capability superato.");
                }
                capabilityResources.Add(resource);
            }
        }

        private DdfRuntimeException ReleaseCapabilityResources()
        {
            List<object> resources;
            lock (capabilityResourcesSync)
            {
                capabilityResourcesClosed = true;
                resources = capabilityResources.ToList();
                capabilityResources.Clear();
            }
            resources.Reverse();
            Exception firstFailure = null;
            foreach (object resource in resources)
            {
                try { DisposeCapabilityResource(resource); }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                }
            }
            return firstFailure == null ? null : Error("DDF415",
                "Errore durante il rilascio di una risorsa capability: " +
                firstFailure.Message, lastCapabilityLocation, firstFailure);
        }

        private void DisposeCapabilityResource(object resource)
        {
            if (resource is IAsyncDisposable asyncResource)
                asyncResource.DisposeAsync().AsTask()
                    .WaitAsync(CapabilityTimeout).GetAwaiter().GetResult();
            else if (resource is IDisposable disposable)
                Task.Run(disposable.Dispose).WaitAsync(CapabilityTimeout)
                    .GetAwaiter().GetResult();
        }

        public static string Format(object value)
        {
            if (value == null) return "null";
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is double floating)
                return floating.ToString("R", CultureInfo.InvariantCulture);
            if (value is Array array) return "[" + array.Length + "]";
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString() ?? string.Empty;
        }

        internal static DdfRuntimeException Error(string code, string message,
            DdfRuntimeSourceLocation location, Exception inner = null) =>
            new DdfRuntimeException(code, message, location, inner);

        private sealed class MutableDebugFrame
        {
            internal MutableDebugFrame(string functionSymbolId,
                DdfRuntimeSourceLocation declaration)
            {
                FunctionSymbolId = functionSymbolId;
                Declaration = declaration;
                Location = declaration;
            }

            internal string FunctionSymbolId { get; }
            internal DdfRuntimeSourceLocation Declaration { get; }
            internal DdfRuntimeSourceLocation Location { get; set; }
            internal Dictionary<string, DebugRegistration> Parameters { get; } =
                new(StringComparer.Ordinal);
            internal List<Dictionary<string, DebugRegistration>> Scopes { get; } =
                new();
        }

        private sealed class DebugRegistration
        {
            internal string SymbolId { get; init; }
            internal string TypeName { get; init; }
            internal DdfRuntimeDebugVariableKind Kind { get; init; }
            internal Func<object> Reader { get; init; }
        }

        private MutableDebugFrame RequireDebugFrame() => debugFrames.Count == 0
            ? throw new InvalidOperationException(
                "A compiled DDF debug frame is required.")
            : debugFrames[^1];

        private static DebugRegistration Registration(string symbolId,
            string typeName, DdfRuntimeDebugVariableKind kind,
            Func<object> reader)
        {
            if (string.IsNullOrWhiteSpace(symbolId))
                throw new ArgumentException("Symbol id is required.",
                    nameof(symbolId));
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Type name is required.",
                    nameof(typeName));
            return new DebugRegistration
            {
                SymbolId = symbolId,
                TypeName = typeName,
                Kind = kind,
                Reader = reader ?? throw new ArgumentNullException(nameof(reader))
            };
        }

        private IReadOnlyList<DdfRuntimeDebugValue> CaptureVariables(
            MutableDebugFrame frame)
        {
            var registrations = new Dictionary<string, DebugRegistration>(
                debugGlobals, StringComparer.Ordinal);
            foreach (KeyValuePair<string, DebugRegistration> item in
                     frame.Parameters)
                registrations[item.Key] = item.Value;
            foreach (Dictionary<string, DebugRegistration> scope in frame.Scopes)
                foreach (KeyValuePair<string, DebugRegistration> item in scope)
                    registrations[item.Key] = item.Value;
            return registrations.Values.OrderBy(item => item.Kind)
                .ThenBy(item => item.SymbolId, StringComparer.Ordinal)
                .Select(CaptureValue).ToList().AsReadOnly();
        }

        private static DdfRuntimeDebugValue CaptureValue(
            DebugRegistration registration)
        {
            return CaptureValue(registration.SymbolId, registration.TypeName,
                registration.Kind, registration.Reader(), new DebugCaptureState(), 0);
        }

        private static DdfRuntimeDebugValue CaptureValue(string symbolId,
            string typeName, DdfRuntimeDebugVariableKind kind, object value,
            DebugCaptureState state, int depth)
        {
            state.Values++;
            var children = new List<DdfRuntimeDebugValue>();
            bool truncated = state.Values > DebugMaximumValues;
            if (!truncated && depth >= DebugMaximumDepth &&
                (value is Array || value is IDdfRuntimeDebugStructure))
                truncated = true;
            bool tracked = value is Array || value is IDdfRuntimeDebugStructure;
            if (!truncated && tracked && !state.Active.Add(value))
            {
                return new DdfRuntimeDebugValue(symbolId, typeName, kind,
                    "<cycle>", Array.Empty<DdfRuntimeDebugValue>(), true);
            }
            if (!truncated && value is Array array)
            {
                int count = Math.Min(array.Length, DebugMaximumChildren);
                for (int index = 0; index < count; index++)
                {
                    object child = array.GetValue(index);
                    string elementType = ArrayElementType(typeName);
                    children.Add(CaptureValue(symbolId + "[" + index.ToString(
                            CultureInfo.InvariantCulture) + "]", elementType,
                        DdfRuntimeDebugVariableKind.Element, child, state,
                        depth + 1));
                    if (state.Values >= DebugMaximumValues)
                    {
                        truncated = index + 1 < array.Length;
                        break;
                    }
                }
                truncated |= array.Length > count;
            }
            else if (!truncated && value is IDdfRuntimeDebugStructure structure)
            {
                IReadOnlyList<DdfRuntimeDebugMember> members =
                    structure.GetDebugMembers() ?? Array.Empty<DdfRuntimeDebugMember>();
                int count = Math.Min(members.Count, DebugMaximumChildren);
                for (int index = 0; index < count; index++)
                {
                    DdfRuntimeDebugMember member = members[index];
                    children.Add(CaptureValue(member.SymbolId, member.TypeName,
                        DdfRuntimeDebugVariableKind.Field, member.Value, state,
                        depth + 1));
                    if (state.Values >= DebugMaximumValues)
                    {
                        truncated = index + 1 < members.Count;
                        break;
                    }
                }
                truncated |= members.Count > count;
            }
            if (tracked) state.Active.Remove(value);
            return new DdfRuntimeDebugValue(symbolId, typeName, kind,
                FormatDebugValue(value, typeName), children.AsReadOnly(), truncated);
        }

        private sealed class DebugCaptureState
        {
            internal int Values { get; set; }
            internal HashSet<object> Active { get; } = new(
                ReferenceComparer.Instance);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();
            public new bool Equals(object left, object right) =>
                ReferenceEquals(left, right);
            public int GetHashCode(object value) =>
                RuntimeHelpers.GetHashCode(value);
        }

        private static string ArrayElementType(string typeName) =>
            typeName != null && typeName.EndsWith("[]", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 2)
                : "value";

        private static string FormatDebugValue(object value, string typeName)
        {
            if (value == null || value is bool || value is byte ||
                value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long ||
                value is ulong || value is float || value is double ||
                value is decimal || value is char || value is string ||
                value is Array)
                return Format(value);
            return "{" + (typeName ?? "value") + "}";
        }
    }

    public static class DdfRuntimeValues
    {
        public static T RequireNonNull<T>(T value,
            DdfRuntimeSourceLocation location) where T : class
        {
            if (value == null)
                throw DdfRuntimeContext.Error("DDF403",
                    "Un valore DDF non può essere null.", location);
            return value;
        }

        public static int StringLength(string value,
            DdfRuntimeSourceLocation location) =>
            RequireNonNull(value, location).Length;

        public static T[] CreateArray<T>(int length,
            DdfRuntimeSourceLocation location)
        {
            if (length < 0)
                throw DdfRuntimeContext.Error("DDF405",
                    "La lunghezza di un array non può essere negativa.", location);
            return new T[length];
        }

        public static ref T ArrayElement<T>(T[] value, int index,
            DdfRuntimeSourceLocation location)
        {
            RequireNonNull(value, location);
            int checkedIndex = DdfRuntimeNumeric.CheckedIndex(index, value.Length,
                location);
            return ref value[checkedIndex];
        }

        public static char StringCharacterAt(string value, int index,
            DdfRuntimeSourceLocation location)
        {
            RequireNonNull(value, location);
            int checkedIndex = DdfRuntimeNumeric.CheckedIndex(index, value.Length,
                location);
            return value[checkedIndex];
        }
    }
}
