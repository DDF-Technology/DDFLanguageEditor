# Beta 0.9.0 limitations

This release is intended for interface and language-design evaluation only.

## Implemented

- local text editing;
- visible line numbers;
- experimental token and block highlighting;
- four-space Tab, multiline indentation and assisted newline indentation;
- UTF-8 `.ddf` open, save and recent-file workflows;
- unsaved-change prompts, find/replace and document status information;
- formal tokenization, incremental highlighting and lexical diagnostics;
- typed syntax tree, expression precedence and recoverable syntax diagnostics;
- live document outline with symbol navigation;
- matching delimiters and a source-preserving folded preview;
- contextual completion from the language catalog and locally visible symbols;
- idempotent whole-document formatting with caret mapping and one-step Undo;
- document-local symbol resolution, hover, definition navigation and scoped rename;
- recursive folder workspace, shared global completion and cross-file definition navigation;
- primitive/structure/array type checking with stable semantic diagnostics;
- internal AST interpreter with functions, control flow, arrays and structures;
- Run/Stop commands, cooperative cancellation, instruction limit and Output palette;
- navigable runtime diagnostics and DDF call stacks in the Output palette;
- minimal standard library with `print`, `readLine`, `length`, `toInt` and `toFloat`;
- native input dialog and a toolbar for the primary editor commands;
- unified light application chrome with a dark code editor, folded view and gutter;
- SDK-style projects targeting .NET 10 and modern Windows Forms bootstrap/DPI behavior;
- unresolved-name and duplicate-declaration diagnostics;
- dependency-free regression tests for the editor core;
- repeatable dynamic smoke tests for real WinForms editing sequences.

## Not implemented

- automatic recovery, snapshots or concurrent-edit conflict detection;
- cross-file local/member resolution, overload resolution, generics or user-defined conversions;
- compiler, bytecode VM, native runtime or full debugger;
- complete standard library, non-interactive input streams, breakpoints or step-by-step execution;
- project files, dependency graphs, build configurations or multi-document tabs;
- extension or plug-in APIs;
- stability guarantees for the DDF syntax.

The folded preview can show multiple collapsed blocks and is read-only. A single
block can be expanded independently; expanding all returns to the original
editable buffer without replacing its text or Undo state.

Detailed local resolution remains document-local and does not yet resolve
overloads or generic types. A workspace shares top-level symbols and signatures between files,
but does not define imports, dependencies or compilation units.

Formatting follows the implemented parser subset and intentionally does not
repair invalid syntax or apply semantic style rules.

The first interpreter executes only the current document through `main`. It is
bounded to 100,000 syntax-node instructions per run and does not yet load
functions from other workspace documents at runtime. Output through `>>` is
currently textual; interactive input is not defined.

New documents are not persisted until they are saved. The editor asks before
discarding known unsaved changes, but does not yet recover content after a crash
or system interruption.
