# Beta 0.9.4.1 limitations

This release is intended for interface and language-design evaluation only.

## Implemented

- local text editing;
- visible line numbers;
- experimental token and block highlighting;
- configurable tabs/spaces, multiline indentation and assisted newline indentation;
- UTF-8 `.ddf` open, save and recent-file workflows;
- unsaved-change prompts, find/replace and document status information;
- formal tokenization, incremental highlighting and lexical diagnostics;
- typed syntax tree, expression precedence and recoverable syntax diagnostics;
- live document outline with symbol navigation;
- matching delimiters and a source-preserving folded preview;
- context-ranked completion using expected type, semantic scope, frequency and proximity;
- structured completion rows with category glyph, type and local/workspace origin;
- automatic signature help with the current parameter for local, workspace and standard functions;
- context-aware snippets with indentation, navigable fields and one-step insertion Undo;
- structured hover with type, signature, origin, declaration, references and `///` documentation;
- native inline diagnostic waves with full hover messages, without changing source colors, selection or Undo;
- extensible quick fixes for unterminated constructs, bad characters and parser-expected tokens;
- structural brace recovery using parser context, nesting and source indentation;
- background analysis and completion with cancellation and rejection of obsolete snapshots;
- idempotent whole-document formatting with readable blank-line separation, caret mapping and one-step Undo;
- document-local symbol resolution, hover, definition navigation and scoped rename;
- recursive folder workspace, shared global completion and cross-file definition navigation;
- clickable file/structure/function/control-flow breadcrumb driven by the recoverable syntax tree;
- searchable Command Palette generated from current menu actions, shortcuts and enabled state;
- drag-and-drop opening for DDF files and workspace folders, with persisted recent workspaces;
- persistent editor preferences for font, zoom, code theme, indentation, line endings and format-on-save;
- persistent right and bottom palette dimensions and pinned/auto-hide state;
- cancellable workspace-wide text and declaration search with live unsaved-buffer snapshots and navigable results;
- primitive/structure/array type checking with stable semantic diagnostics;
- internal AST interpreter with functions, control flow, arrays and structures;
- Run/Stop commands, cooperative cancellation, instruction limit and Output palette;
- navigable runtime diagnostics and DDF call stacks in the Output palette;
- minimal standard library with `print`, `readLine`, `length`, `toInt` and `toFloat`;
- native input dialog and a toolbar for the primary editor commands;
- unified light application chrome with a dark code editor, folded view and gutter;
- vertically resizable bottom palette and horizontally resizable right palette;
- SDK-style projects targeting .NET 10 and modern Windows Forms bootstrap/DPI behavior;
- unresolved-name and duplicate-declaration diagnostics;
- dependency-free regression tests for the editor core;
- repeatable dynamic smoke tests for real WinForms editing sequences.
- independent document tabs with Save All and preserved native Undo/Redo;
- document-scoped breakpoints with remapping and a navigable status palette;
- automatic pairs, contextual block editing, line comments and an editor context menu;
- duplicate/move/delete line commands and context-aware multiline paste indentation;
- progressive syntax selection with per-tab shrink history and matching-delimiter navigation;
- multiple cursors/selections with token-aware occurrence selection and single-step Undo;

## Not implemented

- crash recovery or concurrent-edit conflict detection;
- cross-file local/member resolution, overload resolution, generics or user-defined conversions;
- compiler, bytecode VM, native runtime or full debugger;
- complete standard library, non-interactive input streams or step-by-step execution;
- project files, dependency graphs or build configurations;
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

Quick fixes only act on diagnostics the parser can identify unambiguously. If
removing a brace leaves a different but still valid single-statement construct,
there is no certain error location or original block intent to reconstruct; the
result must be reviewed manually instead of applying a speculative edit.

The first interpreter executes only the current document through `main`. It is
bounded to 100,000 syntax-node instructions per run and does not yet load
functions from other workspace documents at runtime. Output through `>>` is
currently textual; interactive input is not defined.

New documents are not persisted until they are saved. The editor asks before
discarding known unsaved changes, but does not yet recover content after a crash
or system interruption.
