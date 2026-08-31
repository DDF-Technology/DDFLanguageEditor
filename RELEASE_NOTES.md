# DDFLanguageEditor 0.9.2.9

This is an unsigned, experimental Windows beta released under the MIT License.

Version 0.9.2.9 makes brace repair structural rather than cursor-relative.
Missing-delimiter diagnostics now retain the owning syntax context. Quick fixes
reconstruct mandatory opening and closing braces on correctly indented lines,
including nested blocks. During recovery, the parser uses block indentation to
avoid consuming an outer closing brace as the closure of an incomplete inner
block. The same provider architecture remains available for future grammar
rules without coupling them to WinForms.

Version 0.9.2.8 corrects the insertion anchor used by missing-token fixes. The
diagnostic underline still identifies the following token that exposed the
syntax error, but punctuation is now inserted immediately after the previous
valid token, even when spaces, comments or blank lines separate the two. Missing
keywords also receive context-aware surrounding spaces.

Version 0.9.2.7 introduces an extensible quick-fix pipeline. Built-in providers
close unterminated strings, block comments and library directives, remove
unrecognized characters and insert tokens or keywords explicitly requested by
the parser. Fixes are available from the editor context menu, `Ctrl+.`, the
Edit menu and the icon bar, and each application remains one native Undo step.
Parser diagnostics now retain their exact insertion point separately from the
visible error span, keeping edits correct both before delimiters and at EOF.

Version 0.9.2.6 replaces solid diagnostic backgrounds with native RichEdit
wavy underlines. Syntax colors and the editor background remain readable, while
hover displays severity, stable code, position and the complete message used by
the Diagnostics palette. Decorations are refreshed incrementally and do not
change source text, selection or the native Undo history. The diagnostic model
also supports distinct warning and error presentation for future language rules.

Version 0.9.2.5 replaces ad-hoc tooltip strings with a structured hover model.
Hover now reports category, type, complete function signature, document origin,
declaration line/column, reference count and the first reference positions.
Contiguous comments beginning with `///` provide source documentation, while
standard-library functions expose descriptions from the runtime catalog. The
same model handles current-document, calculated-type and workspace symbols.

Version 0.9.2.4 adds context-aware snippets to statement completion. Templates
cover functions, the `main` entry point, structures, conditional blocks and all
current loop forms. Expansion follows the indentation of the insertion line;
editable fields are selected in order with `Tab`, can be revisited with
`Shift+Tab`, and the session closes with `Escape` or after its final caret.
The complete template insertion remains one native Undo operation.

Version 0.9.2.3 makes formatted DDF code easier to scan by inserting one blank
line between consecutive instructions and independent control-flow contexts.
The formatter keeps `for` headers compact, avoids empty lines immediately before
closing braces, preserves inline comments and treats `do/while` as one context.

Version 0.9.2.2 adds automatic Signature Help for functions declared in the
current document, indexed workspace documents and the standard runtime catalog.
The popup shows the full DDF signature, its origin and a bold row for the active
parameter. Nested calls are tracked independently, while commas in protected
text do not advance the parameter index. Moving outside the call or pressing
Escape closes the popup without taking focus from the editor.

Version 0.9.2.1 fixes document formatting so the RichEdit control cannot extend
the green character format of a leading comment into the following code. The
editor now invalidates incremental analysis and repaints the complete buffer
after a whole-document formatting replacement.

Version 0.9.2.0 begins the IntelliSense milestone. Completion now ranks symbols
by syntactic context, expected type, actual semantic visibility, prior usage and
source proximity. Local declarations override same-named workspace candidates;
external functions and structures retain their relative source-file origin.
Every popup row shows a category glyph, category name, calculated/declared type
and origin without changing the text inserted by Enter or Tab.

Version 0.9.1.3 completes the 0.9.1 editing-comfort milestone. `Alt+Click` adds
a cursor without losing the existing ones, `Ctrl+D` selects the next token
occurrence and `Ctrl+Shift+L` selects all code occurrences while leaving equal
text in comments and strings untouched. Typing, paired characters, Enter, Tab,
Backspace, Delete, Cut and Paste operate on every active selection as one Undo
step. Duplicate Lines moves to `Ctrl+Shift+D`.

Version 0.9.1.2 adds progressive syntax selection with `Shift+Alt+Right`:
identifier, expression, statement, block, function and whole document. The
reverse command (`Shift+Alt+Left`) uses an independent history for every open
tab. `Ctrl+Shift+\\` moves between matching parentheses, brackets and braces,
ignoring delimiters inside strings and comments. All three actions are exposed
in Edit, the editor context menu and the icon toolbar.

Version 0.9.1.1 adds complete-line operations to the daily editing workflow.
`Ctrl+Shift+D` duplicates, `Alt+Up` and `Alt+Down` move, and `Ctrl+Shift+K` deletes
the current lines while preserving selections and native Undo. Multiline paste
now removes redundant common indentation and aligns the inserted block with the
current DDF scope. Every action is available from Edit, the context menu and
the icon toolbar.

Version 0.9.1.0 begins the editor-comfort phase. Parentheses, brackets, braces,
quotes and apostrophes close automatically, wrap selections and do not duplicate
an existing closing character. Enter expands an empty brace pair into an
indented block, while Backspace removes an empty pair as one undoable action.
Lines and selections can be commented with `Ctrl+/`, and the editor now exposes
a light-themed context menu synchronized with its main editing commands.

Version 0.9.0.1 introduces true multi-document editing. Each tab owns its editor
buffer and therefore retains unsaved text, caret, selection, scroll and native
Undo/Redo while other files are active. Save All, selective tab closing and an
aggregate exit confirmation complete the workflow. Breakpoints now belong to a
document, follow line edits where possible and appear in a navigable palette;
unbound locations are shown separately instead of silently being executed.

This release adds the first formal DDF parser and typed abstract syntax tree.
Declarations, functions, blocks, control-flow statements and precedence-aware
expressions are parsed with recoverable errors. Lexical and syntax diagnostics
now share the navigable editor panel, and the testable core includes separate
lexer and parser corpora.

Version 0.4.1 also centralizes the recognized vocabulary and operator rules in
an extensible language catalog. This prepares the planned comparison with C
without coupling concrete keyword spelling to the lexer or parser.

Version 0.5.1 adds a live, resizable document outline. It indexes libraries,
structures, fields, functions, parameters and variables directly from the
recoverable AST. Double-clicking an entry selects its exact declaration name.

Version 0.5.2 highlights matching `()`, `[]` and `{}` delimiters while ignoring
comments and strings. Multiline AST blocks can be opened in a compact read-only
projection with `Ctrl+M`; visible tokens retain their syntax colors and expanding
returns to the untouched editable source. The gutter keeps original source line
numbers and uses `⋯` for the hidden lines.

Version 0.5.3 adds contextual completion backed by the extensible language
catalog and the recoverable document symbol index. Suggestions appear while
typing or with `Ctrl+Space`, can be navigated from the keyboard and are inserted
as a normal undoable edit. Comments and strings are excluded, while local
symbols are limited to declarations visible at the caret.

Version 0.5.4 adds whole-document formatting through the Edit menu or
`Ctrl+Shift+F`. It applies stable indentation and token spacing while preserving
comments, strings and library directives. Formatting is idempotent, maps the
caret to the corresponding token and is recorded as one undoable edit.

Version 0.5.5 adds document-local semantic symbol resolution. Hovering a
resolved name shows its category, type or signature and declaration line.
`F12` navigates to its definition, while `F2` renames only the declaration and
references belonging to the same lexical scope. Unresolved names and duplicate
declarations are reported as `DDF201` and `DDF202` diagnostics.

Version 0.5.6 adds a small-folder DDF workspace. File > Open Folder recursively
indexes `.ddf` sources and exposes them in a compact explorer beside the
Outline. Completion includes global symbols from other documents, false
unresolved-name diagnostics are suppressed when the workspace provides a
definition, and `F12` opens the target file automatically.

Version 0.6.0 adds the first runtime-independent DDF type checker. It validates
primitive, structure and array types, assignments, operators, boolean
conditions, function arguments and returns. Structure fields can be accessed
with `.`, expression types appear in hover information, and semantic problems
use stable diagnostic codes `DDF301` through `DDF308`. Workspace function
signatures participate in the same checks.
Typing `}` on an otherwise empty line now aligns it with the corresponding
opening block and remains a normal undoable edit.

Version 0.7.0 adds the first internal DDF interpreter. It executes the current
document through `main`, including functions, lexical scopes, assignments,
control flow, arrays and structure fields. `F5` starts execution, `Shift+F5`
requests a cooperative stop, and `value >> Console` writes to the Output
palette. Runtime failures use `DDF401` through `DDF407`; every run is bounded
to 100,000 instructions so an accidental infinite loop cannot freeze the IDE.

Version 0.7.1 introduces the first centralized standard library: `print`,
`readLine`, `length`, `toInt` and `toFloat`. The editor supplies interactive
input through a native dialog while the Core remains UI-independent and fully
testable. Failed conversions produce `DDF408`. A flat icon toolbar now exposes
13 primary file, editing, search, formatting, folding and Run/Stop commands.

Version 0.7.2 aligns every application surface to a shared light palette,
including menus, toolbar, Workspace, Outline, Diagnostics, Output, completion
and all secondary dialogs. Only the source editor, folded projection and line
number gutter retain the dedicated dark code theme. Toolbar and pin glyphs now
use explicit high-contrast colors and are covered by the dynamic UI smoke.

Version 0.7.3 replaces the executable icon resource with a regenerated
multi-resolution ICO whose small frames use a clear navy/orange chevron mark.
Secondary forms open at screen center. Workspace and Outline now use standard
tabs, the right palette occupies the full usable height, and Diagnostics/Output
automatically uses only the width remaining to its left.
Find/Replace now uses an aligned grid for fields and options plus a single,
uniform action row; controls specific to replacement collapse cleanly in Find mode.

Version 0.7.4 captures a structured DDF call stack whenever execution fails.
The Output palette displays the positioned runtime diagnostic followed by user
and standard-function frames. Double-clicking the diagnostic selects the exact
failing expression; double-clicking a frame selects its corresponding call site.
The final runtime report is appended atomically so the UI never exposes a
partially rendered stack while execution commands are being re-enabled.

Version 0.8.0 modernizes all four projects to SDK format and ports the editor,
Core and executable regression suites from .NET Framework 4.8 to .NET 10.
The WinForms bootstrap now uses the modern application configuration and DPI
model while preserving the tested 300-pixel palette geometry. Recent files use
a dependency-free UTF-8 store under the user's local application data folder.

Version 0.8.1 closes the controlled port with an official self-contained
Windows x64 distribution. The repeatable release script validates that the .NET
and Windows Desktop hosts are present, produces a ZIP and writes its SHA-256;
the same package is generated as a GitHub Actions artifact.

Version 0.9.0 starts the interactive debugger. Clicking a source line in the
gutter, or pressing `F9`, toggles a breakpoint marked with `●`. The interpreter
pauses before the corresponding executable statement, selects it in the editor
and turns `F5`/Run into Continue. Stop remains available during a pause and
releases the runtime safely before cancellation. The icon toolbar includes a
dedicated breakpoint command and keeps Run synchronized with Continue.

The repeatable WinForms smoke now exercises every File, Edit, View and Help command,
including search/replace, completion, formatting, semantic navigation, recent files, folding states
and all 22 shortcuts. It uses isolated temporary files and does not persist
smoke entries in the user's recent-file settings.

The IDE now starts maximized with source diagnostics always visible. The old
beta banner has been removed from the editing surface while beta status remains
in version metadata and in the new Help > About popup, together with Fabio De
Deo, www.ddf.technology and the MIT License. A multi-resolution application
icon is embedded in the executable and displayed by the application windows.
The dynamic smoke covers all 26 menu commands, including breakpoint, Run, Stop and About.

The application icon now uses a white rounded tile, while About renders the
embedded high-resolution PNG instead of scaling the small executable icon. The
editor adopts an IDE-oriented dark palette with clearer strings and comments.
The folded preview can keep multiple independent blocks collapsed and expand a
single marker without affecting the others.

Line numbers are left-aligned in a non-selectable gutter. The Outline starts at
a compact width, and both Outline and Diagnostics can be pinned or switched to
auto-hide. The About window now opens centered on the screen.

## Run

1. Extract the complete ZIP to a writable local folder.
2. Start `DDFLanguageEditor.exe`; the required .NET 10 runtime is included.

Keep all extracted files in the same folder as the executable.

Windows may display a reputation warning because the executable is not digitally
signed. Verify the SHA-256 published on the GitHub release before running it.

## Important

- new documents exist only in memory until they are saved;
- automatic crash recovery and concurrent-edit detection are not included;
- the interpreter runs only the current document; no compiler, full debugger, non-interactive input stream or multi-file runtime is included;
- the language specification is an unstable historical draft;
- use only disposable example text and copy anything important before closing.

See `BETA_LIMITATIONS.md`, `README.md` and `THIRD_PARTY_NOTICES.md` for the full
scope, build instructions and dependency information.
