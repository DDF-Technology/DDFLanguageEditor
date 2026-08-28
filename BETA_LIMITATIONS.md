# Beta 0.5.4 limitations

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
- dependency-free regression tests for the editor core;
- repeatable dynamic smoke tests for real WinForms editing sequences.

## Not implemented

- automatic recovery, snapshots or concurrent-edit conflict detection;
- semantic validation or symbol resolution;
- compiler, interpreter or runtime;
- build, run or debug commands;
- project/workspace management;
- extension or plug-in APIs;
- stability guarantees for the DDF syntax.

The folded preview can show multiple collapsed blocks and is read-only. A single
block can be expanded independently; expanding all returns to the original
editable buffer without replacing its text or Undo state.

Completion is syntactic and document-local. It does not yet resolve types,
members, overloads, imports from disk or symbols from other files.

Formatting follows the implemented parser subset and intentionally does not
repair invalid syntax or apply semantic style rules.

New documents are not persisted until they are saved. The editor asks before
discarding known unsaved changes, but does not yet recover content after a crash
or system interruption.
