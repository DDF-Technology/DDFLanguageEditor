# Changelog

## 0.9.2.8 - 2026-08-31

- anchor missing-token quick fixes after the previous valid token instead of on the token that exposed the parser error;
- preserve intervening spaces, comments and blank lines when inserting punctuation on the preceding statement;
- add context-aware spacing around missing keyword fixes;
- cover the reported multiline semicolon scenario in Core and real WinForms smoke tests, bringing Core coverage to 123 tests;

## 0.9.2.7 - 2026-08-31

- add an extensible provider-based quick-fix service independent from WinForms;
- close unterminated strings, comments and library directives, remove bad characters and insert parser-expected tokens;
- preserve exact parser insertion positions so fixes before closing delimiters and at end of file remain correct;
- expose fixes from the editor context menu, `Ctrl+.`, the Edit menu and a new toolbar button with native Undo;
- expand regression coverage to 122 Core tests, 39 menu commands, 35 shortcuts and 28 toolbar actions;

## 0.9.2.6 - 2026-08-31

- replace diagnostic background fills with native RichEdit wavy underlines;
- preserve syntax colors, source selection and native Undo while diagnostics update incrementally;
- show the complete diagnostic code, severity, position and message on editor hover;
- keep source and folded views synchronized with the same diagnostic collection;
- introduce extensible error/warning severity styling and expand coverage to 119 Core tests plus an inline-diagnostic WinForms smoke scenario;

## 0.9.2.5 - 2026-08-31

- centralize structured hover data independently from the WinForms tooltip;
- show symbol category, calculated/declared type, DDF signature, document origin and declaration position;
- count references and display their first source positions for local and workspace declarations;
- extract contiguous `///` documentation and document every standard runtime function;
- expand regression coverage to 118 Core tests plus local, standard and workspace hover smoke scenarios;

## 0.9.2.4 - 2026-08-31

- add a central snippet catalog for `if`, `while`, `do`, `for`, functions, `main` and structures;
- expose snippets only in statement completion context and respect alternative language catalogs;
- expand templates relative to the current indentation and navigate editable fields with `Tab`/`Shift+Tab`;
- preserve a single native Undo step for insertion and close the active session with `Escape`;
- expand regression coverage to 115 Core tests plus a dynamic WinForms snippet scenario;

## 0.9.2.3 - 2026-08-31

- insert one blank line between consecutive formatted statements and independent control-flow contexts;
- avoid blank lines inside `for` headers and immediately before closing braces;
- retain `do { ... } while (...);` as one visual context and preserve inline comment attachment;
- expand regression coverage to 113 Core tests plus the dynamic formatting smoke;

## 0.9.2.2 - 2026-08-31

- show function signatures automatically while the caret is inside local, workspace or standard-library calls;
- track the active parameter across nested calls while ignoring commas inside strings and comments;
- expose signature origin and a bold current-parameter row without stealing editor focus;
- expand regression coverage to 112 Core tests plus a dynamic WinForms Signature Help scenario;

## 0.9.2.1 - 2026-08-31

- repaint the complete RichEdit buffer after document formatting so a leading comment cannot leak its green character format into unchanged code;
- extend the WinForms formatting smoke with explicit comment and keyword color assertions;

## 0.9.2.0 - 2026-08-31

- rank completion by statement/expression/type context, expected type, semantic scope, usage frequency and proximity;
- expose only symbols genuinely visible at the caret, including correct shadowing and scope exit behavior;
- retain relative file origins for workspace completion while preferring local declarations on duplicate names;
- render structured popup rows with category glyph, category, type and origin metadata;
- expose completion on the icon toolbar and expand coverage to 107 Core tests, 38 menu commands, 34 shortcuts and 27 toolbar actions;

## 0.9.1.3 - 2026-08-30

- add persistent per-tab multiple cursors through `Alt+Click` with visible secondary selections;
- select the next token occurrence with `Ctrl+D` or all code occurrences with `Ctrl+Shift+L`;
- apply typing, paired characters, Enter, Tab, Backspace, Delete, Cut and Paste to all selections as one Undo step;
- move Duplicate Lines to `Ctrl+Shift+D` and expose occurrence commands in Edit, context menu and toolbar;
- complete roadmap milestone 0.9.1 with 102 Core tests, 26 toolbar commands, 38 menu commands and 34 shortcuts;

## 0.9.1.2 - 2026-08-30

- add AST-aware progressive selection from token through expression, statement, block, function and document;
- retain an independent shrink-selection history for each open document tab;
- navigate between matching parentheses, brackets and braces while ignoring strings and comments;
- expose the three navigation commands in Edit, the context menu, keyboard shortcuts and the icon toolbar;
- expand regression coverage to 98 Core tests, 24 toolbar commands, 36 menu commands and 32 shortcuts;

## 0.9.1.1 - 2026-08-30

- duplicate the current lines with `Ctrl+D` while retaining the corresponding caret or selection;
- move complete selected lines with `Alt+Up` and `Alt+Down` as one native Undo operation;
- delete complete lines with `Ctrl+Shift+K`, including correct first/last-line newline handling;
- normalize and reindent multiline clipboard text relative to the current code block;
- expose all line operations in Edit, the editor context menu and the main icon toolbar;
- expand regression coverage to 94 Core tests, 21 toolbar commands, 33 menu commands and 29 shortcuts;

## 0.9.1.0 - 2026-08-30

- auto-close parentheses, brackets, braces, quotes and apostrophes while avoiding duplicate closing characters;
- expand Enter between braces and delete empty pairs with one Backspace, preserving a single native Undo step;
- wrap selections with typed pairs and retain the selected inner text;
- add indentation-preserving line comment toggling through Edit, `Ctrl+/` and the main toolbar;
- add a light-themed editor context menu with current Undo, clipboard, Find, Rename and comment states;
- cover the editing gestures with 90 Core tests and dynamic WinForms smoke scenarios;

## 0.9.0.1 - 2026-08-30

- add document tabs backed by independent editor controls and in-memory buffers;
- keep the tab strip in a dedicated layout row so it cannot cover the first source lines;
- preserve unsaved text, caret, selection, scroll and native Undo/Redo while switching files;
- add dirty markers, close-tab actions, Save All and an aggregate exit confirmation;
- keep workspace analysis synchronized with every open in-memory document;
- scope breakpoints to their document, remap them after edits and flag unbound locations;
- add a navigable Breakpoint palette plus Save All and Close Document toolbar commands;
- expand the Core suite to 87 tests and the UI smoke to 28 menu commands and 24 shortcuts;

## 0.9.0 - 2026-08-30

- add line breakpoints toggled from the gutter or with `F9`;
- pause the interpreter before the matching executable statement without blocking the WinForms UI;
- reuse `F5` and the Run toolbar command as Continue while execution is paused;
- keep Stop cooperative and able to release a paused runtime safely;
- mark active breakpoint lines in the non-selectable gutter and navigate to the paused statement;
- add a dedicated breakpoint icon to the toolbar and keep Run synchronized with Continue;
- keep the Edit menu responsive when another Windows process temporarily owns the clipboard;
- make keyboard Cut, Copy and Paste independent from stale Edit-menu enabled states;
- derive self-contained release package names and validation from assembly version metadata;
- expand the Core suite to 85 tests and the menu smoke to 26 commands and 22 shortcuts;

## 0.8.1 - 2026-08-30

- adopt self-contained Windows x64 as the official distribution format;
- add a repeatable release script that publishes and validates the bundled .NET and Windows Desktop hosts;
- generate a ready-to-distribute ZIP and its SHA-256 checksum under `.artifacts\release`;
- publish the same verified package as a GitHub Actions artifact;

## 0.8.0 - 2026-08-30

- convert all application and test projects to concise SDK-style project files;
- verify the intermediate SDK conversion while still targeting .NET Framework 4.8;
- retarget the UI and smoke suite to `net10.0-windows` and the platform-neutral Core/tests to `net10.0`;
- adopt the modern WinForms bootstrap, platform annotations and Segoe UI autoscaling metrics;
- replace legacy `System.Configuration` user settings with a dependency-free UTF-8 local store;
- move local and CI builds to the .NET 10 CLI and pin the supported SDK family with `global.json`;
- preserve all 84 Core tests, 25 menu commands and dynamic WinForms scenarios without warnings;

## 0.7.4 - 2026-08-30

- capture structured DDF call stacks for runtime failures in user and standard functions;
- show runtime diagnostics and call frames together in the Output palette;
- make errors and individual frames navigable to their exact source span by double-click;
- batch final runtime output to avoid partial WinForms updates while preserving colored links;
- add Core and WinForms regressions for nested failures, source highlighting and call-site navigation;
- disable pending symbol tooltips before form disposal to avoid delayed UI callbacks;

## 0.7.3 - 2026-08-29

- regenerate the Release application icon as a newly named multi-resolution resource;
- use a simplified high-contrast navy/orange chevron mark for small shell and title-bar sizes;
- load the window icon directly from the embedded assembly resource instead of shell extraction;
- center Find/Replace, Rename, runtime input and About on the active screen;
- render Workspace/Outline with the same standard tab style used by Diagnostics/Output;
- extend the right palette to the full usable form height and constrain the bottom palette to the remaining width;
- add icon-content, dialog-position and docking-geometry checks to the WinForms smoke;
- reorganize Find/Replace with aligned fields and checkbox plus one uniformly spaced action bar;

## 0.7.2 - 2026-08-29

- introduce one shared light application palette for forms and dynamically created controls;
- keep only the source editor, folded projection and line-number gutter on the code-oriented dark theme;
- convert Workspace, Outline, Diagnostics, Output, completion and status surfaces to light colors;
- restyle About, Find/Replace, Rename and runtime-input dialogs consistently;
- restore high-contrast toolbar and palette-pin glyphs on light backgrounds;
- add a WinForms regression smoke that rejects dark non-editor surfaces and low-contrast command icons;

## 0.7.1 - 2026-08-29

- add a centralized minimal standard library with `print`, `readLine`, `length`, `toInt` and `toFloat`;
- make standard functions available to semantic analysis, type checking and completion;
- connect `readLine` to a native editor input dialog through a testable runtime callback;
- report failed standard conversions as positioned `DDF408` diagnostics;
- add a flat icon toolbar for 13 primary editing, navigation, folding and execution commands;
- keep toolbar Run/Stop state synchronized with the existing menu commands;
- expand the dependency-free core suite to 83 tests and cover toolbar/input in the WinForms smoke;

## 0.7.0 - 2026-08-29

- add the first internal AST interpreter without external compiler dependencies;
- execute functions, lexical scopes, declarations, assignments, control flow and returns;
- support primitive values, arrays, structures, indexes and member access at runtime;
- route `value >> Console` to the new Output palette;
- add Run (`F5`) and Stop (`Shift+F5`) with cooperative cancellation;
- report positioned runtime diagnostics `DDF401`-`DDF407` and cap execution at 100,000 instructions;
- display the value returned by `main` separately from the internal runtime-step counter;
- expand the dependency-free core suite to 81 tests and the menu smoke to 25 commands and 21 shortcuts;

## 0.6.0 - 2026-08-29

- add a runtime-independent type model for primitives, structures and arrays;
- infer expression types and validate declarations, assignments, operators and boolean conditions;
- check function argument counts/types and return values, including signatures from workspace files;
- parse and type-check structure member access through `.` and array element access;
- report stable semantic diagnostics `DDF301`-`DDF308`;
- enrich editor hover with calculated types and merge type errors into source diagnostics;
- align a typed closing brace with its unmatched block indentation while ignoring comments and strings;
- preserve the requested 300-pixel right navigation palette for Workspace and Outline;
- expand the dependency-free core suite to 76 tests;

## 0.5.6 - 2026-08-29

- add a recursively indexed workspace for small folders of `.ddf` sources;
- add File > Open Folder and Close Folder with a compact file explorer beside the Outline;
- open workspace documents by double-click while preserving unsaved-change confirmation;
- offer top-level functions, structures and variables from other workspace documents in completion;
- resolve document diagnostics against the shared index and navigate across files with `F12`;
- keep modified workspace documents indexed from their in-memory contents;
- expand the core suite to 65 tests and the WinForms smoke to 23 commands and 19 shortcuts;

## 0.5.5 - 2026-08-29

- add a document-local semantic model that resolves declarations and references by lexical scope;
- report unresolved names (`DDF201`) and duplicate declarations in the same scope (`DDF202`);
- show symbol kind, signature/type and declaration line while hovering over resolved names;
- add Go to Definition (`F12`) and scope-safe Rename Symbol (`F2`) with one-step Undo;
- keep shadowed and same-named symbols in separate rename sets;
- expand the core suite to 62 tests and the WinForms menu smoke to 21 commands and 18 shortcuts;

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
