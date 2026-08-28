# DDFLanguageEditor 0.5.4

This is an unsigned, experimental Windows beta released under the MIT License.

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

The repeatable WinForms smoke now exercises every File, Edit, View and Help command,
including search/replace, completion, formatting, recent files, folding states
and all 16 shortcuts. It uses isolated temporary files and does not persist
smoke entries in the user's recent-file settings.

The IDE now starts maximized with source diagnostics always visible. The old
beta banner has been removed from the editing surface while beta status remains
in version metadata and in the new Help > About popup, together with Fabio De
Deo, www.ddf.technology and the MIT License. A multi-resolution application
icon is embedded in the executable and displayed by the application windows.
The dynamic smoke covers all 19 menu commands, including About.

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
2. Ensure Microsoft .NET Framework 4.8 is installed.
3. Start `DDFLanguageEditor.exe`.

Keep `DDFLanguageEditor.Core.dll` in the same folder as the executable.

Windows may display a reputation warning because the executable is not digitally
signed. Verify the SHA-256 published on the GitHub release before running it.

## Important

- new documents exist only in memory until they are saved;
- automatic crash recovery and concurrent-edit detection are not included;
- no semantic analyzer, compiler, interpreter, debugger or DDF runtime is included;
- the language specification is an unstable historical draft;
- use only disposable example text and copy anything important before closing.

See `BETA_LIMITATIONS.md`, `README.md` and `THIRD_PARTY_NOTICES.md` for the full
scope, build instructions and dependency information.
