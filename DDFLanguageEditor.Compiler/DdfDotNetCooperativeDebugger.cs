using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    public enum DdfDotNetDebugCommand
    {
        Continue,
        StepInto,
        StepOver,
        StepOut,
        Stop
    }

    public sealed class DdfDotNetSourceBreakpoint
    {
        public DdfDotNetSourceBreakpoint(string sourcePath, int start, int length = 1)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path is required.",
                    nameof(sourcePath));
            if (start < 0 || length < 1)
                throw new ArgumentOutOfRangeException(nameof(start));
            SourcePath = sourcePath.Replace('\\', '/');
            Start = start;
            Length = length;
        }

        public string SourcePath { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfDotNetCompiledFrame
    {
        internal DdfDotNetCompiledFrame(string symbolId, string name,
            DdfRuntimeSourceLocation location,
            IEnumerable<DdfDotNetCompiledVariable> variables)
        {
            SymbolId = symbolId;
            Name = name;
            Location = location;
            Variables = variables.ToList().AsReadOnly();
        }

        public string SymbolId { get; }
        public string Name { get; }
        public DdfRuntimeSourceLocation Location { get; }
        public IReadOnlyList<DdfDotNetCompiledVariable> Variables { get; }
    }

    public sealed class DdfDotNetCompiledVariable
    {
        internal DdfDotNetCompiledVariable(string symbolId, string name,
            string typeName, DdfRuntimeDebugVariableKind kind,
            string displayValue,
            IEnumerable<DdfDotNetCompiledVariable> children, bool isTruncated,
            bool isReadOnly, bool isFixed)
        {
            SymbolId = symbolId;
            Name = name;
            TypeName = typeName;
            Kind = kind;
            DisplayValue = displayValue;
            Children = children.ToList().AsReadOnly();
            IsTruncated = isTruncated;
            IsReadOnly = isReadOnly;
            IsFixed = isFixed;
        }

        public string SymbolId { get; }
        public string Name { get; }
        public string TypeName { get; }
        public DdfRuntimeDebugVariableKind Kind { get; }
        public string DisplayValue { get; }
        public IReadOnlyList<DdfDotNetCompiledVariable> Children { get; }
        public bool IsTruncated { get; }
        public bool IsReadOnly { get; }
        public bool IsFixed { get; }
    }

    public sealed class DdfDotNetCompiledPause
    {
        internal DdfDotNetCompiledPause(DdfRuntimeDebugSnapshot snapshot,
            IEnumerable<DdfDotNetCompiledFrame> frames)
        {
            Location = snapshot.Location;
            Instructions = snapshot.Instructions;
            Frames = frames.ToList().AsReadOnly();
        }

        public DdfRuntimeSourceLocation Location { get; }
        public long Instructions { get; }
        public IReadOnlyList<DdfDotNetCompiledFrame> Frames { get; }
    }

    /// <summary>
    /// Cooperative pause controller for compiled DDF verification runners.
    /// Generated code reports only DDF locations and bound symbol ids through
    /// the runtime ABI; this controller never inspects generated C# names.
    /// </summary>
    public sealed class DdfDotNetCooperativeDebugger :
        IDdfRuntimeDebugObserver, IDisposable
    {
        private readonly object sync = new();
        private readonly ManualResetEventSlim paused = new(false);
        private readonly ManualResetEventSlim resume = new(false);
        private readonly IReadOnlyDictionary<string, DdfDotNetDebugSymbol> symbols;
        private readonly IReadOnlyDictionary<string, DdfDotNetDebugSymbol>
            symbolsByStorageName;
        private readonly IReadOnlyDictionary<string, int> symbolOwners;
        private IReadOnlyList<DdfDotNetSourceBreakpoint> breakpoints =
            Array.Empty<DdfDotNetSourceBreakpoint>();
        private DdfDotNetDebugCommand command = DdfDotNetDebugCommand.Continue;
        private int resumeDepth;
        private string resumeFunctionSymbolId = string.Empty;
        private long resumeInstruction;
        private DdfDotNetSourceBreakpoint suppressedBreakpoint;
        private bool disposed;
        private DdfDotNetCompiledPause currentPause;
        private DdfRuntimeDebugSnapshot latestRuntimeSnapshot;

        public DdfDotNetCooperativeDebugger(
            IEnumerable<DdfDotNetSourceMap> sourceMaps)
        {
            if (sourceMaps == null) throw new ArgumentNullException(nameof(sourceMaps));
            List<DdfDotNetSourceMap> maps = sourceMaps.Where(map => map != null)
                .ToList();
            symbols = maps.SelectMany(map => map.Symbols)
                .GroupBy(symbol => symbol.SymbolId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            symbolsByStorageName = maps.SelectMany(map => map.Symbols)
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol.StorageName))
                .GroupBy(symbol => symbol.StorageName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            symbolOwners = maps.SelectMany((map, owner) => map.Symbols.Select(
                    symbol => new { symbol.SymbolId, Owner = owner }))
                .GroupBy(item => item.SymbolId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Owner,
                    StringComparer.Ordinal);
        }

        public DdfDotNetCompiledPause CurrentPause
        {
            get { lock (sync) return currentPause; }
        }

        /// <summary>
        /// Most recent DDF sequence-point snapshot, also while execution is
        /// running. It is used to describe a cooperative host-call suspension
        /// without turning that suspension into a debugger breakpoint.
        /// </summary>
        public DdfDotNetCompiledPause LatestSnapshot
        {
            get
            {
                lock (sync) return latestRuntimeSnapshot == null ? null :
                    ProjectSnapshot(latestRuntimeSnapshot);
            }
        }

        public void SetBreakpoints(
            IEnumerable<DdfDotNetSourceBreakpoint> values)
        {
            var normalized = (values ?? Array.Empty<DdfDotNetSourceBreakpoint>())
                .ToList().AsReadOnly();
            lock (sync) breakpoints = normalized;
        }

        public bool WaitForPause(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            return paused.Wait(timeout);
        }

        public void Resume(DdfDotNetDebugCommand nextCommand)
        {
            lock (sync)
            {
                if (currentPause == null)
                    throw new InvalidOperationException("The compiled runtime is not paused.");
                command = nextCommand;
                resumeDepth = currentPause.Frames.Count;
                resumeFunctionSymbolId = currentPause.Frames.FirstOrDefault()
                    ?.SymbolId ?? string.Empty;
                resumeInstruction = currentPause.Instructions;
                suppressedBreakpoint = breakpoints.FirstOrDefault(value =>
                    Contains(value, currentPause.Location));
                currentPause = null;
                paused.Reset();
                resume.Set();
            }
        }

        public void OnSequencePoint(DdfRuntimeDebugSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            bool mustPause;
            lock (sync)
            {
                ThrowIfDisposed();
                latestRuntimeSnapshot = snapshot;
                DdfDotNetSourceBreakpoint matchingBreakpoint = breakpoints
                    .FirstOrDefault(value => Contains(value, snapshot.Location));
                if (suppressedBreakpoint != null && !SameBreakpoint(
                        suppressedBreakpoint, matchingBreakpoint))
                    suppressedBreakpoint = null;
                bool atBreakpoint = matchingBreakpoint != null &&
                    !SameBreakpoint(matchingBreakpoint, suppressedBreakpoint);
                bool afterResume = snapshot.Instructions > resumeInstruction;
                mustPause = atBreakpoint || (afterResume && command switch
                {
                    DdfDotNetDebugCommand.StepInto => true,
                    DdfDotNetDebugCommand.StepOver =>
                        snapshot.Frames.Count < resumeDepth ||
                        snapshot.Frames.Count == resumeDepth &&
                        snapshot.Frames.FirstOrDefault()?.FunctionSymbolId ==
                        resumeFunctionSymbolId,
                    DdfDotNetDebugCommand.StepOut =>
                        snapshot.Frames.Count < resumeDepth,
                    _ => false
                });
                if (!mustPause) return;
                currentPause = ProjectSnapshot(snapshot);
                resume.Reset();
                paused.Set();
            }
            resume.Wait(cancellationToken);
            lock (sync)
            {
                resume.Reset();
                if (command == DdfDotNetDebugCommand.Stop)
                    throw new OperationCanceledException(
                        "Compiled DDF debugging stopped.", cancellationToken);
            }
        }

        private DdfDotNetCompiledPause ProjectSnapshot(
            DdfRuntimeDebugSnapshot snapshot)
        {
            var frames = snapshot.Frames.Select(frame =>
            {
                if (!symbols.TryGetValue(frame.FunctionSymbolId,
                        out DdfDotNetDebugSymbol function) ||
                    function.Kind != "Function")
                    throw new InvalidOperationException(
                        "Unknown DDF function in compiled stack: " +
                        frame.FunctionSymbolId + ".");
                int functionOwner = symbolOwners[frame.FunctionSymbolId];
                var variables = frame.Variables.Where(value =>
                        value.Kind != DdfRuntimeDebugVariableKind.Global &&
                        value.Kind != DdfRuntimeDebugVariableKind.State ||
                        symbolOwners.TryGetValue(value.SymbolId,
                            out int variableOwner) &&
                        variableOwner == functionOwner)
                    .Select(value =>
                {
                    if (!symbols.TryGetValue(value.SymbolId,
                            out DdfDotNetDebugSymbol variable))
                        throw new InvalidOperationException(
                            "Unknown DDF variable in compiled frame: " +
                            value.SymbolId + ".");
                    return ProjectVariable(value, variable.Name);
                });
                return new DdfDotNetCompiledFrame(frame.FunctionSymbolId,
                    function.Name, frame.Location, variables);
            });
            return new DdfDotNetCompiledPause(snapshot, frames);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                command = DdfDotNetDebugCommand.Stop;
                currentPause = null;
                paused.Set();
                resume.Set();
            }
            // The execution thread may still be leaving Wait(). The managed
            // gates are intentionally left for collection to avoid racing a
            // Stop/Dispose request with that final wake-up.
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(
                nameof(DdfDotNetCooperativeDebugger));
        }

        private static bool Contains(DdfDotNetSourceBreakpoint breakpoint,
            DdfRuntimeSourceLocation location) => breakpoint != null &&
            breakpoint.SourcePath == location.DocumentPath &&
            location.Start >= breakpoint.Start &&
            location.Start < breakpoint.Start + breakpoint.Length;

        private static bool SameBreakpoint(DdfDotNetSourceBreakpoint left,
            DdfDotNetSourceBreakpoint right) => left != null && right != null &&
            left.SourcePath == right.SourcePath && left.Start == right.Start &&
            left.Length == right.Length;

        private DdfDotNetCompiledVariable ProjectVariable(
            DdfRuntimeDebugValue value, string name)
        {
            var children = value.Children.Select(child =>
            {
                if (child.Kind == DdfRuntimeDebugVariableKind.Field)
                {
                    if (!symbols.TryGetValue(child.SymbolId,
                            out DdfDotNetDebugSymbol field) ||
                        field.Kind != "Field")
                        throw new InvalidOperationException(
                            "Unknown DDF field in compiled value: " +
                            child.SymbolId + ".");
                    return ProjectVariable(child, field.Name);
                }
                string suffix = child.SymbolId.StartsWith(value.SymbolId,
                        StringComparison.Ordinal)
                    ? child.SymbolId.Substring(value.SymbolId.Length)
                    : string.Empty;
                return ProjectVariable(child, name + suffix);
            });
            return new DdfDotNetCompiledVariable(value.SymbolId, name,
                value.TypeName, value.Kind, ProjectDisplayValue(value), children,
                value.IsTruncated, value.IsReadOnly, value.IsFixed);
        }

        private string ProjectDisplayValue(DdfRuntimeDebugValue value)
        {
            string prefix = (value.TypeName ?? string.Empty) + ".";
            if (value.DisplayValue != null &&
                value.DisplayValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                string storageName = value.DisplayValue.Substring(prefix.Length);
                if (symbolsByStorageName.TryGetValue(storageName,
                        out DdfDotNetDebugSymbol member) &&
                    member.Kind == "EnumerationMember" &&
                    member.Type == value.TypeName)
                    return prefix + member.Name;
            }
            return value.DisplayValue;
        }
    }
}
