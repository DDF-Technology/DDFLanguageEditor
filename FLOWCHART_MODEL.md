# DDF flowchart model

The `DdfFlowchartBuilder` in `DDFLanguageEditor.Core` converts one selected
function from the common IR into a renderer-independent control-flow graph.
Source code remains authoritative: the graph never rewrites the document.
The flowchart is intentionally read-only; diagram-to-code editing is outside the
product roadmap. Node selection can navigate to source, but it cannot mutate it.

## Model

- `DdfFlowchart` identifies the document, function, entry and exit nodes.
- `DdfFlowchartNode` stores a stable build-local ID, semantic kind, compact
  label, source interval and any referenced function names.
- `DdfFlowchartEdge` stores the control role (`Next`, `True`, `False`,
  `Break`, `Continue`, `Return` or `Terminate`) independently from `IsBackEdge`.
- `DdfFlowchartBuildResult` returns either a graph or diagnostics such as
  `DDFC001` when the requested function is absent.

Calls are annotations on their containing node, not control-flow edges. This
keeps the function control-flow graph separate from the future call graph and
data-flow views.

## Covered IR

The first increment represents entry, exit, sequential declarations and
expressions, decisions, `while`, `do while`, `for`, calls, returns, breaks,
continues and explicit termination. A continue edge returns to the loop test,
or to the increment node of a counted `for`. Labels are slices of the source, while topology and
semantic roles come exclusively from IR node types.

Control-transfer targets follow lexical loop ownership rather than visual
proximity. `continue` reaches the condition of `while`/`do while` and the
increment step of `for`; `brk` reaches the continuation immediately after the
innermost loop even when it appears inside `if` or `select`; `ret` and `end`
reach the function exit with distinct edge roles. Structured recovery, typed
iteration, lexical resource cleanup and cooperative suspension will receive
dedicated topology only when their language verticals are active; the current
builder does not invent placeholder semantics for them.

`DdfFlowchartSnapshotService` produces immutable, source-versioned snapshots in
the background. It debounces rapid edits, cancels the previous request and checks
its monotonically increasing request ID before publishing a result. Consumers can
therefore retain their last valid view while a newer document is being analyzed,
without allowing an obsolete graph to replace it.

Each snapshot exposes an explicit `Current`, `Partial` or `Unavailable` state.
Parser and IR diagnostics travel with recoverable IR: when a function can still
be lowered, missing expressions become source-linked partial nodes and the palette
shows an amber warning while keeping the graph navigable. If no function can be
represented, the last diagram remains visible but is marked as stale. Unsupported
IR constructs (`DDFIR001`) are counted as omitted constructs and their diagnostic
details are exposed by the palette status. A later valid snapshot clears the notice
automatically.

`DdfFlowchartLayouter` now assigns deterministic ranks and non-overlapping node
positions while ignoring explicit loop-back edges for rank propagation. The first
WinForms palette renders semantic shapes and branch labels, follows the function
at the caret, highlights the corresponding node and navigates to source on double
click. Invalid transient input retains the preceding valid diagram with an
explicit stale status. Palette visibility is stored with the existing palette
layout preferences.

Applying a completed snapshot preserves the current zoom and anchors the selected
source node to the same client position even when preceding statements add or
remove layout ranks. Equivalent semantic nodes are matched by kind and label when
their source span changes; if no anchor survives, the relative viewport center is
retained. Switching function instead centers the newly active node.

Semantic lanes keep the main continuation centered, place pre-test loop bodies
on a side lane and split `if`/`else` destinations around their decision. The
palette toolbar provides mutually exclusive source-selection pointer and
hand-pan modes, zoom controls and full-diagram SVG or JPEG export rather than
exporting only the visible viewport. SVG is the default documentation format and
is emitted as native vector shapes, text and routed paths without embedded raster
content. With pan enabled, dragging moves the viewport without
activating nodes or changing the source selection.
The `ƒ` menu lists calls in the displayed function and resolves their declaration
either in the active document or in the workspace index. Double-clicking a call
node performs the same navigation and switches document when necessary.
The same menu exposes Back and Forward commands backed by a dedicated call
navigation history. Moving the caret into another function in the destination
document does not replace the recorded caller, so Back still restores its exact
document and source position. Dynamic items explicitly inherit the application
menu font and light/high-contrast colors.

`DdfFlowchartProjection` can collapse maximal, unbranched sequences of process,
call and partial nodes into source-linked group nodes. The projection rewires the
outer edges, retains aggregated calls and never mutates the authoritative graph.
The `▤` toolbar command toggles the projection; double-clicking a group expands
that sequence while leaving the other groups collapsed.

Code and diagram selection are synchronized without editing the document. Moving
the caret selects the smallest containing node, or the nearest source-linked node
when the caret is on intervening whitespace, and scrolls the canvas only if that
node is outside the viewport. A pointer click selects the exact source interval;
arrow keys move spatially between nodes and Enter or Space activates the selected
node. Collapsed groups and partial nodes preserve this mapping. Activation is
disabled for stale diagrams because their source intervals no longer identify the
current buffer; none of these operations changes text, dirty state or Undo.

Debugger highlighting is derived from the immutable `DdfIrModule` collection in
the build artifact that started the session. At every pause the module and
containing function are resolved from the pause document path and source span,
then the corresponding runtime node is marked independently from the ordinary
selection. Breakpoints and Step Into, Over and Out therefore switch diagram,
function and document across workspace files without consulting a newer editor
analysis. The active node uses a dedicated execution badge and the marker is
cleared before resuming, stopping or completing execution.

The executable gate `--flowchart-runtime-sync-only` treats the debug snapshot as
the single correlation point for the active document, selected source span,
yellow editor line, function diagram and execution node. It covers local and
cross-file Step Into, Over and Out, cooperative capability suspension and resume,
normal completion, propagated runtime failure and execution-wide resource cleanup.
While a capability is suspended the node remains on the calling statement and the
palette reports `DEBUG IN ATTESA`; after resume it advances to the next DDF
statement. Completion, cancellation and fault clear both editor and diagram
markers. Structured `attempt/recover/always` and lexical `use` will join this gate
only when their dedicated IR nodes become active.

`FlowchartView` is the reusable WinForms presentation boundary. It owns the
canvas, local toolbar, status row, pointer/pan exclusivity, grouping switch, zoom
commands and icon resources. Hosts provide analysis and navigation through its
public diagram/source/debug methods and activation events, and retain control of
external concerns such as choosing an export destination. The standalone IDE now
hosts this same control inside its optional palette; a smoke test also constructs
and drives it without a `MainForm`. Packaging it with the complete editor control
remains part of the `0.11.2` SDK milestone.
Decision labels use distinct anchors: ordinary true/false branches leave opposite
sides of the diamond, while a loop body leaves laterally and its exit continues
downward. Opaque, color-coded label badges keep the text readable over connectors.
Every connector segment is horizontal or vertical, including the final approach
to a node and loop-back routes.
All connectors first leave the bottom of their source node vertically. Sibling
branches share a departure junction when the control flow permits it; their
labels remain distinct without using diagonal or side exits.
Routing is adaptive: aligned nodes use one vertical segment, adjacent lanes use a
single lateral detour, and long edges select the nearest obstacle-free channel
between local lanes instead of travelling to the outer diagram boundary.
Edges converging on the same node use a common approach junction, making compatible
elbows and the final trunk coincide. Back edges use offset local channels and join
an existing solid trunk without painting dashes over it; rendering also clips any
residual collinear overlap. Edge labels are composed after every connector and
node so their opaque badges always remain foreground.
Routes reserve their segments progressively. Later connections reject candidate
channels that would cross an existing horizontal or vertical segment, while shared
endpoints and intentional merge junctions remain valid. Outer channels are kept as
a fallback only when no nearer intersection-free local channel exists.
Whenever an incoming route reaches a line already shared by edges with the same
target, a compact arrow is drawn immediately before that merge vertex. The marker
inherits the solid or loop-back color and indicates flow direction without covering
the junction, node, or branch label.

The repeatable WinForms gate `--flowchart-geometry-only` exercises both a compact
combination of decisions and loops and the large
`samples/flowchart-complex-smoke-test.ddf` graph. It requires every semantic edge
to have a route; every route to remain orthogonal, simplified, vertically departed
and inside the diagram; solid and dashed paths never to overlap or cross; compatible
merges to share an elbow; both route kinds to receive pre-merge arrows; and two
successive layouts of the same input to produce identical coordinates and label
anchors. This test is the release gate for current constructs. Future structured
recovery, typed iteration and lexical cleanup must extend the matrix when their IR
nodes become active rather than weakening these invariants.

The multi-file sample in `samples/flowchart-workspace` exercises navigation,
grouping, control-flow forms, recursion, arrays, structures, runtime and debug.
The reusable `FlowchartView` now exposes an independently switchable educational
mode and a change event. The standalone shell mirrors it in the View menu and
persists the preference per user. Switching it changes only educational
presentation: source, selection, Undo, debug state and build permissions remain
untouched, while the ordinary flowchart and debugger stay available.
When enabled, a dedicated panel explains the selected semantic node in plain
language. Selected decision branches receive stronger green/red emphasis. During
a debugger pause the panel identifies the current step and lists the immutable
runtime values whose identifiers occur in that node; disabling the mode collapses
the panel and stops these presentation updates without disabling the diagram.
