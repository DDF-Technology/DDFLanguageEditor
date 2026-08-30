using System;
using System.Collections.Generic;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private readonly HashSet<int> breakpointLines = new HashSet<int>();
        private DdfDebuggerSession debuggerSession;

        private void initializeDebugger()
        {
            richTextBoxLineNumbers.LineClicked += richTextBoxLineNumbers_LineClicked;
        }

        private void richTextBoxLineNumbers_LineClicked(object sender, LineNumberClickEventArgs e)
        {
            toggleBreakpointAtLine(e.SourceLine);
        }

        private void toggleBreakpointMenuItem_Click(object sender, EventArgs e)
        {
            leaveFoldedView();
            int line = richTextBoxMainEditor.GetLineFromCharIndex(richTextBoxMainEditor.SelectionStart) + 1;
            toggleBreakpointAtLine(line);
        }

        private void toggleBreakpointAtLine(int line)
        {
            if (line <= 0) return;
            if (!breakpointLines.Remove(line)) breakpointLines.Add(line);
            debuggerSession?.SetBreakpoints(breakpointLines);
            updateLineNumbers();
        }

        private void clearBreakpoints()
        {
            breakpointLines.Clear();
            debuggerSession?.SetBreakpoints(breakpointLines);
        }

        private string formatLineNumber(int line)
        {
            return breakpointLines.Contains(line) ? "● " + line : line.ToString();
        }

        private void onDebuggerPaused(DdfDebugPauseInfo pause)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<DdfDebugPauseInfo>(onDebuggerPaused), pause);
                return;
            }

            appendOutput("Breakpoint raggiunto: riga " + pause.Line + ", colonna " + pause.Column + ".");
            navigateToRuntimeSpan(pause.Start, pause.Length);
            updateExecutionCommands(true);
        }
    }
}
