using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Runtime.Tests
{
    internal static class Program
    {
        private sealed class CapabilityProvider : IDdfRuntimeCapabilityProvider
        {
            private readonly int[] values = { 3, 7 };

            public object Invoke(string capabilityId, IReadOnlyList<object> arguments,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (capabilityId == "math.double")
                    return (int)arguments[0] * 2;
                if (capabilityId == "value.write") return null;
                int index = (int)arguments[0];
                if (capabilityId == "array.read") return values[index];
                if (capabilityId == "array.write")
                {
                    values[index] = (int)arguments[1];
                    return null;
                }
                throw new InvalidOperationException("unknown capability");
            }
        }

        private sealed class TrackedResource : IDisposable
        {
            private readonly string name;
            private readonly IList<string> released;
            private readonly bool fail;

            internal TrackedResource(string name, IList<string> released,
                bool fail = false)
            {
                this.name = name;
                this.released = released;
                this.fail = fail;
            }

            public void Dispose()
            {
                released.Add(name);
                if (fail) throw new InvalidOperationException("cleanup " + name);
            }
        }

        private sealed class TrackedAsyncResource : IAsyncDisposable
        {
            private readonly string name;
            private readonly IList<string> released;

            internal TrackedAsyncResource(string name, IList<string> released)
            {
                this.name = name;
                this.released = released;
            }

            public ValueTask DisposeAsync()
            {
                released.Add(name);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class ManagedCapabilityProvider :
            IDdfRuntimeAsyncCapabilityProvider
        {
            private readonly IList<string> released;

            internal ManagedCapabilityProvider(IList<string> released) =>
                this.released = released;

            internal string LastTransactionId { get; private set; }

            public object Invoke(string capabilityId,
                IReadOnlyList<object> arguments,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException(
                    "Il percorso sincrono non deve essere usato.");

            public async ValueTask<object> InvokeAsync(string capabilityId,
                IReadOnlyList<object> arguments,
                DdfRuntimeCapabilityInvocationContext context)
            {
                LastTransactionId = context.TransactionId;
                if (capabilityId == "async.value")
                {
                    await Task.Yield();
                    return 42;
                }
                if (capabilityId == "async.timeout")
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan,
                        context.CancellationToken);
                    return 0;
                }
                if (capabilityId == "resources")
                {
                    context.RegisterResource(new TrackedResource("first",
                        released));
                    context.RegisterAsyncResource(new TrackedAsyncResource(
                        "second", released));
                    return 7;
                }
                if (capabilityId == "resource.limit")
                {
                    context.RegisterResource(new TrackedResource("accepted",
                        released));
                    context.RegisterResource(new TrackedResource("rejected",
                        released));
                    return 0;
                }
                if (capabilityId == "resource.failure")
                {
                    context.RegisterResource(new TrackedResource("broken",
                        released, fail: true));
                    return 0;
                }
                throw new InvalidOperationException("unknown capability");
            }
        }

        private sealed class SuspendingCapabilityProvider :
            IDdfRuntimeAsyncCapabilityProvider
        {
            private readonly TaskCompletionSource<object> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            internal Task Started => started.Task;
            private readonly TaskCompletionSource started = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            internal bool SynchronousPathUsed { get; private set; }

            public object Invoke(string capabilityId,
                IReadOnlyList<object> arguments,
                CancellationToken cancellationToken)
            {
                SynchronousPathUsed = true;
                throw new InvalidOperationException(
                    "Il percorso sincrono non deve essere usato.");
            }

            public async ValueTask<object> InvokeAsync(string capabilityId,
                IReadOnlyList<object> arguments,
                DdfRuntimeCapabilityInvocationContext context)
            {
                started.TrySetResult();
                return await completion.Task.WaitAsync(
                    context.CancellationToken);
            }

            internal void Resume(object value) => completion.TrySetResult(value);
        }

        private sealed class TrackedTransaction :
            IDdfRuntimeCapabilityTransaction
        {
            private readonly IList<string> events;
            private readonly bool failCommit;

            internal TrackedTransaction(string id, IList<string> events,
                bool failCommit = false)
            {
                Id = id;
                this.events = events;
                this.failCommit = failCommit;
            }

            public string Id { get; }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                events.Add("commit");
                if (failCommit)
                    throw new InvalidOperationException("commit failed");
                return ValueTask.CompletedTask;
            }

            public ValueTask RollbackAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                events.Add("rollback");
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                events.Add("dispose");
                return ValueTask.CompletedTask;
            }
        }

        private sealed class TrackedTransactionFactory :
            IDdfRuntimeCapabilityTransactionFactory
        {
            private readonly IList<string> events;
            private readonly bool failBegin;
            private readonly bool failCommit;

            internal TrackedTransactionFactory(IList<string> events,
                bool failBegin = false, bool failCommit = false)
            {
                this.events = events;
                this.failBegin = failBegin;
                this.failCommit = failCommit;
            }

            public ValueTask<IDdfRuntimeCapabilityTransaction> BeginAsync(
                DdfRuntimeCapabilityTransactionContext context)
            {
                events.Add("begin");
                if (failBegin)
                    throw new InvalidOperationException("begin failed");
                return ValueTask.FromResult<IDdfRuntimeCapabilityTransaction>(
                    new TrackedTransaction("tx-42", events, failCommit));
            }
        }

        private sealed class DebugObserver : IDdfRuntimeDebugObserver
        {
            internal readonly List<DdfRuntimeDebugSnapshot> Snapshots = new();

            public void OnSequencePoint(DdfRuntimeDebugSnapshot snapshot,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Snapshots.Add(snapshot);
            }
        }

        private sealed class DebugLeaf : IDdfRuntimeDebugStructure
        {
            internal int Count { get; set; }
            public IReadOnlyList<DdfRuntimeDebugMember> GetDebugMembers() =>
                new[] { new DdfRuntimeDebugMember("field_count", "int", Count) };
        }

        private sealed class DebugNode : IDdfRuntimeDebugStructure
        {
            internal int Value { get; set; }
            internal DebugNode Next { get; set; }
            internal DebugLeaf[] Leaves { get; set; } = Array.Empty<DebugLeaf>();
            public IReadOnlyList<DdfRuntimeDebugMember> GetDebugMembers() =>
                new[]
                {
                    new DdfRuntimeDebugMember("field_value", "int", Value),
                    new DdfRuntimeDebugMember("field_next", "Node", Next),
                    new DdfRuntimeDebugMember("field_leaves", "Leaf[]", Leaves)
                };
        }

        private static readonly DdfRuntimeSourceLocation Location =
            new DdfRuntimeSourceLocation("main.ddf", 12, 4, "s_test");

        private static async Task<int> Main()
        {
            try
            {
                KeepsTheAbiIndependentAndVersioned();
                ValidatesPortablePersistedState();
                EnforcesDdfNumericSemantics();
                PreservesPostfixAndIndexedValueSemantics();
                EnforcesBudgetCancellationIoAndEnd();
                DeniesCapabilitiesByDefault();
                GovernsAsyncCapabilitiesTimeoutsAndResources();
                await SuspendsAndResumesRuntimeCapabilitiesCooperatively();
                GovernsCapabilityEffectsAndTransactions();
                ReportsBoundDebugLocationsAndFrames();
                CapturesTypedRuntimeErrorFramesBeforeUnwind();
                CapturesImmutableDebugValuesAndScopes();
                CapturesStructuredDebugValuesWithinLimits();
                Console.WriteLine("All 13 runtime ABI tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void KeepsTheAbiIndependentAndVersioned()
        {
            Equal("1.11.0", DdfRuntimeAbi.Version);
            string[] references = typeof(DdfRuntimeAbi).Assembly.GetReferencedAssemblies()
                .Select(item => item.Name).ToArray();
            Require(!references.Any(item => item.StartsWith("DDFLanguageEditor",
                    StringComparison.Ordinal)),
                "La runtime ABI dipende da un altro assembly DDFLanguageEditor.");
            Console.WriteLine("PASS runtime ABI 1.11.0 indipendente da Core, UI, SDK e Roslyn");
        }

        private static void ValidatesPortablePersistedState()
        {
            var entries = new[]
            {
                new DdfPersistedStateEntry("module@1.0.0::main.ddf::count",
                    "int", 42),
                new DdfPersistedStateEntry("module@1.0.0::main.ddf::name",
                    "string", "DDF")
            };
            var snapshot = new DdfPersistedStateSnapshot(
                DdfPersistedStateSnapshot.CurrentSchemaVersion, "ddf", "0.13.0",
                entries);
            Equal(2, snapshot.Entries.Count);
            Equal(42, snapshot.ByKey[entries[0].Key].RequireValue("int"));
            bool duplicateRejected = false;
            try
            {
                _ = new DdfPersistedStateSnapshot(
                    DdfPersistedStateSnapshot.CurrentSchemaVersion, "ddf",
                    "0.13.0", new[] { entries[0], entries[0] });
            }
            catch (ArgumentException) { duplicateRejected = true; }
            bool mismatchRejected = false;
            try { entries[0].RequireValue("string"); }
            catch (InvalidOperationException) { mismatchRejected = true; }
            Require(duplicateRejected && mismatchRejected,
                "Lo snapshot state non rifiuta duplicati o tipi incompatibili.");
            Console.WriteLine("PASS snapshot state portabile, immutabile e tipizzato");
        }

        private static async Task
            SuspendsAndResumesRuntimeCapabilitiesCooperatively()
        {
            var provider = new SuspendingCapabilityProvider();
            var context = new DdfRuntimeContext(20,
                capabilityProvider: provider,
                capabilityTimeout: TimeSpan.FromSeconds(2));
            ValueTask<DdfRuntimeExecutionResult> pending = context.ExecuteAsync(
                async runtime => await runtime.InvokeCapabilityValueAsync<int>(
                    "async.suspend", Array.Empty<object>(), Location,
                    DdfRuntimeCapabilityEffectKind.Function));
            await provider.Started.WaitAsync(TimeSpan.FromSeconds(1));
            Require(!pending.IsCompleted,
                "L'esecuzione asincrona non si è sospesa sulla capability.");
            Require(!provider.SynchronousPathUsed,
                "L'esecuzione asincrona ha usato il bridge sincrono.");
            provider.Resume(42);
            DdfRuntimeExecutionResult result = await pending;
            Equal(42, result.Value);
            Equal(DdfRuntimeCapabilityEffectDisposition.Applied,
                result.CapabilityEffects.Disposition);
            Equal(DdfRuntimeCapabilityEffectStatus.Applied,
                result.CapabilityEffects.Effects.Single().Status);
            Console.WriteLine("PASS sospensione e ripresa capability Runtime ABI senza bridge sincrono");
        }

        private static void CapturesTypedRuntimeErrorFramesBeforeUnwind()
        {
            var context = new DdfRuntimeContext(10);
            var outerDeclaration = new DdfRuntimeSourceLocation(
                "main.ddf", 0, 4, "fn_main");
            var callLocation = new DdfRuntimeSourceLocation(
                "main.ddf", 24, 6, "call_divide");
            var innerDeclaration = new DdfRuntimeSourceLocation(
                "math.ddf", 0, 6, "fn_divide");
            DdfRuntimeException failure = Capture(() => context.Execute(runtime =>
            {
                runtime.EnterFunction("fn_main", outerDeclaration);
                try
                {
                    runtime.Tick(callLocation);
                    runtime.EnterFunction("fn_divide", innerDeclaration);
                    try
                    {
                        throw new DdfRuntimeException("DDF404",
                            "Divisione per zero.", new DdfRuntimeSourceLocation(
                                "math.ddf", 31, 1, "division"));
                    }
                    catch (DdfRuntimeException exception)
                    {
                        runtime.CaptureError(exception);
                        throw;
                    }
                    finally { runtime.ExitFunction("fn_divide"); }
                }
                catch (DdfRuntimeException exception)
                {
                    runtime.CaptureError(exception);
                    throw;
                }
                finally { runtime.ExitFunction("fn_main"); }
            }));
            Equal("DDF404", failure.Code);
            Equal(2, failure.Frames.Count);
            Equal("fn_divide", failure.Frames[0].FunctionSymbolId);
            Equal("math.ddf", failure.Frames[0].Declaration.DocumentPath);
            Equal("fn_main", failure.Frames[1].FunctionSymbolId);
            Equal(callLocation, failure.Frames[1].Location);
            Console.WriteLine("PASS stack errori runtime tipizzato catturato prima dell'unwind");
        }

        private static void EnforcesDdfNumericSemantics()
        {
            Equal(42, DdfRuntimeNumeric.Add(19, 23, Location));
            Equal(1024, DdfRuntimeNumeric.Power(2, 10, Location));
            Equal(2.5d, DdfRuntimeNumeric.Divide(5d, 2d, Location));
            Equal(123, DdfRuntimeNumeric.ToInt(" 123 ", Location));
            Equal(1.5d, DdfRuntimeNumeric.ToFloat("1.5", Location));
            Equal("DDF417", Capture(() =>
                DdfRuntimeNumeric.Add(int.MaxValue, 1, Location)).Code);
            Equal("DDF404", Capture(() =>
                DdfRuntimeNumeric.Divide(1, 0, Location)).Code);
            Equal("DDF418", Capture(() =>
                DdfRuntimeNumeric.Multiply(double.MaxValue, 2d, Location)).Code);
            Equal("DDF419", Capture(() =>
                DdfRuntimeNumeric.Power(2, -1, Location)).Code);
            Equal("DDF405", Capture(() =>
                DdfRuntimeNumeric.CheckedIndex(3, 3, Location)).Code);
            Equal(Location, Capture(() =>
                DdfRuntimeNumeric.Divide(1, 0, Location)).Location);
            Console.WriteLine("PASS semantica numerica DDF e diagnostiche runtime stabili");
        }

        private static void EnforcesBudgetCancellationIoAndEnd()
        {
            var output = new List<string>();
            var context = new DdfRuntimeContext(2, output: output.Add,
                input: () => null);
            DdfRuntimeExecutionResult ended = context.Execute(runtime =>
            {
                runtime.Tick(Location);
                runtime.Write(1.5d);
                Equal(string.Empty, runtime.ReadLine(Location));
                runtime.End(7);
                return 0;
            });
            Require(ended.Ended, "end non ha terminato il programma.");
            Equal(7, ended.Value);
            Equal(1L, ended.Instructions);
            Equal("1.5", output.Single());
            var valueOutput = new List<string>();
            var valueContext = new DdfRuntimeContext(1, output: valueOutput.Add);
            Equal(7, valueContext.WriteValue(7));
            Equal("7", valueOutput.Single());
            Equal("[3]", DdfRuntimeContext.Format(new int[3]));
            Equal(3, DdfRuntimeValues.StringLength("DDF", Location));
            Equal("DDF403", Capture(() => DdfRuntimeValues.StringLength(
                null, Location)).Code);
            Equal("DDF407", Capture(() => context.Execute(runtime =>
            {
                runtime.Tick(Location);
                runtime.Tick(Location);
                runtime.Tick(Location);
                return null;
            })).Code);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var cancelledContext = new DdfRuntimeContext(1, cancelled.Token);
            try
            {
                cancelledContext.Execute(runtime => null);
                throw new InvalidOperationException("La cancellazione non è stata osservata.");
            }
            catch (OperationCanceledException) { }
            Equal("DDF408", Capture(() => new DdfRuntimeContext(1)
                .ReadLine(Location)).Code);
            Console.WriteLine("PASS budget, cancellazione, I/O invarianti ed end governati dalla ABI");
        }

        private static void PreservesPostfixAndIndexedValueSemantics()
        {
            int integer = 4;
            Equal(4, DdfRuntimeNumeric.PostIncrement(ref integer, Location));
            Equal(5, integer);
            Equal(5, DdfRuntimeNumeric.PostDecrement(ref integer, Location));
            Equal(4, integer);
            double floating = 1.5d;
            Equal(1.5d, DdfRuntimeNumeric.PostIncrement(ref floating, Location));
            Equal(2.5d, floating);
            int[] values = DdfRuntimeValues.CreateArray<int>(2, Location);
            DdfRuntimeValues.ArrayElement(values, 1, Location) = 9;
            Equal(9, DdfRuntimeValues.ArrayElement(values, 1, Location));
            Equal('D', DdfRuntimeValues.StringCharacterAt("DDF", 0, Location));
            Equal("DDF405", Capture(() => DdfRuntimeValues.CreateArray<int>(
                -1, Location)).Code);
            Equal("DDF405", Capture(() =>
            {
                int ignored = DdfRuntimeValues.ArrayElement(values, 2, Location);
            }).Code);
            Console.WriteLine("PASS postfix checked, array e stringhe indicizzate con errori DDF");
        }

        private static void DeniesCapabilitiesByDefault()
        {
            Equal("DDF409", Capture(() => new DdfRuntimeContext(10)
                .InvokeCapability("math.double", new object[] { 4 }, Location)).Code);
            var context = new DdfRuntimeContext(10,
                capabilityProvider: new CapabilityProvider());
            Equal(8, context.InvokeCapability("math.double",
                new object[] { 4 }, Location));
            Equal(8, context.InvokeCapabilityValue<int>("math.double",
                new object[] { 4 }, Location));
            Equal(5, context.WriteCapabilityValue("value.write", 5, Location));
            Equal(7, context.ReadCapabilityElement<int>("array.read", 1,
                Location));
            Equal(9, context.WriteCapabilityElement("array.write", 1, 9,
                Location));
            Equal(9, context.ReadCapabilityElement<int>("array.read", 1,
                Location));
            Equal("DDF405", Capture(() => context.ReadCapabilityElement<int>(
                "array.read", 3, Location)).Code);
            Equal("DDF405", Capture(() => context.WriteCapabilityElement(
                "array.write", -1, 4, Location)).Code);
            Equal("DDF409", Capture(() => context.InvokeCapabilityValue<string>(
                "math.double", new object[] { 4 }, Location)).Code);
            Equal("DDF409", Capture(() => context.InvokeCapability("unknown",
                Array.Empty<object>(), Location)).Code);
            Console.WriteLine("PASS capability runtime deny-by-default con provider esplicito");
        }

        private static void GovernsAsyncCapabilitiesTimeoutsAndResources()
        {
            var released = new List<string>();
            var provider = new ManagedCapabilityProvider(released);
            var context = new DdfRuntimeContext(20,
                capabilityProvider: provider,
                capabilityTimeout: TimeSpan.FromMilliseconds(100));
            DdfRuntimeExecutionResult result = context.Execute(runtime =>
            {
                Equal(42, runtime.InvokeCapabilityValue<int>("async.value",
                    Array.Empty<object>(), Location));
                return runtime.InvokeCapabilityValue<int>("resources",
                    Array.Empty<object>(), Location);
            });
            Equal(7, result.Value);
            Require(released.SequenceEqual(new[] { "second", "first" }),
                "Le risorse capability non sono state rilasciate in ordine LIFO.");

            var timeout = new DdfRuntimeContext(10,
                capabilityProvider: provider,
                capabilityTimeout: TimeSpan.FromMilliseconds(20));
            Equal("DDF414", Capture(() => timeout.Execute(runtime =>
                runtime.InvokeCapability("async.timeout",
                    Array.Empty<object>(), Location))).Code);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));
                var cancelled = new DdfRuntimeContext(10,
                    cancellation.Token, capabilityProvider: provider,
                    capabilityTimeout: TimeSpan.FromSeconds(1));
                try
                {
                    cancelled.Execute(runtime => runtime.InvokeCapability(
                        "async.timeout", Array.Empty<object>(), Location));
                    throw new InvalidOperationException(
                        "La cancellazione esterna non è stata propagata.");
                }
                catch (OperationCanceledException) when
                    (cancellation.IsCancellationRequested) { }
            }

            var calls = new DdfRuntimeContext(10,
                capabilityProvider: provider, maxCapabilityCalls: 1);
            Equal("DDF413", Capture(() => calls.Execute(runtime =>
            {
                runtime.InvokeCapability("async.value", Array.Empty<object>(),
                    Location);
                return runtime.InvokeCapability("async.value",
                    Array.Empty<object>(), Location);
            })).Code);

            released.Clear();
            var resources = new DdfRuntimeContext(10,
                capabilityProvider: provider, maxCapabilityResources: 1);
            Equal("DDF413", Capture(() => resources.Execute(runtime =>
                runtime.InvokeCapability("resource.limit",
                    Array.Empty<object>(), Location))).Code);
            Require(released.SequenceEqual(new[] { "rejected", "accepted" }),
                "La risorsa rifiutata o quella accettata non è stata rilasciata.");

            released.Clear();
            var cleanup = new DdfRuntimeContext(10,
                capabilityProvider: provider);
            Equal("DDF415", Capture(() => cleanup.Execute(runtime =>
                runtime.InvokeCapability("resource.failure",
                    Array.Empty<object>(), Location))).Code);
            Equal("broken", released.Single());
            Console.WriteLine("PASS capability asincrone, timeout, budget e cleanup LIFO governati");
        }

        private static void GovernsCapabilityEffectsAndTransactions()
        {
            var released = new List<string>();
            var provider = new ManagedCapabilityProvider(released);

            var immediate = new DdfRuntimeContext(10,
                capabilityProvider: provider);
            DdfRuntimeExecutionResult immediateResult = immediate.Execute(runtime =>
                runtime.InvokeCapability("async.value", Array.Empty<object>(),
                    Location, DdfRuntimeCapabilityEffectKind.Function));
            Equal(DdfRuntimeCapabilityEffectDisposition.Applied,
                immediateResult.CapabilityEffects.Disposition);
            Equal(DdfRuntimeCapabilityEffectStatus.Applied,
                immediateResult.CapabilityEffects.Effects.Single().Status);

            var events = new List<string>();
            var committed = new DdfRuntimeContext(10,
                capabilityProvider: provider,
                capabilityEffectPolicy:
                    DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure,
                capabilityTransactionFactory:
                    new TrackedTransactionFactory(events));
            DdfRuntimeExecutionResult committedResult = committed.Execute(runtime =>
                runtime.InvokeCapability("async.value", Array.Empty<object>(),
                    Location, DdfRuntimeCapabilityEffectKind.Function));
            Equal(DdfRuntimeCapabilityEffectDisposition.Committed,
                committedResult.CapabilityEffects.Disposition);
            Equal("tx-42", committedResult.CapabilityEffects.TransactionId);
            Equal("tx-42", provider.LastTransactionId);
            Require(events.SequenceEqual(new[] { "begin", "commit", "dispose" }),
                "La transazione riuscita non segue begin/commit/dispose.");

            events.Clear();
            var rolledBack = new DdfRuntimeContext(10,
                capabilityProvider: provider,
                capabilityEffectPolicy:
                    DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure,
                capabilityTransactionFactory:
                    new TrackedTransactionFactory(events));
            try
            {
                rolledBack.Execute(runtime =>
                {
                    runtime.InvokeCapability("async.value",
                        Array.Empty<object>(), Location,
                        DdfRuntimeCapabilityEffectKind.Function);
                    throw new InvalidOperationException("script failed");
                });
                throw new InvalidOperationException(
                    "L'errore dello script non è stato propagato.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "script failed") { }
            Equal(DdfRuntimeCapabilityEffectDisposition.RolledBack,
                rolledBack.LastCapabilityEffects.Disposition);
            Require(events.SequenceEqual(new[] { "begin", "rollback", "dispose" }),
                "La transazione fallita non segue begin/rollback/dispose.");

            events.Clear();
            var commitFailed = new DdfRuntimeContext(10,
                capabilityProvider: provider,
                capabilityEffectPolicy:
                    DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure,
                capabilityTransactionFactory:
                    new TrackedTransactionFactory(events, failCommit: true));
            Equal("DDF420", Capture(() => commitFailed.Execute(runtime =>
                runtime.InvokeCapability("async.value", Array.Empty<object>(),
                    Location, DdfRuntimeCapabilityEffectKind.Function))).Code);
            Equal(DdfRuntimeCapabilityEffectDisposition.CommitFailed,
                commitFailed.LastCapabilityEffects.Disposition);
            Require(events.SequenceEqual(new[] { "begin", "commit", "dispose" }),
                "Il fallimento del commit non ha rilasciato la transazione.");

            events.Clear();
            var beginFailed = new DdfRuntimeContext(10,
                capabilityProvider: provider,
                capabilityEffectPolicy:
                    DdfRuntimeCapabilityEffectPolicy.RollbackOnFailure,
                capabilityTransactionFactory:
                    new TrackedTransactionFactory(events, failBegin: true));
            Equal("DDF420", Capture(() => beginFailed.Execute(runtime => 0)).Code);
            Equal(DdfRuntimeCapabilityEffectDisposition.BeginFailed,
                beginFailed.LastCapabilityEffects.Disposition);
            Console.WriteLine("PASS ricevuta effetti e transazioni capability commit/rollback governate");
        }

        private static void ReportsBoundDebugLocationsAndFrames()
        {
            Equal(7, new DdfRuntimeContext(10).Execute(runtime =>
                runtime.AtCall(Location, () => 7)).Value);
            var observer = new DebugObserver();
            var context = new DdfRuntimeContext(10, debugObserver: observer);
            context.Execute(runtime =>
            {
                runtime.EnterFunction("f_main", new DdfRuntimeSourceLocation(
                    "main.ddf", 0, 4, "f_main"));
                try
                {
                    runtime.Tick(Location);
                    var callLocation = new DdfRuntimeSourceLocation(
                        "main.ddf", 40, 7, "call_child");
                    runtime.AtCall(callLocation, () =>
                    {
                        runtime.EnterFunction("f_child",
                            new DdfRuntimeSourceLocation(
                                "library.ddf", 2, 5, "f_child"));
                        try
                        {
                            runtime.Tick(new DdfRuntimeSourceLocation(
                                "library.ddf", 20, 3, "s_child"));
                        }
                        finally { runtime.ExitFunction("f_child"); }
                        return 0;
                    });
                    runtime.Tick(Location);
                    return 1;
                }
                finally { runtime.ExitFunction("f_main"); }
            });
            Equal(3, observer.Snapshots.Count);
            Equal(1, observer.Snapshots[0].Frames.Count);
            Equal("f_main", observer.Snapshots[0].Frames[0].FunctionSymbolId);
            Equal(2, observer.Snapshots[1].Frames.Count);
            Equal("f_child", observer.Snapshots[1].Frames[0].FunctionSymbolId);
            Equal("f_main", observer.Snapshots[1].Frames[1].FunctionSymbolId);
            Equal(40, observer.Snapshots[1].Frames[1].Location.Start);
            Equal(Location, observer.Snapshots[2].Frames[0].Location);
            Require(observer.Snapshots[1].Instructions == 2 &&
                    observer.Snapshots[1].Location.DocumentPath == "library.ddf",
                "Lo snapshot debugger non conserva istruzioni o posizione DDF.");
            try
            {
                context.ExitFunction("missing");
                throw new InvalidOperationException(
                    "Uno stack debug sbilanciato è stato accettato.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message.Contains("not balanced",
                    StringComparison.Ordinal)) { }
            Console.WriteLine("PASS observer debugger con sequence point e stack bound bilanciato");
        }

        private static void CapturesImmutableDebugValuesAndScopes()
        {
            var observer = new DebugObserver();
            var context = new DdfRuntimeContext(10, debugObserver: observer);
            int global = 3;
            int state = 4;
            int parameter = 5;
            int[] values = { 7, 8 };
            context.Execute(runtime =>
            {
                runtime.RegisterDebugGlobal("g_total", "int", () => global);
                runtime.RegisterDebugState("s_counter", "int", () => state);
                runtime.EnterFunction("f_main", Location);
                try
                {
                    runtime.RegisterDebugParameter("p_value", "int",
                        () => parameter);
                    runtime.EnterDebugScope();
                    try
                    {
                        runtime.RegisterDebugLocal("l_values", "int[]",
                            () => values);
                        runtime.RegisterDebugLocal("l_fixed", "int", () => 42,
                            true, true);
                        runtime.Tick(Location);
                        parameter = 6;
                        values[0] = 9;
                        runtime.EnterDebugScope();
                        try
                        {
                            int nested = 11;
                            runtime.RegisterDebugLocal("l_nested", "int",
                                () => nested);
                            runtime.Tick(Location);
                        }
                        finally { runtime.ExitDebugScope(); }
                        runtime.Tick(Location);
                    }
                    finally { runtime.ExitDebugScope(); }
                    return 0;
                }
                finally { runtime.ExitFunction("f_main"); }
            });
            Equal(3, observer.Snapshots.Count);
            DdfRuntimeDebugFrame first = observer.Snapshots[0].Frames.Single();
            Equal("5", first.Variables.Single(value =>
                value.SymbolId == "p_value").DisplayValue);
            DdfRuntimeDebugValue firstArray = first.Variables.Single(value =>
                value.SymbolId == "l_values");
            Equal("[2]", firstArray.DisplayValue);
            Equal("7", firstArray.Children[0].DisplayValue);
            Equal("int", firstArray.Children[0].TypeName);
            DdfRuntimeDebugValue fixedValue = first.Variables.Single(value =>
                value.SymbolId == "l_fixed");
            Equal(true, fixedValue.IsReadOnly);
            Equal(true, fixedValue.IsFixed);
            Equal(false, first.Variables.Single(value =>
                value.SymbolId == "g_total").IsReadOnly);
            DdfRuntimeDebugFrame second = observer.Snapshots[1].Frames.Single();
            Equal("6", second.Variables.Single(value =>
                value.SymbolId == "p_value").DisplayValue);
            Equal("9", second.Variables.Single(value =>
                value.SymbolId == "l_values").Children[0].DisplayValue);
            Require(second.Variables.Any(value => value.SymbolId == "l_nested"),
                "Il locale dello scope annidato non è visibile nello snapshot.");
            Require(observer.Snapshots[2].Frames.Single().Variables.All(value =>
                    value.SymbolId != "l_nested"),
                "Un locale uscito dallo scope è rimasto visibile.");
            Equal("3", first.Variables.Single(value =>
                value.SymbolId == "g_total").DisplayValue);
            DdfRuntimeDebugValue stateValue = first.Variables.Single(value =>
                value.SymbolId == "s_counter");
            Equal("4", stateValue.DisplayValue);
            Equal(DdfRuntimeDebugVariableKind.State, stateValue.Kind);
            Console.WriteLine("PASS snapshot valori immutabili con parametri, locali, globali, state, array e scope");
        }

        private static void CapturesStructuredDebugValuesWithinLimits()
        {
            var observer = new DebugObserver();
            var context = new DdfRuntimeContext(10, debugObserver: observer);
            var root = new DebugNode
            {
                Value = 12,
                Leaves = new[] { new DebugLeaf { Count = 4 } }
            };
            root.Next = root;
            int[] many = Enumerable.Range(0,
                DdfRuntimeContext.DebugMaximumChildren + 10).ToArray();
            context.Execute(runtime =>
            {
                runtime.EnterFunction("f_main", Location);
                try
                {
                    runtime.EnterDebugScope();
                    try
                    {
                        runtime.RegisterDebugLocal("l_root", "Node", () => root);
                        runtime.RegisterDebugLocal("l_many", "int[]", () => many);
                        runtime.Tick(Location);
                        root.Value = 99;
                        root.Leaves[0].Count = 8;
                    }
                    finally { runtime.ExitDebugScope(); }
                    return 0;
                }
                finally { runtime.ExitFunction("f_main"); }
            });
            DdfRuntimeDebugFrame frame = observer.Snapshots.Single().Frames.Single();
            DdfRuntimeDebugValue node = frame.Variables.Single(value =>
                value.SymbolId == "l_root");
            Equal("{Node}", node.DisplayValue);
            Equal("12", node.Children.Single(value =>
                value.SymbolId == "field_value").DisplayValue);
            DdfRuntimeDebugValue cycle = node.Children.Single(value =>
                value.SymbolId == "field_next");
            Equal("<cycle>", cycle.DisplayValue);
            Require(cycle.IsTruncated,
                "Il ciclo di una struttura non è stato troncato.");
            DdfRuntimeDebugValue leaves = node.Children.Single(value =>
                value.SymbolId == "field_leaves");
            Equal("4", leaves.Children.Single().Children.Single(value =>
                value.SymbolId == "field_count").DisplayValue);
            DdfRuntimeDebugValue large = frame.Variables.Single(value =>
                value.SymbolId == "l_many");
            Equal(DdfRuntimeContext.DebugMaximumChildren, large.Children.Count);
            Require(large.IsTruncated,
                "Un array oltre il limite non è stato marcato come troncato.");
            Console.WriteLine("PASS strutture e array annidati copiati con cicli e limiti espliciti");
        }

        private static DdfRuntimeException Capture(Action action)
        {
            try { action(); }
            catch (DdfRuntimeException exception) { return exception; }
            throw new InvalidOperationException("Era atteso un errore runtime DDF.");
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("expected <" + expected +
                    "> but was <" + actual + ">");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
