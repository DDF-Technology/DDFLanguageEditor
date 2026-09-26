using System;
using System.Collections.Generic;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSourceBreakpoint
    {
        public DdfSourceBreakpoint(string documentPath, int line)
        { DocumentPath = documentPath ?? string.Empty; Line = line; }
        public string DocumentPath { get; }
        public int Line { get; }
    }

    public enum DdfRuntimeVariableKind
    {
        Local,
        Parameter,
        Global,
        Field,
        Element
    }

    public sealed class DdfDebugPauseInfo
    {
        internal DdfDebugPauseInfo(int line, int column, int start, int length,
            DdfRuntimeStateSnapshot state, string documentPath)
        {
            Line = line;
            Column = column;
            Start = start;
            Length = length;
            State = state;
            DocumentPath = documentPath ?? string.Empty;
        }

        public int Line { get; }
        public int Column { get; }
        public int Start { get; }
        public int Length { get; }
        public DdfRuntimeStateSnapshot State { get; }
        public string DocumentPath { get; }
    }

    public sealed class DdfRuntimeVariableSnapshot
    {
        internal DdfRuntimeVariableSnapshot(string name, string displayValue, int scopeDepth,
            DdfRuntimeVariableKind kind)
        { Name = name; DisplayValue = displayValue; ScopeDepth = scopeDepth; Kind = kind; }
        public string Name { get; }
        public string DisplayValue { get; }
        public int ScopeDepth { get; }
        public DdfRuntimeVariableKind Kind { get; }
        public string TypeName { get; internal set; }
        public IReadOnlyList<DdfRuntimeVariableSnapshot> Children { get; internal set; } = Array.Empty<DdfRuntimeVariableSnapshot>();
        public bool IsTruncated { get; internal set; }
    }

    public sealed class DdfRuntimeStateSnapshot
    {
        internal DdfRuntimeStateSnapshot(IReadOnlyList<DdfRuntimeVariableSnapshot> variables,
            IReadOnlyList<DdfRuntimeStackFrame> callStack,
            IReadOnlyList<DdfRuntimeFrameSnapshot> frames)
        { Variables = variables; CallStack = callStack; Frames = frames; }
        public IReadOnlyList<DdfRuntimeVariableSnapshot> Variables { get; }
        public IReadOnlyList<DdfRuntimeStackFrame> CallStack { get; }
        public IReadOnlyList<DdfRuntimeFrameSnapshot> Frames { get; }
    }

    public sealed class DdfRuntimeFrameSnapshot
    {
        internal DdfRuntimeFrameSnapshot(DdfRuntimeStackFrame frame,
            IReadOnlyList<DdfRuntimeVariableSnapshot> variables)
        { Frame = frame; Variables = variables; }
        public DdfRuntimeStackFrame Frame { get; }
        public IReadOnlyList<DdfRuntimeVariableSnapshot> Variables { get; }
    }

    public sealed class DdfDebuggerSession : IDisposable
    {
        private readonly object synchronization = new object();
        private readonly ManualResetEventSlim resumeSignal = new ManualResetEventSlim(true);
        private HashSet<int> breakpointLines = new HashSet<int>();
        private HashSet<string> sourceBreakpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool isPaused;
        private bool stepIntoPending;
        private bool stepOverPending;
        private int stepOverFrameDepth;
        private bool stepOutPending;
        private int stepOutFrameDepth;
        private bool disposed;
        private int activeWaiters;
        private bool resumeSignalDisposed;

        public Action<DdfDebugPauseInfo> Paused { get; set; }

        public bool IsPaused
        {
            get { lock (synchronization) return isPaused; }
        }

        public void SetBreakpoints(IEnumerable<int> lines)
        {
            var replacement = new HashSet<int>();
            if (lines != null)
                foreach (int line in lines)
                    if (line > 0) replacement.Add(line);

            lock (synchronization)
            {
                ThrowIfDisposed();
                breakpointLines = replacement;
                sourceBreakpoints.Clear();
            }
        }

        public void SetBreakpoints(IEnumerable<DdfSourceBreakpoint> breakpoints)
        {
            var replacement = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (breakpoints != null)
                foreach (DdfSourceBreakpoint breakpoint in breakpoints)
                    if (breakpoint != null && breakpoint.Line > 0)
                        replacement.Add(BreakpointKey(breakpoint.DocumentPath, breakpoint.Line));
            lock (synchronization)
            {
                ThrowIfDisposed();
                breakpointLines.Clear();
                sourceBreakpoints = replacement;
            }
        }

        public void Continue()
        {
            lock (synchronization)
            {
                if (disposed) return;
                stepIntoPending = false;
                stepOverPending = false;
                stepOutPending = false;
                isPaused = false;
                resumeSignal.Set();
            }
        }

        public void StepInto()
        {
            lock (synchronization)
            {
                if (disposed || !isPaused) return;
                stepIntoPending = true;
                stepOverPending = false;
                stepOutPending = false;
                isPaused = false;
                resumeSignal.Set();
            }
        }

        public void StepOver()
        {
            lock (synchronization)
            {
                if (disposed || !isPaused) return;
                stepIntoPending = false;
                stepOverPending = true;
                stepOutPending = false;
                isPaused = false;
                resumeSignal.Set();
            }
        }

        public void StepOut()
        {
            lock (synchronization)
            {
                if (disposed || !isPaused) return;
                stepIntoPending = false;
                stepOverPending = false;
                stepOutPending = true;
                stepOutFrameDepth = stepOverFrameDepth;
                isPaused = false;
                resumeSignal.Set();
            }
        }

        internal bool BeforeStatement(string source, DdfIrStatement node, Func<bool> cancellationRequested,
            Func<DdfRuntimeStateSnapshot> captureState = null, int frameDepth = 0,
            string documentPath = null)
        {
            GetPosition(source, node == null ? 0 : node.Start, out int line, out int column);
            Action<DdfDebugPauseInfo> callback;
            lock (synchronization)
            {
                if (disposed) return true;
                bool stepReached = stepIntoPending || (stepOverPending && frameDepth <= stepOverFrameDepth);
                bool stepOutReached = stepOutPending && frameDepth < stepOutFrameDepth;
                bool breakpointReached = !stepOutPending && (breakpointLines.Contains(line) ||
                    sourceBreakpoints.Contains(BreakpointKey(documentPath, line)));
                if (!breakpointReached && !stepReached && !stepOutReached) return true;

                stepIntoPending = false;
                stepOverPending = false;
                stepOutPending = false;
                stepOverFrameDepth = frameDepth;
                isPaused = true;
                resumeSignal.Reset();
                activeWaiters++;
                callback = Paused;
            }

            callback?.Invoke(new DdfDebugPauseInfo(
                line,
                column,
                Math.Max(0, node == null ? 0 : node.Start),
                Math.Max(1, node == null ? 1 : node.Length), captureState?.Invoke(), documentPath));

            try
            {
                while (!resumeSignal.Wait(25))
                {
                    if (cancellationRequested != null && cancellationRequested())
                    {
                        Continue();
                        return false;
                    }
                }

                lock (synchronization)
                    if (disposed) return false;
                return cancellationRequested == null || !cancellationRequested();
            }
            finally
            {
                ReleaseWaiter();
            }
        }

        public void Dispose()
        {
            bool disposeSignal = false;
            lock (synchronization)
            {
                if (disposed) return;
                disposed = true;
                isPaused = false;
                resumeSignal.Set();
                if (activeWaiters == 0 && !resumeSignalDisposed)
                {
                    resumeSignalDisposed = true;
                    disposeSignal = true;
                }
            }
            if (disposeSignal) resumeSignal.Dispose();
        }

        private void ReleaseWaiter()
        {
            bool disposeSignal = false;
            lock (synchronization)
            {
                activeWaiters--;
                if (disposed && activeWaiters == 0 && !resumeSignalDisposed)
                {
                    resumeSignalDisposed = true;
                    disposeSignal = true;
                }
            }
            if (disposeSignal) resumeSignal.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DdfDebuggerSession));
        }

        private static void GetPosition(string source, int start, out int line, out int column)
        {
            line = 1;
            column = 1;
            int safeStart = Math.Min(Math.Max(0, start), source == null ? 0 : source.Length);
            for (int index = 0; index < safeStart; index++)
            {
                if (source[index] == '\n') { line++; column = 1; }
                else if (source[index] != '\r') column++;
            }
        }

        private static string BreakpointKey(string documentPath, int line) =>
            (documentPath ?? string.Empty).Replace('/', '\\') + "\n" + line;
    }
}
