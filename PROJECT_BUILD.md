# DDF project and build contract

From version 0.9.6.7, the language package and the user project have separate
responsibilities:

- the language package defines syntax, semantics, runtime operations, standard
  library and the build backends it supports;
- `ddf-project.json` selects source files, the exact language-package version,
  entry source and function, dependencies, backend, output and build options.

The project manifest is validated by `ddf-project.schema.json`. A minimal
manifest is:

```json
{
  "schemaVersion": "1.2",
  "name": "example",
  "module": {
    "id": "example.application",
    "version": "1.0.0",
    "exports": [ "main" ]
  },
  "languageId": "ddf",
  "languagePackageVersion": "0.12.3",
  "sources": [ "**/*.ddf" ],
  "entrySource": "main.ddf",
  "entryPoint": "main",
  "dependencies": [],
  "references": [],
  "backend": "ddf-vm-ir-v1",
  "output": {
    "directory": ".ddf-build",
    "artifact": "example.ddfbuild.json"
  },
  "options": {
    "useUnsavedBuffers": true,
    "maxInstructions": 100000
  }
}
```

Manifest version `1.2` adds the required `references` array. Entries describe
only logical, exact-version dependencies of kind `managed-dotnet`, `com`,
`native` or `host-service`, plus architecture, origin and required
capabilities. They never contain loadable paths, ProgIDs or native symbols.
Until an authorized resolver is supplied, a non-empty array is rejected with
`DDFB018`; see [DDF_EXTERNAL_REFERENCES.md](DDF_EXTERNAL_REFERENCES.md).

The first resolver contract is now available. Applications provide an exact
catalog plus an authorization callback; no global machine search occurs.
Standalone may resolve a verified managed assembly with size and SHA-256,
whereas embedded receives only a typed adapter identity and never hands an
assembly to the compiler. Adapter API metadata remains a separate gate.

Successful builds atomically create
`<output.directory>/ddf-references.lock.json`. The `1.0` receipt records the
owner module, context, exact binding, authorized local path, size, SHA-256 and
catalog-supplied transitive dependencies. Existing locks are verified and
never silently replaced (`DDFR010`/`DDFR011`). The lock schema is distributed
as `ddf-references-lock.schema.json`.

## Commands

- **Convalida progetto** parses, checks and lowers every selected source without
  writing files.
- **Esegui build** runs the same Core preparation, writes its deterministic
  receipt and then emits verified Roslyn development artifacts in the selected
  Debug/Release directory.
- **Compila ed esegui** performs that build and immediately runs the exact
  verified artifact in the isolated worker.
- **Esegui ultima build** runs the last valid artifact of the current standalone
  session in the isolated worker without rebuilding it.
- **Pubblica EXE...** performs a fresh build and creates a new self-contained
  package directory containing the executable, runtime, verified manifest,
  licenses and notices.
- **Avvia senza debug** executes the entry program from that in-memory artifact.
- **Avvia debug** (`F5`) executes the same artifact with breakpoint support.

All sources selected by `sources` form one module and one runtime program.
Functions, structures and globals are visible across those files in a flat,
case-sensitive namespace; file and folder names never create namespaces. Open
unsaved buffers participate when `useUnsavedBuffers` is enabled. `exports`
declares the future public API and every listed name must exist. `@@'...'` is a
deprecated library/capability hint; it never includes another source or module.

Errors appear in Output with their document, line and column; double-clicking a
row opens the correct file and selects its source span. When
`useUnsavedBuffers` is true, open named buffers take precedence over disk and
are marked in the receipt. Sources are never saved implicitly.

Without `ddf-project.json`, the editor creates an implicit in-memory project for
the open folder or current file. In a folder it discovers the source containing
the single `main` entry point independently from the active tab. Zero or multiple
`main` definitions stop validation with a configuration diagnostic. A completely
untitled document can be validated, run and debug, but must be saved before
**Compilazione > Esegui build** may write an artifact. In the standalone
application, modified named buffers are saved first; cancelling any save cancels
the persistent build.

## Current boundary

From 0.11.0, `ddf-vm-ir-v1` validates and executes all selected files as one
project-wide program. Breakpoints, stepping, runtime diagnostics and stack
frames retain their source document. Duplicate runtime declarations produce
`DDFB010`. Global initialization follows deterministic source/declaration order;
forward or cyclic global dependencies produce `DDFB011`.

Module identity, version and sorted exports are pinned reproducibly in the
receipt and fingerprint. Exact dependencies are resolved recursively from
`.ddf/modules/<id>/<version>`; only direct exports are visible, dependencies are
initialized before consumers, and missing modules, version conflicts and cycles
stop the build. The store is excluded from application source globbing and does
not define namespaces. Resolved modules and their source hashes are recorded in
the artifact. `IDdfModuleResolver` allows a host to substitute the storage
lookup without changing language semantics.
Runtime functions, structures and globals are internally scoped by exact module
identity and version, so separate dependencies may reuse private names. Source
code still uses unqualified names: lookup starts in the current module and then
considers only direct exported dependencies; ambiguous public APIs fail type
checking.
See [the module-system specification](DDF_MODULE_SYSTEM_SPEC.md) for visibility,
initialization, dependency and directive rules.

Each resolved module now also produces one immutable, backend-neutral
`DdfCompilationUnit`. Exact source snapshots, structural IR, bound IR `2.0` and the exported
interface are separate layers and are not serialized into the current build
receipt. See [the compiled-unit contract](DDF_COMPILATION_UNIT_CONTRACT.md).

Source modules can be packed as deterministic `.ddfmod` ZIP archives and
installed with `DdfModulePackageStore`. The manager stages and validates the
payload before moving it to `.ddf/modules/<id>/<version>`, rejects implicit
replacement of the same version, and supports exact listing/removal. Different
versions coexist. Archive names and installation paths never affect symbol
identity. See [the module package format](DDF_MODULE_PACKAGE_FORMAT.md).

In the standalone editor, open a workspace and choose **Modules > Manage project
modules** (`Moduli > Gestisci moduli progetto...`). The dialog previews package
metadata before installation and can pack the active workspace, install a
package or source folder, validate, refresh and remove exact versions. These
filesystem operations are intentionally absent from the embedded shell.

## .NET backend and standalone editor build

The 0.13.x backend transforms the common IR into generated C# and compiles it
with Roslyn into .NET assemblies with original-source debug mappings. Integrated
execution uses registered host APIs; standalone publishing produces a
self-contained executable with its runtime and dependencies. Missing host API
implementations prevent standalone export. The VM remains the embedded runtime
and semantic reference; the reusable WinForms assembly does not reference
Roslyn.

The standalone editor exposes **Compilazione > Esegui build**. Debug and Release
are selected explicitly and persisted, with initial target `win-x64`.
Development artifacts (DLL, Portable PDB, DDF source map and manifest) are kept
at stable user-facing paths below `bin\Debug\win-x64\artifacts` or
`bin\Release\win-x64\artifacts`. The verified content-addressed cache is an
internal implementation detail below
`obj\ddf\cache\<Configuration>\<target>\v1.0\<prefix>\<fingerprint>` and is
never exposed as the development output. Every successful build replaces the
complete stable artifact directory atomically; Debug and Release keep distinct
caches and outputs. Output reports the equivalent `ddf build` command,
configuration, target, stable artifact directory, technical cache root and
navigable diagnostics. The
App launcher injects the compiler bridge; an embedded `MainForm(true)` hides the
compilation menu and rejects that bridge. The adjacent **Esegui ultima build**
command becomes available after a successful
build in the current editor session. It links that exact verified artifact and
runs it in the restricted one-shot worker without rebuilding; later source edits
are reported but do not alter the executed program. Output includes program
output, returned JSON value and runtime instruction count, and Stop cancels the
worker. **Compila ed esegui** performs the same verified build and immediately
runs the newly produced artifact, while folder opening and self-contained
publication remain separate operations. **Pubblica EXE...** asks for the package
name and parent directory, refuses implicit replacement, and reports the final
executable path in Output.

The published package has an explicit two-level layout. Its root contains only
`<application>.exe` plus the `support` directory. The root executable is a .NET
apphost generated for `support/<application>.dll`, so it resolves the bundled
runtime and application files relative to its own location and does not depend
on the process working directory. Managed/native runtime files, DDF modules,
external redistributable assemblies, licenses, notices and
`ddf-publish.json` are stored below `support`. Manifest format `1.2` records the
support directory and uses package-root-relative paths. Publication fails if a
second file or directory escapes the root allowlist.

The standalone publication dialog also selects an optional Windows `.ico`
file, shows a preview and remembers that preference. Clearing the choice uses
the embedded DDF multi-resolution icon. The publisher validates the ICO
directory before invoking MSBuild, embeds its resources in the root apphost and
records icon source, file name and SHA-256 in the manifest. The same hash is an
input to `publicationFingerprint`, so changing only the icon still identifies
a different publication. Invalid or truncated icons fail with `DDFC606`.

The automated distribution gate launches the package from an unrelated working
directory with an empty `PATH`, nonexistent `DOTNET_ROOT` and
`DOTNET_ROOT_X64`, and multi-level lookup disabled. It also requires the
self-contained host/runtime files below `support`, recomputes every declared
size and SHA-256, rejects undeclared payload files, and proves that an existing
destination and its sentinel remain untouched. This hermetic check does not
replace the final acceptance run on a genuinely clean Windows x64 VM.

The standalone CLI now provides `ddf validate`, `ddf test`, `ddf build`,
`ddf run` and `ddf package`.
All commands use the same Core preparation as the IDE; build then uses the
verified Roslyn plan and content-addressed artifact cache. Run links the verified
bundle and executes it only in the one-shot restricted worker, without an
in-process fallback. Text and JSON diagnostics plus stable exit codes form the
initial CI contract. Package publishes the first DDF-produced self-contained
Windows x64 executable, including the selected .NET runtime, DDF runtime ABI,
modules, licenses and a hashed payload manifest while excluding Roslyn and the
Compiler assembly. Test validates the project and reports every declarative
language-package conformance case (tokens, diagnostics, formatting and runtime
result) in text or JSON; application-level test assertions remain a later
language feature. See [DDF_CLI.md](DDF_CLI.md).

The final distribution check is intentionally external to the ordinary build
suite. [DDF_CLEAN_VM_ACCEPTANCE.md](DDF_CLEAN_VM_ACCEPTANCE.md) describes how
to preserve the exact package, hashes and JSON report. The `0.13.4` candidate
passed this gate on Windows 10 Professional x64 build `10.0.19045` without a
modern .NET installation; the retained report records all nine checks, process
exit code `0`, the expected value `42`, and no standard error output.

See [the ecosystem architecture](ECOSYSTEM_ARCHITECTURE.md) and
[the roadmap](ROADMAP.md) for the migration, SDK, teaching and AI decisions.
