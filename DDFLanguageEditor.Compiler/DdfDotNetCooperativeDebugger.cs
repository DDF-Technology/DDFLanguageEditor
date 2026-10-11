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
            IEnumerable<DdfDotNetCompiledVariable> children, bool isTruncated)
        {
            SymbolId = symbolId;
            Name = name;
            TypeName = typeName;
            Kind = kind;
            DisplayValue = displayValue;
            Children = children.ToList().AsReadOnly();
            IsTruncated = isTruncated;
        }

        public string SymbolId { get; }
        public string Name { get; }
        public string TypeName { get; }
        public DdfRuntimeDebugVariableKind Kind { get; }
        public string DisplayValue { get; }
        public IReadOnlyList<DdfDotNetCompiledVariable> Children { get; }
        public bool IsTruncated { get; }
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
        private readonly IReadOnlyDictionary<string, int> symbolOwners;
        private IReadOnlyList<DdfDotNetSourceBreakpoint> breakpoints =
            Array.Empty<DdfDotNetSourceBreakpoint>();
        private DdfDotNetDebugCommand command = DdfDotNetDebugCommand.Continue;
        private int resumeDepth;
        private string resumeFunctionSymbolId = string.Empty;
        private long resumeInstruction;
        private bool disposed;
        private DdfDotNetCompiledPause currentPause;

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
                bool atBreakpoint = breakpoints.Any(value =>
                    value.SourcePath == snapshot.Location.DocumentPath &&
                    snapshot.Location.Start >= value.Start &&
                    snapshot.Location.Start < value.Start + value.Length);
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
                            value.Kind != DdfRuntimeDebugVariableKind.Global ||
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
                currentPause = new DdfDotNetCompiledPause(snapshot, frames);
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
                value.TypeName, value.Kind, value.DisplayValue, children,
                value.IsTruncated);
        }
    }
}
