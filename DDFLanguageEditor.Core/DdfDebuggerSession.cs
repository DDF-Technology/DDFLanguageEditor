using System;
using System.Collections.Generic;
using System.Threading;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfDebugPauseInfo
    {
        internal DdfDebugPauseInfo(int line, int column, int start, int length)
        {
            Line = line;
            Column = column;
            Start = start;
            Length = length;
        }

        public int Line { get; }
        public int Column { get; }
        public int Start { get; }
        public int Length { get; }
    }

    public sealed class DdfDebuggerSession : IDisposable
    {
        private readonly object synchronization = new object();
        private readonly ManualResetEventSlim resumeSignal = new ManualResetEventSlim(true);
        private HashSet<int> breakpointLines = new HashSet<int>();
        private bool isPaused;
        private bool disposed;

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
            }
        }

        public void Continue()
        {
            lock (synchronization)
            {
                if (disposed) return;
                isPaused = false;
                resumeSignal.Set();
            }
        }

        internal bool BeforeStatement(string source, DdfSyntaxNode node, Func<bool> cancellationRequested)
        {
            GetPosition(source, node == null ? 0 : node.Start, out int line, out int column);
            Action<DdfDebugPauseInfo> callback;
            lock (synchronization)
            {
                if (disposed) return true;
                if (!breakpointLines.Contains(line)) return true;

                isPaused = true;
                resumeSignal.Reset();
                callback = Paused;
            }

            callback?.Invoke(new DdfDebugPauseInfo(
                line,
                column,
                Math.Max(0, node == null ? 0 : node.Start),
                Math.Max(1, node == null ? 1 : node.Length)));

            while (!resumeSignal.Wait(25))
            {
                if (cancellationRequested != null && cancellationRequested())
                {
                    Continue();
                    return false;
                }
            }

            return cancellationRequested == null || !cancellationRequested();
        }

        public void Dispose()
        {
            lock (synchronization)
            {
                if (disposed) return;
                disposed = true;
                isPaused = false;
                resumeSignal.Set();
            }
            resumeSignal.Dispose();
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
    }
}
