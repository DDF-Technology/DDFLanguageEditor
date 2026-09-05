# Editor performance budgets

This document defines the measurable responsiveness contract introduced in
`0.9.5.0`. It is a regression baseline, not a claim that every target is already
met on every computer.

## Measurement rules

- Use a warm application process and a visible WinForms handle.
- Use the generated 500-line DDF document from the dynamic editor smoke test.
- Measure elapsed wall-clock time with `Stopwatch` on the UI thread.
- Record 30 samples for interactive operations and evaluate the 95th percentile.
- Measure a complete synchronous analysis/color pass and a real first popup
  show/layout cycle, not only object construction.
- Run Release builds on Windows with no debugger attached when establishing a
  comparison baseline.

Two limits are recorded for every scenario. The **target p95** is the product
goal perceived by the user. The larger **smoke ceiling p95** is a deterministic
regression guard that tolerates loaded developer and CI machines. Exceeding the
ceiling fails the dynamic smoke suite; exceeding only the target is reported in
the measurement output and becomes optimization work for the following steps.

| Scenario | Reference workload | Target p95 | Smoke ceiling p95 |
| --- | --- | ---: | ---: |
| Input editor | 30 consecutive insertions in 500 lines | 16 ms | 120 ms |
| Selection/caret | 30 selections distributed through 500 lines | 16 ms | 120 ms |
| Scroll and gutter | 30 viewport/gutter updates through 500 lines | 16 ms | 180 ms |
| Full highlighting | lexer, parser, semantics, types, outline, diagnostics and formatting for 500 lines | 250 ms | 5000 ms |
| Popup opening | create, lay out and first-show the complete Command Palette | 100 ms | 1000 ms |
| Large-document analysis dispatch | schedule cancellable analysis for 2,000 DDF lines | 16 ms | 120 ms |
| Workspace-load dispatch | start or supersede indexing of 40 files × 100 lines | 50 ms | 250 ms |
| Large workspace input | 20 insertions in 2,000 lines with 40 indexed files | 16 ms | 120 ms |
| Language-package switch | 6 alternating activations with 256 keywords, 96 snippets, 2,000 active lines and 40 indexed files | 500 ms | 5000 ms |

## Language-package generation gate

From `0.9.6.3`, every successful package activation advances an atomic catalog
revision. Background analysis, completion and workspace indexing capture both
the language definition and that revision before dispatch. Their result can be
applied only while the revision is still current, independently of ordinary
document/request versioning.

The package-switch smoke workload alternates two independently loaded packages
six times while an analysis and workspace indexing are active. It then verifies
that diagnostics/outline, completion and workspace all expose the final package
revision and that the final package-specific completion item is available.

## Endurance gate

The `0.9.5.6` gate runs 160 deterministic mixed-command cycles across four open
documents. It alternates editing, Undo/Redo, background analysis, navigation,
visual settings, formatting, folding and popup creation while checking an UI
heartbeat and exact buffer integrity.

The complete iteration p95 must stay at or below 750 ms. After forced garbage
collection, retained managed growth must not exceed 64 MiB, process-handle
growth must not exceed 24, and GDI or USER object growth must not exceed 16.
These deliberately tolerant ceilings detect accumulation and deadlock regressions;
they are not replacements for the stricter interactive budgets above.

## Interpretation

The 16 ms interactive target corresponds to one refresh interval at 60 Hz. A
single slower sample is tolerated; p95 exposes sustained latency without making
the result depend on the worst scheduler interruption. Full highlighting has a
separate budget because analysis can run in the background after typing, while
the UI application phase still has to remain bounded.

Every later `0.9.5` optimization must run the same scenarios and update these
numbers only when the workload or product expectation intentionally changes.
Loosening a ceiling to hide a regression is not an acceptable fix.
