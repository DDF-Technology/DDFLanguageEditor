# Beta 0.11.0 limitations

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
- periodic and pre-save external-file conflict detection with side-by-side comparison, reload, keep and postpone choices, suppressing line-ending-only false conflicts;
- repeatable p95 performance budgets for input, selection, scroll/gutter, full highlighting and Command Palette opening;
- accelerated multi-document endurance smoke with latency, memory, handle and GDI/USER resource-growth gates;
- versioned DDF language package selectable at runtime from a directory or `.ddflang` archive, with persistent choice, atomic validation and embedded fallback;
- declarative lexical rules shared by tokenization, classification, completion prefixes and rename validation;
- canonical EBNF grammar loaded and validated from the language package, with generic typed-by-kind syntax nodes;
- extensible keyword role identifiers and DDF `if/else` parsing, analysis and execution;
- package-driven IDE services plus active-package selection, information and fallback diagnostics available from the Help menu;
- persistent package installation/update, activation and disable-to-embedded workflows;
- isolated token/tree/diagnostic/formatter playground and declarative package tests enforced before activation;
- declarative primitive types, conversions, compatibility, mutability, scopes and function-signature policies;
- versioned safe semantic-operation bindings and a common source-positioned intermediate representation consumed by Run and debugger;
- explicit diagnostic domains for source, language definitions, engine capabilities and runtime failures;
- inert native-extension requirements pinned by ID, publisher, version and SHA-256, with separate deny-by-default capability authorization;
- deduplicated gutter, Outline and Diagnostics rendering with viewport-preserving syntax-format updates;
- cancellable, versioned workspace loading and source-only buffer snapshots that avoid parsing on the input path;
- cancellable workspace-wide text and declaration search with live unsaved-buffer snapshots and navigable results;
- primitive/structure/array type checking with stable semantic diagnostics;
- syntax-independent IR virtual machine with controlled value, scope, call, flow, array and structure operations;
- package-defined standard-function signatures and safe host bindings, with immutable runtime state prepared at breakpoints;
- separate project manifest with exact language version, sources, entry point, dependency declarations, backend, output and options;
- Validate/Build/Run/Debug/Stop commands, cooperative cancellation, instruction limit and navigable Output palette;
- deterministic JSON build receipts and optional unsaved named buffers without implicit source writes;
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
- per-document viewport preservation across formatting, save, tab switches and restored project sessions;
- immutable Variables, Call Stack and project-persistent Watch palettes, including side-effect-free arithmetic, comparison, logical, member and index expressions;
- Step Into (`F11`), Step Over (`F10`) and Step Out (`Shift+F11`) with synchronized source highlighting, Variables, Watch and Call Stack;
- project-wide runtime modules with cross-file functions, structures, globals, breakpoints, diagnostics and unsaved-buffer snapshots;

## Not implemented

- automatic three-way merging, operating-system file locking or multi-instance recovery coordination;
- cross-file local/member resolution, overload resolution, generics or user-defined conversions;
- serialized executable bytecode, native runtime or full debugger UI;
- complete standard library or non-interactive input streams;
- dependency resolution/distribution, configuration matrices or compiled backend implementations;
- native plug-in loading, isolation, registry and consent UI;
- visual package-authoring editor, online registry/download, package uninstall and user installation of additional build backends;
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

The first interpreter executes only the selected entry source through the unique `main`. It is
bounded to 100,000 IR-node instructions per run and does not yet load
functions from other workspace documents at runtime. Output through `>>` is
currently textual; interactive input is not defined.

`lexicon.json`, `grammar.ebnf`, `types.json` and `semantics.json` now define their
respective layers through validated data and safe operation identifiers.
Formatting, runtime, build and standard-library sections use fixed, versioned
safe adapters. Packages may configure their supported mappings, but genuinely
new engine operations or host bindings still require an engine update or an
explicitly authorized plug-in.

New documents are not persisted until they are saved. Crash recovery and Hot
Exit keep local copies, but they are not a replacement for source control or
backups and cannot automatically merge concurrent versions.
