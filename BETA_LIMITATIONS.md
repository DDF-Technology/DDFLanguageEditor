# Beta 0.9.6.1 limitations

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
- persistent current-line highlighting, whitespace markers, indentation guides and word wrap with source-preserving overlays;
- persistent interactive editor overview for diagnostics, search matches, breakpoints and the visible viewport;
- persistent right and bottom palette dimensions and pinned/auto-hide state;
- accessible names, high-contrast application chrome, per-monitor DPI layout and `F6`/`Shift+F6` pane traversal;
- persistent configurable keyboard shortcuts with conflict detection and a restorable default preset;
- optional idle autosave for named documents and Hot Exit recovery for dirty named or untitled buffers;
- previous-session restoration for workspace, ordered tabs, active document, selections and breakpoints;
- opt-out local crash-recovery journal with explicit restore, discard or postpone choice;
- periodic and pre-save external-file conflict detection with side-by-side comparison, reload, keep and postpone choices;
- repeatable p95 performance budgets for input, selection, scroll/gutter, full highlighting and Command Palette opening;
- accelerated multi-document endurance smoke with latency, memory, handle and GDI/USER resource-growth gates;
- versioned DDF language package loadable from a directory or `.ddflang` archive, with atomic validation and embedded fallback;
- declarative lexical rules shared by tokenization, classification, completion prefixes and rename validation;
- active language-package information and fallback diagnostics available from the Help menu;
- deduplicated gutter, Outline and Diagnostics rendering with viewport-preserving syntax-format updates;
- cancellable, versioned workspace loading and source-only buffer snapshots that avoid parsing on the input path;
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

- automatic three-way merging, operating-system file locking or multi-instance recovery coordination;
- cross-file local/member resolution, overload resolution, generics or user-defined conversions;
- compiler, bytecode VM, native runtime or full debugger;
- complete standard library, non-interactive input streams or step-by-step execution;
- project files, dependency graphs or build configurations;
- extension or plug-in APIs;
- user-facing package installation/selection and fully declarative grammar, semantics, runtime or build adapters;
- full text-rendering minimap (the lightweight marker overview is implemented instead);
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

`lexicon.json` now directly defines the complete lexical layer. Grammar, types,
semantics, formatting, runtime, build and standard-library sections still select
fixed, safe DDF adapters; making their rules fully declarative is deliberately
split across the remaining `0.9.6.x` roadmap.

New documents are not persisted until they are saved. Crash recovery and Hot
Exit keep local copies, but they are not a replacement for source control or
backups and cannot automatically merge concurrent versions.
