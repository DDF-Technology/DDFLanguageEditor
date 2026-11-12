# DDFLanguageEditor 0.12.11 Beta

Questa beta rende visibile nel popup dell'updater la preparazione del download,
i MiB ricevuti e la percentuale di avanzamento, seguiti dalla verifica
dell'archivio. Gli eventi di progresso arrivati in ritardo non sovrascrivono
lo stato di verifica. La release serve anche a provare il percorso completo
di aggiornamento automatico partendo da una copia isolata della `0.12.10`.
La prova UI end-to-end su una copia temporanea isolata `0.12.10 → 0.12.11`
è stata completata il `2026-11-12`: manifesto e ZIP firmati verificati, due
consensi distinti, worker fuori processo, riavvio della nuova versione,
`--verify-installation` riuscito e finalizzazione del backup solo dopo
conferma. L'installazione dell'utente sul Desktop non è stata toccata. Le prove
di disco fisicamente pieno e degli altri guasti ambientali residuali sono
state rinunciate, non superate. Non usare build non firmate per installazioni.

## Release precedente: 0.12.10 Beta

Questa beta corregge la sincronizzazione dell'installazione automatica. Nella
prova UI isolata `0.12.8 → 0.12.9`, il controllo della release, i due consensi,
il download e la verifica del pacchetto firmato sono riusciti, ma l'editor si
è chiuso prima che il worker potesse attestare il processo: il journal è
terminato in `Failed` prima dello swap, lasciando intatta la versione iniziale.

Il worker della `0.12.10` segnala la propria prontezza solo dopo aver verificato
PID e istante d'avvio dell'editor. L'editor aspetta questo segnale prima di
chiudersi; un test di regressione verifica il segnale anche fra due processi
Windows reali. La `0.12.9` già pubblicata non contiene la correzione: per
validare il percorso UI completo occorrerà aggiornare una `0.12.10` installata
verso una release firmata successiva. L'aggiornamento dell'installazione
personale da `0.12.9` a `0.12.10` resta manuale.

Gate locale: build Debug e Release senza warning, 347 test Core, 32 test
contrattuali del compilatore e smoke WinForms completo con 80 comandi menu
superati. Il candidato ZIP `win-x64` self-contained ha superato il self-test
anche dopo estrazione fuori dal repository; i pacchetti SDK Core e WinForms
`0.12.10-beta` sono stati generati. La prerelease GitHub contiene ZIP,
checksum e manifesto `official-2026` verificati; il candidato locale non
firmato non è distribuibile. Nel publish single-file del runner permangono tre avvisi
`IL3000` già noti e separati dalle build della soluzione.

## Release precedente: 0.12.9 Beta

La `v0.12.8` pubblicata non si avvia da una cartella esterna al repository:
il servizio di debug usa ancora un resolver separato che cerca la vecchia DLL
del runner e genera `FileNotFoundException`. Le prove sotto il repository
avevano trovato per errore la DLL di sviluppo nelle directory parent. La
release GitHub è segnalata come non avviabile; non sostituire con essa una
installazione precedente.

La `0.12.9` usa un solo resolver per build e debug e verifica
entrambi i servizi nel self-test dell'EXE distribuito. Lo script di release
estrae ora lo ZIP in una cartella temporanea esterna al repository e ripete
il self-test prima di creare una release. La prova esterna è riuscita:
self-test, avvio GUI, bridge Debug/Release/Run/Publish con il runner estratto,
build Release senza warning, 345 test Core, 32 test del compilatore e smoke
UI updater sono verdi.

La `0.12.8` può essere avviata temporaneamente impostando solo per il suo
processo `DDF_RUNNER_PATH` al file `support/runner/DDFLanguageEditor.Runner.exe`
contenuto nella cartella estratta; la prova locale ha confermato che così
l'app resta avviata. È un workaround, non una correzione della release.

## Release precedente: 0.12.8 Beta — non avviabile fuori dal repository

La release `v0.12.7` non si avvia da un'estrazione pulita: il pacchetto non
contiene `DDFLanguageEditor.Runner.dll`, che il servizio di build cerca già
durante l'avvio. Il titolo della release GitHub è stato contrassegnato come
non avviabile. Non sostituire l'installazione precedente con la `0.12.7`.

Questo hotfix include il runner DDF come EXE separato e self-contained sotto
`support/runner`, senza richiedere un'installazione .NET globale. Il controllo
di pubblicazione richiede il file e avvia l'autoverifica del prodotto, che ora
costruisce anche il servizio di build. Build Release senza avvisi/errori, Core
`345/345`, smoke UI updater, avvio GUI da ZIP estratto e runner senza `dotnet`
nel `PATH` sono stati verificati localmente. Il bridge editor ha inoltre
compilato in Debug e Release, eseguito uno script e pubblicato un EXE con il
runner distribuito.

## Release precedente: 0.12.7 Beta — non avviabile da ZIP pulito

Questa release bootstrap è il primo standalone che include il worker
dell'updater. La `v0.12.6` non può installarla automaticamente: serve un primo
aggiornamento manuale. Un aggiornamento automatico completo potrà essere
provato contro una release firmata successiva, partendo dalla `0.12.7`
installata.

Novità incluse: download e staging di release verificate,
secondo consenso prima dell'installazione, worker self-contained fuori processo,
rollback e ritentativo pre-swap controllato. La finalizzazione di `Completed`
può eliminare il backup precedente solo dopo conferma e verifica dell'intero
target. `Applying`, `Verifying`, `RolledBack` e stati ambigui non vengono
riparati automaticamente dall'editor aperto.

La prova con disco fisicamente pieno e gli altri guasti ambientali residui
sono stati rinunciati, non superati; vedere la roadmap per il rischio residuo.
Build Release, pacchetto, firma ufficiale e staging su copia isolata sono
stati verificati. La prova UI end-to-end dell'installazione automatica resta
aperta fino a una release successiva realmente disponibile.

## Release precedente: 0.12.6 Beta

Windows x64 self-contained beta under the MIT License. This is a real product
release candidate, not a declaration that the DDF language or Editor SDK has
reached 1.0.

Highlights since the last public GitHub release:

- Multi-file workspace editing, source-linked flowchart and a debugger with
  cross-file stepping, breakpoints, Watch, Variables and Call Stack.
- Standalone DDF execution and compilation on .NET 10 with a separately
  embeddable WinForms editor SDK (`0.12.6-beta` package version, contract `0.42`).
- Language package management and a guided Package Studio for syntax profiles,
  with validation and preview before package export.
- Flat, accessible toolbar icons with per-monitor DPI sizing; Package Studio
  layout and navigation improvements.
- Signed GitHub update metadata and a standalone update check. Automatic
  download and installation are not enabled by this release: their separate
  staging, consent, health-check and rollback path still requires end-to-end
  acceptance against this real signed release.
- Extension plug-in protocols and catalogs remain **contracts only**; this
  release does not load arbitrary plug-ins or grant new host capabilities.

The DDF language remains experimental. Native/COM libraries, physical hardware
providers and industrial protocols are not implied by the editor's contract
catalogs. Embedded hosts retain control of scripting capabilities and their
own distribution.

## Earlier release notes

### DDFLanguageEditor 0.11.2

This is an unsigned, experimental Windows beta released under the MIT License.

Version 0.11.2 delivers the first embeddable editor SDK. The repository now
produces separate `DDFLanguageEditor.Core` and `DDFLanguageEditor.WinForms`
libraries, plus a standalone launcher, with local `0.11.2-beta` NuGet packages
and a minimal host sample.

The public host contract reaches version 0.31 and covers documents, analysis,
commands, persistence, projects, build, execution, debugging, host APIs,
permissions, databases, effects and the embedded WinForms lifecycle. The
standalone shell uses the same build, Run and Debug services while retaining
multi-file workspaces, unsaved buffers, breakpoints, Watch, Call Stack,
automatic source navigation and the dynamic flowchart.

The flowchart introduced in 0.11.1 derives control flow from the shared IR,
supports source navigation, zoom, pan, educational annotations, runtime
position and SVG/JPEG export. HostDemo, DatabaseDemo and MinimalHost exercise
the supported integration paths without granting scripts arbitrary reflection
or implicit file-system access.

The Core suite contains 202 passing tests. The WinForms smoke covers all 74
registered menu commands as well as the standalone and embedded lifecycles.

Previous version notes follow.

## 0.11.1

Version 0.11.1 adds the dynamic, source-linked flowchart and its reusable
`FlowchartView`. It visualizes decisions, loops, calls and returns, follows the
debugger, navigates between source and nodes, and supports pointer, pan, zoom
and full-diagram export.

## 0.11.0

This is an unsigned, experimental Windows beta released under the MIT License.

Version 0.11.0 executes every source selected by the project manifest as one
runtime program. The entry `main` can call functions and use structures or
globals declared in other files, including modified unsaved buffers.

Breakpoints, stepping, Variables, Watch, Call Stack and runtime diagnostics now
retain the correct source document and navigate across tabs. Duplicate runtime
names and unsafe forward/cyclic global initialization are rejected before run.
Source membership is controlled by `ddf-project.json`; `@@'...'` remains a
library/capability directive rather than a file include.

The debugger visibly activates the external function's tab on Step Into and
returns to the caller's tab on Step Out. F11, F10 and Shift+F11 are handled
directly so Windows menu activation cannot swallow source-level stepping.

The Core suite contains 174 passing tests. The WinForms smoke includes an
end-to-end cross-file call, visible tab switching, consecutive source steps and
Step Out to `main`.

Previous version notes follow.

## 0.10.1

Version 0.10.1 completes source-level stepping with Step Into (`F11`). When the
paused statement invokes a user function, the debugger stops on its first DDF
statement and refreshes source highlighting, Variables, Watch and Call Stack.
Step Over (`F10`) and Step Out (`Shift+F11`) remain available.

Version 0.10.0 added a Variables palette populated automatically at each
pause. Parameters, locals and globals retain their declared type, while arrays
and structures can be expanded from the immutable runtime snapshot.

A Call Stack palette lists the current frame followed by its callers, including
function, source location and document. Double-click or Enter navigates to the
selected call site, and frames retain source identity for future multi-file debug.
Selecting a frame also switches the Variables palette to that frame's immutable
parameter and local-variable snapshot without mixing caller and callee scopes.

The debugger added Step Over (`F10`) and Step Out (`Shift+F11`).
Implicit workspaces always discover their unique `main`, reject duplicate entry
points and do not stop on breakpoints in code that is not executed. External-file
detection also suppresses false conflicts when disk and buffer differ only in
line-ending representation.

Watch expressions now support side-effect-free arithmetic, comparisons, logic,
parentheses, members and calculated array indices. They use the active language
package's lexer, precedence and runtime-operation bindings while evaluating only
the immutable pause snapshot. Calls, assignments, output and postfix mutations
are rejected. Expression lists persist separately for each workspace folder.

The Core suite contains 173 passing tests. The WinForms smoke covers all 73 menu
commands and 52 toolbar buttons, grouped into seven movable, persistent bars.

Previous version notes follow.

## 0.9.6.7

Version 0.9.6.7 adds a persistent language-package manager. Packages can be
installed or transactionally updated from a directory or `.ddflang`, validated,
selected, or disabled in favor of the embedded DDF fallback.

The isolated language playground previews tokens, the generic syntax tree,
diagnostics and formatting without touching the open document. Every package now
contains `tests.json`; its declarative corpus is executed before activation and a
failure rejects the package atomically with `DDFT001`.

The authoring guide distinguishes safe declarative changes from engine operations
and explicitly authorized native plug-ins. An automated `Nova Demo` changes both a
keyword and the assignment operator, then parses, formats and executes the result
without recompiling the editor.

## 0.9.6.6

Version 0.9.6.6 separates the language package from a workspace-level
`ddf-project.json`. A project pins the exact language package and chooses its
sources, entry point, dependency declarations, backend, output and execution
limit. The schema and a complete sample project are included.

Validate, Build, Run without debugging and Debug now use one build pipeline.
The first replaceable backend, `ddf-vm-ir-v1`, parses, checks and lowers every
selected source to the common IR. Build writes a deterministic JSON receipt
with source hashes and a configuration fingerprint; Validate writes nothing.
Named unsaved buffers may participate without being saved implicitly.

Build diagnostics retain document paths and source spans, so Output rows can
open the correct file. The Core suite contains 168 passing tests and the dynamic
WinForms smoke covers all 60 menu commands plus project validation/build and
unsaved-buffer preservation.

Runtime calls across source files are intentionally deferred to 0.11.0; this
release executes the entry-source IR while validating cross-file signatures.

Version 0.9.6.5 introduces a syntax-independent IR virtual machine. Concrete
operators are translated to stable, closed operation identifiers during
lowering, so execution no longer compares DDF spellings such as `<<`, `||` or
`^/`. Values, scopes, calls, control flow, arrays and structures remain bounded
to engine-supported operations.

`runtime.json` now declares the instruction set, entry point, default execution
limit, output target and expression bindings. `standard-library.json` declares
function names, signatures, documentation and safe host bindings. Packages may
rename syntax and standard functions without recompiling the engine, but cannot
request reflection, scripts or arbitrary host methods.

Cancellation, source positions, runtime diagnostics, call stacks and breakpoints
are preserved. Each pause already receives an immutable runtime-state snapshot
for the variable-inspection UI planned in 0.10.0. The Core suite contains 164
tests, including package-only operator and standard-library substitutions.

Previous version notes follow.

Version 0.9.6.4 makes primitive types, conversions, compatibility, mutability,
scope and signature policies declarative through a validated `types.json`.
Grammar productions are connected to the closed and versioned
`ddf-safe-semantics` operation catalog without allowing package-provided host
code.

A public source-positioned IR now separates execution from concrete syntax.
Run and debugger consume `DdfIr*` nodes; the typed DDF AST remains only as a
temporary lowering input. Diagnostics explicitly distinguish source errors,
language-definition errors, unsupported engine capabilities and runtime
failures.

Language manifests can declare inert native-extension requirements using exact
ID, publisher, version, SHA-256 and requested capabilities. Authorization is
separate, capability-specific and deny-by-default; this release does not load
native binaries. The language-package information dialog exposes the declared
requirements.

Editor viewports are now stable across formatting, normal and format-on-save
workflows and tab switches. Horizontal and vertical positions are persisted per
document and restored with the project session.

The automated Core suite contains 161 passing tests. The dynamic WinForms smoke
also covers viewport restoration, long documents, performance budgets and all
57 menu commands.

Previous version notes follow.

Version 0.9.6.3 derives Outline, folding, breadcrumb, syntax selection,
completion, Signature Help, hover, snippets, formatting, indentation, automatic
pairs and lexical Quick Fixes from language-package metadata. The typed DDF AST
remains only as a compatibility projection for semantic and runtime services.

The active language can now be changed at runtime from a package directory or
`.ddflang` archive through Help or the icon bar. The selection persists between
launches and updates editor services, workspace extensions, file dialogs,
Settings and status without restarting. Invalid packages are rejected
atomically and leave the previous language active.

Every successful activation advances a catalog revision. Analysis, completion
and workspace results carry the revision that created them and cannot update the
UI after a newer package becomes active. Dynamic smoke coverage alternates six
realistic packages containing 256 additional keywords and 96 snippets while a
2,000-line document and a 40-file workspace are active; the measured package
switch p95 remains guarded by its dedicated performance budget.

Previous version notes follow.

Version 0.9.6.2 makes `grammar.ebnf` the canonical syntax definition of the DDF
package. `grammar.json` contains compilation settings and typed-by-kind
node/field metadata. Package activation compiles and validates EBNF references,
start rules, deterministic alternatives and unsupported left recursion. Parsing
also produces a generic syntax tree with exact source spans and recoverable
missing syntax. Keyword roles are extensible string identifiers and the default
DDF package includes `else` throughout parsing, IDE services and runtime.

Previous version notes follow.

Version 0.9.6.1 makes the lexical layer genuinely declarative. Unicode character
classes and explicit characters now define whitespace, identifier starts and
parts, and numeric digits. The package also controls decimal separators,
strings, escapes, line and block comments, library directives, multiline
behavior, unterminated-token diagnostics and longest-match ordering.

The loaded definition is shared by the lexer, syntax classifier, completion
prefix calculation and rename validation. Library completion no longer assumes
the DDF-specific `@@'...` form. Exact source spans and the incremental lexer are
preserved while editing incomplete code.

Package loading rejects missing rules, duplicate or unreachable markers,
incompatible character classes and lexical categories that could never be
recognized. `language-lexicon.schema.json` documents the public JSON contract.
Regression tests load an external package using `$` identifiers, comma decimals,
`#`/`##` comments, backtick strings with `~` escapes and `use[...]` directives,
then verify tokenization, classification, completion and incremental equivalence
without recompiling the editor.

Previous version notes follow.

Version 0.9.6.0 introduces the first stable boundary between the editor and a
language definition. DDF now ships as a versioned package made of a manifest and
separate JSON documents for lexicon, grammar, types, semantics, formatting,
IntelliSense, runtime, build and standard library. The same package can be read
from a directory or from a ZIP-compatible `.ddflang` archive.

The current DDF lexicon is loaded from this external package and continues to
drive the existing lexer, parser and formatter without observable grammar
changes. The remaining sections select explicit, versioned adapters to the
current engines; their fully declarative replacements are staged in the next
0.9.6 steps. Package data cannot name or execute arbitrary host code.

Loading is atomic. Unsupported versions or adapters, missing or unsafe section
references, unknown sections and duplicate definitions reject the whole package
with diagnostics containing a code, file and JSON path. A complete copy embedded
in Core is then used as a safe fallback. The status bar and its tooltip expose
the active language and package versions, source, origin and fallback details.
The same information is available without relying on a tooltip through
`Help > Pacchetto linguaggio attivo`, including an explicit successful-load or
embedded-fallback state and copyable diagnostics.

The Core suite covers the embedded resource, folder and `.ddflang` forms,
behavioral equivalence, invalid definitions and fallback. The package contract
is documented in `LANGUAGE_PACKAGE_FORMAT.md`.

Previous milestone notes follow.

Version 0.9.5.6 closes the 0.9.5 milestone with an accelerated endurance gate.
The dynamic WinForms suite now runs 160 mixed cycles across four open documents,
alternating edits, Undo/Redo, cancellable analysis, overview updates, keyboard
navigation, visual settings, formatting, folding and Command Palette lifetimes.
It verifies UI heartbeats and every buffer after the run, and rejects excessive
growth in managed memory, process handles, GDI objects or USER objects.

The reference run completed with a 38.9 ms iteration p95, 23,504 bytes of
managed growth, three additional process handles, three GDI objects and eight
USER objects. The existing interactive and realistic-workspace performance
budgets remain active alongside the endurance gate.

Version 0.9.5.5 completes the first accessibility and multi-monitor pass.
`F6` moves focus from the editor to Outline, then Diagnostics and back to the
editor; `Shift+F6` follows the reverse path. An auto-hidden destination opens
for keyboard use without silently changing its persisted pin state.
The shortcut is handled across native editor, dialog-key and form key-preview
paths. It selects the requested Outline or Diagnostics tab and prevents the
auto-hide timer from closing a palette while keyboard focus remains inside it.
Returning to the editor now targets the folded projection when folding is active,
rather than attempting to focus the hidden source control.
Within the focused palette, `Ctrl+Tab` and `Ctrl+Shift+Tab` cycle its tabs;
`Ctrl+PageDown` and `Ctrl+PageUp` provide the equivalent IDE-style commands.
The primary control of the newly selected tab receives focus immediately.

Focusable controls and toolbar commands now expose stable accessible names.
Application chrome honors Windows high-contrast colors and its normal theme is
checked against a minimum 4.5:1 text contrast. Every form uses per-monitor DPI
autoscaling. Persisted palette dimensions are stored as logical pixels and
converted for the active monitor, so moving between 100% and 150% scaling does
not progressively alter the saved layout.

Version 0.9.5.4 adds a narrow, interactive overview at the right edge of the
editor. It maps errors, warnings, workspace-search matches and breakpoints over
the complete document, while an outlined band shows the currently visible
viewport. Clicking the overview navigates directly to the corresponding line.
The feature can be disabled from View, Settings, the Command Palette or its icon
bar button, and that preference persists between launches.

The overview reuses existing language-service results and never modifies source,
RTF colors, selection or Undo history. Marker snapshots are deduplicated and a
scroll updates only the viewport band. The measured scroll/gutter p95 remains
well below the 16 ms target. A full text-rendering minimap was evaluated and is
intentionally deferred: the overview delivers the primary navigation value at a
much lower rendering and maintenance cost.

The overview uses a 24-pixel high-contrast track. Diagnostic and breakpoint
markers are painted above the viewport band, and a cyan pointer always shows the
current caret line, so a clean document no longer looks like an empty control.
Visible caret and diagnostic markers use the native editor's actual pixel
coordinates; proportional whole-document placement is reserved for lines that
are outside the current viewport.
Caret movement now updates immediately without waiting for typing or analysis.
When a document is longer than the viewport, every marker uses the same ordered
whole-document scale; diagnostic ticks are two pixels thick to keep adjacent
problems distinguishable.
Visible problems occupy the main lane at the exact RichEdit row coordinate;
off-screen problems use a separate two-pixel edge lane at their proportional
whole-document position, avoiding overlaps between the two coordinate systems.
Diagnostic overview positions now originate from the same character offsets as
the native wave underline. Stale analysis snapshots are not reprojected while
typing, and editor coordinates are transformed through screen space to account
for control origins and DPI scaling without accumulating a vertical offset.
Caret and diagnostic ticks now use the same clamped two-pixel rectangle instead
of mixing a GDI+ line with a filled marker, eliminating the final rasterization
offset when both occupy the same editor row.

Version 0.9.5.3 adds four persistent visual aids: current-line highlighting,
space/tab/end-of-line markers, indentation guides and optional word wrap. They
can be changed from View, Settings, the Command Palette or dedicated toggle
buttons on the icon bar. Decorations are painted over the native editor without
changing source text, RTF formatting, selection or Undo history. With wrapping
enabled, the gutter keeps logical source numbers on the first visual row only.

Overlay refreshes are deferred away from synchronous selection and scroll
operations, preserving the established input, selection and scroll targets. The expanded
dynamic smoke also exposed and fixed a teardown race in which the recovery timer
could inspect an already disposed document tab. The roadmap now records the
0.9.6 transition toward versioned language packages, declarative grammar, safe
semantic operations, a generic VM and configurable builds.

Version 0.9.5.2 moves folder indexing off the WinForms input thread. Workspace
loads are cancellable and versioned, so opening or closing another folder
supersedes unfinished work and an obsolete result can never replace the current
workspace. Open buffers are reconciled again when the result reaches the UI,
preserving edits made while indexing was running. Typing in a workspace document
now updates a lightweight source snapshot immediately and leaves parsing and
symbol rebuilding to the existing cancellable background analysis.

The performance contract now includes a deterministic 40-file workspace and a
2,000-line active document. Dynamic smoke tests verify UI heartbeat during a
blocked load, cancellation of the older request, rejection of stale analysis,
bounded dispatch time and responsive editing under this realistic workload.

Version 0.9.5.1 removes avoidable redraw work from the main editing loop. The
line-number gutter now changes atomically only when its visible content or source
mapping actually changes. Outline and Diagnostics reuse identical analysis
snapshots instead of rebuilding their trees and lists. Syntax formatting keeps
the original native RichEdit viewport while token selections are applied under
one redraw transaction. Dynamic smoke tests count renders and invalidations and
verify that repeated analysis neither moves the viewport nor touches unchanged
controls.

Version 0.9.5.0 starts the fluidity, accessibility and refinement milestone with
a measurable responsiveness contract. Dynamic WinForms smoke tests now measure
p95 latency for input, selection, scroll/gutter updates, full highlighting and
Command Palette opening against a deterministic 500-line DDF document. Tight
product targets are reported separately from more tolerant automated regression
ceilings, so loaded development machines remain usable without hiding material
slowdowns. The workloads and interpretation rules are documented in
`EDITOR_PERFORMANCE_BUDGETS.md`.

Version 0.9.4.6 completes the personalization, session and recovery milestone.
Each named tab now tracks its last known disk content and detects edits,
replacement or deletion performed by another program. A centered side-by-side
comparison offers Reload from disk, Keep buffer or Later. Postponed conflicts do
not repeatedly interrupt editing, but are checked again before manual or
automatic saves so no external version is silently overwritten. Reload remaps
breakpoints; Keep preserves the editor content as an explicitly dirty buffer.

Version 0.9.4.5 adds local recovery after an unexpected termination. While
documents are dirty, an enabled-by-default journal is refreshed every five
seconds. A leftover journal opens a centered dialog on the next launch, listing
the recoverable documents and allowing Restore, Discard or Later. Restore opens
the recovered source as modified buffers and never writes to the original files.
The journal is removed after a regular close and can be disabled independently.

Version 0.9.4.4 restores the previous coding session by default. The active
workspace, ordered tabs, active document, selections and per-document
breakpoints are persisted at close. Clean named documents are reloaded from
disk, while Hot Exit overlays only dirty content onto its exact tab, including
when several untitled documents are open. Session restoration can be disabled
from Settings without disabling autosave or Hot Exit.

Version 0.9.4.3 adds two opt-in safeguards to Settings. Autosave writes modified
documents that already have a path after a configurable idle delay. Hot Exit
stores dirty named and untitled buffers when the application closes, then
restores their content, selection, dirty state and breakpoints on the next
launch. Both features are off by default; a failed Hot Exit write falls back to
the normal save confirmation instead of discarding changes.

Version 0.9.4.2 adds a Shortcuts tab to Settings. Every current menu command can
receive a persistent Ctrl/Alt combination or function key, or be left
unassigned. Conflicts are rejected with the name of the existing command, while
Predefiniti restores the tested preset. Menu labels, Command Palette entries,
toolbar tooltips and editor-specific handlers update together after Apply.

Version 0.9.4.1 makes the IDE layout persistent too. The preferred width of the
right Outline/workspace palette, the height of the bottom Diagnostics/Output
palette and both pinned/auto-hide states are restored on the next launch. A
smaller temporary window does not overwrite the user's preferred dimensions.

Version 0.9.4.0 introduces persistent editor preferences. `Ctrl+,`, the View
menu, Command Palette and toolbar open a centered Settings window for font,
zoom, dark/light code surface, tabs or spaces, indentation width, LF/CRLF and
optional format-on-save. Changes apply immediately to every open document and
can be reset to tested defaults in one step.

Version 0.9.3.7 completes the workspace navigation milestone. One or more
`.ddf` files can be dropped onto the editor and open in independent tabs; a
dropped folder becomes the current workspace. Recently opened workspace folders
are persisted in a dedicated File submenu, separately from recent documents.

Version 0.9.3.6 adds a searchable Command Palette through `Ctrl+Shift+P`, the
View menu and the toolbar. It derives actions from the current menus and shows
their category, shortcut and live availability. Filtering is immediate and
execution reuses each original menu handler, keeping future commands easy to add.

Version 0.9.3.5 adds a compact contextual breadcrumb between document tabs and
the editor. It follows the caret through the current file, structure, function
and nested `if`, `while`, `do…while` and `for` blocks. Segments are clickable,
work with recoverable incomplete syntax and feed the existing Back/Forward
history without changing source text or Undo.

Version 0.9.3.4 adds browser-style navigation history. `Alt+Left` and
`Alt+Right` restore document, position and selection across workspace jumps
without touching text Undo/Redo or unsaved buffers. Definitions, references,
search results, quick navigation, Outline, diagnostics, breakpoints and runtime
targets share the same bounded history. Invalid targets are skipped safely and
a new jump after Back clears the Forward branch.

Version 0.9.3.3 adds one reusable navigation palette for files (`Ctrl+P`),
symbols (`Ctrl+Shift+O`), semantic references (`Shift+F12`) and line/column
positions (`Ctrl+G`). `Ctrl+Shift+Backspace` returns to the last edit in the
active document. Workspace references exclude comments, strings and shadowing
locals, while navigation preserves unsaved buffers and Undo history.
Standard editing shortcuts now follow the focused text box throughout the main
window and every dialog. Search, replacement, navigation, rename and runtime
input fields support cut, copy, paste, undo and select all; read-only text keeps
copy/select operations, and each field exposes a consistent context menu.

Version 0.9.3.2 adds selective workspace replacement. `Ctrl+Alt+H`, the Edit
menu and the toolbar open a mandatory preview where individual text matches can
be included or excluded. Before applying anything, every source snapshot is
validated again; one conflict cancels the complete operation. Changed files are
opened as dirty buffers and are never saved automatically. Each modified
document is restored by one Undo action.
The editor repaints complete replacement buffers from their first character, so
an initial comment cannot incorrectly extend its green formatting into the code.

Version 0.9.3.1 adds a horizontal splitter above the bottom palette. Dragging it
resizes Diagnostics, Output and Search vertically while preserving a usable
editor area. The chosen height survives pin and auto-hide transitions; the
splitter disappears in compact mode and becomes draggable again only when the
palette is pinned.

Version 0.9.3.0 introduces workspace-wide text and declaration search. The new
Search palette covers every indexed `.ddf` file and every open document,
including untitled buffers. Unsaved buffer content takes precedence over the
copy indexed from disk. Results show file, line, column, kind and source preview
and navigate on double-click or `Enter`.

Search runs in the background with cooperative cancellation and versioned
snapshots. Editing a document refreshes an active query, while superseded
results are rejected. Text/symbol mode, case sensitivity, `Ctrl+Alt+F`, the
Edit menu and a new toolbar icon expose the feature without changing the
document or its Undo history.

Version 0.9.2.10 moves document analysis and completion away from the UI thread.
Requests are versioned and superseded work is cancelled; a result is marshalled
back to WinForms only when both its source and request still match the active
editor. Explicitly dismissed completion results cannot reappear later. Dynamic
smoke coverage now exercises rapid transient edits and deliberately stale
completion requests.

Brace quick fixes remain intentionally conservative: when removing a brace
leaves a different but grammatically valid single-statement construct, the
parser has no certain diagnostic or original intent to reconstruct. That case
is now documented as a manual-review limitation instead of receiving a
speculative edit.

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
