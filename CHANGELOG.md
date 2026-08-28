# Changelog

## Unreleased

- start the IDE maximized with the source diagnostics panel always visible;
- remove the beta notification banner while retaining beta version metadata;
- add a flat, geometric, multi-resolution application icon on a white rounded tile;
- load the embedded high-resolution PNG in About instead of enlarging the small executable icon;
- adopt an IDE-oriented dark editor palette with higher-contrast strings, comments, types and keywords;
- support multiple simultaneous folded blocks with selective expansion and source-preserving projection mapping;
- left-align line numbers in a dedicated non-selectable gutter that redirects focus to the editor;
- reduce the initial Outline width and add pinned/auto-hide modes to Outline and source diagnostics;
- center the About window on the active screen;
- expand the core suite to 55 tests and the WinForms smoke to 12 editor scenarios;
- add Help > About with author, website, MIT license and beta version details;
- expand the WinForms menu smoke to all 19 commands and cover the startup layout and About popup;

## 0.5.4 - 2026-08-28

- added a catalog-driven whole-document formatter with stable Allman indentation and token spacing;
- preserved comments, strings, library directives and incomplete token streams during formatting;
- added source-to-output caret and selection mapping;
- exposed Format Document in the Edit menu with `Ctrl+Shift+F` and one-step Undo;
- added idempotence, control-flow, array, call, invalid-source and alternative-catalog tests;
- expanded the dependency-free core suite from 47 to 54 tests and the WinForms smoke to 9 editor scenarios;
- expanded menu coverage to 18 commands and 16 shortcuts;

## 0.5.3 - 2026-08-28

- added catalog-driven contextual completion for keywords, types and boolean literals;
- added document-symbol suggestions scoped to the current declaration and caret position;
- completed known library names inside `@@'...'` directives;
- suppressed completion inside comments and strings;
- added an editor popup with automatic activation, `Ctrl+Space`, keyboard navigation and undoable insertion;
- added a discoverable Edit menu command and expanded menu coverage to 17 commands and 15 shortcuts;
- expanded the dependency-free core suite from 42 to 47 tests and the WinForms smoke to 8 editor scenarios;
- suppressed native RichEdit redraw and selection notifications during syntax formatting;
- deferred delimiter highlighting while the mouse is selecting, restoring backward drag and word double-click behavior;
- preserved syntax and diagnostic colors in the read-only folded projection;
- kept the line-number gutter visible in folded view with original source numbers and a `⋯` marker;
- made folding available from declaration headers and refreshed stale ranges when the command is invoked;
- handled `Ctrl+M` directly in the editor so folding does not depend on stale menu state;
- kept syntax and delimiter formatting out of the user's Undo history;
- fixed incremental re-lexing when inserted text shares a prefix with old tokens;
- prevented delimiter matching from consuming a stale lexical snapshot during rapid edits;
- added a deterministic 400-edit lexer smoke and raised the core suite from 39 to 42 tests;
- added a repeatable WinForms smoke covering colored and numbered folding, mouse-selection preservation, library cuts, Undo and 80 rapid edits;
- covered all 17 File, Edit and View menu commands plus their 15 keyboard shortcuts through the real WinForms menu items;
- isolated Open/Save dialogs, temporary files, recent-file persistence and the Windows clipboard during menu smoke tests;
- stopped UI timers during form shutdown and ignored stale folding ranges while a document is being replaced;
- documented the maintained menu coverage matrix in `MENU_TEST_MATRIX.md`.

## 0.5.2 - 2026-08-28

- added token-aware matching for parentheses, brackets and braces;
- ignored delimiter-like characters inside strings and comments;
- derived multiline folding ranges from recoverable AST blocks;
- recorded explicit closing-brace presence to avoid invalid folds in incomplete code;
- added a compact read-only fold projection that never replaces the source buffer;
- added View menu commands and keyboard shortcuts for folding and expansion;
- preserved the editor selection and Undo buffer across folded previews;
- expanded the dependency-free regression suite from 33 to 39 tests;
- verified matching, folding and expansion with a Windows UI smoke test.

## 0.5.1 - 2026-08-28

- added exact declaration-name spans to the typed syntax tree;
- added a UI-independent hierarchical document symbol index;
- indexed libraries, structures, fields, functions, parameters and local variables;
- kept useful symbols available while parsing incomplete source text;
- added a resizable live outline panel with type and signature details;
- added double-click navigation from outline entries to exact declaration names;
- expanded the dependency-free regression suite from 30 to 33 tests;
- verified outline layout, population and navigation with a Windows UI smoke test.

## 0.4.1 - 2026-08-28

- centralized keywords, literals, operators and punctuation in an immutable language definition;
- replaced parser checks for concrete keyword spelling with reusable syntax roles;
- moved operator precedence, associativity and prefix/postfix behavior into the catalog;
- made full and incremental lexing configurable with alternative language definitions;
- added a maintained vocabulary inventory and extension workflow for the future C comparison;
- added tests proving that an alternative vocabulary drives both lexer and parser;
- expanded the dependency-free regression suite from 27 to 30 tests.

## 0.4.0 - 2026-08-28

- added a formal grammar for the supported DDF subset;
- added a typed AST for compilation units, declarations, statements and expressions;
- added a recursive parser with documented precedence and associativity;
- added recoverable syntax diagnostics `DDF101` through `DDF105`;
- unified lexical and syntax diagnostics in the navigable editor panel;
- added valid and invalid parser corpora;
- expanded the dependency-free regression suite from 21 to 27 tests;
- verified that the full editor smoke sample parses without diagnostics.

## 0.3.0 - 2026-08-28

- replaced heuristic classification with a formal DDF token model and lexer;
- added absolute source spans and line/column lexical diagnostics;
- added diagnostics for unterminated strings, block comments and library directives;
- added diagnostics for unrecognized characters;
- added incremental re-lexing and recoloring from a safe boundary near each edit;
- added a navigable diagnostics panel with source highlighting;
- added valid and invalid DDF lexer corpora;
- expanded the dependency-free regression suite from 16 to 21 tests;
- verified diagnostic rendering and panel layout with an automated UI smoke test.

## 0.2.0 - 2026-08-28

- added New, Open, Save and Save As workflows for `.ddf` documents;
- added strict UTF-8 reading and staged UTF-8 writes without a byte-order mark;
- added unsaved-change prompts and a dirty marker in the title and status bar;
- added persistent recent files and standard document keyboard shortcuts;
- added Find and Replace, including case matching and Replace All;
- added line, column, file path and encoding status information;
- added document, recent-file and search services to the testable core;
- expanded the dependency-free regression suite from 9 to 16 tests;
- verified the main window and Find dialog with an automated UI smoke test.

## 0.1.1 - 2026-08-28

- separated syntax classification and editing transformations from WinForms;
- corrected highlighting for full line comments, strings, numbers and longest operators;
- preserved selections and undo history when handling Tab, Shift+Tab and Enter;
- added multiline indentation and outdentation;
- debounced document highlighting and removed repeated font allocations;
- improved visible line-number calculation and disabled word wrapping;
- documented the recognized DDF editor subset and the long-term roadmap;
- added dependency-free regression tests and Windows CI builds.

## 0.1.0-beta.1 - 2026-08-26

- first public MIT-licensed beta;
- explicit in-application beta banner and version identity;
- responsive resizing of the editor and line-number gutter;
- documented implemented features and missing language toolchain;
- documented the non-persistent editor buffer and unsigned build;
- added third-party framework notices.
