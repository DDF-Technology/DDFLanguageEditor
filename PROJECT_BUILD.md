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
  "schemaVersion": "1.1",
  "name": "example",
  "module": {
    "id": "example.application",
    "version": "1.0.0",
    "exports": [ "main" ]
  },
  "languageId": "ddf",
  "languagePackageVersion": "0.12.2.1",
  "sources": [ "**/*.ddf" ],
  "entrySource": "main.ddf",
  "entryPoint": "main",
  "dependencies": [],
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

## Commands

- **Convalida progetto** parses, checks and lowers every selected source without
  writing files.
- **Compila progetto** runs the same pipeline and writes a deterministic build
  receipt containing exact versions, backend, inputs, hashes and fingerprint.
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
`main` definitions stop validation with a configuration diagnostic. A completely untitled document can be
validated, run and debug, but must be saved before **Compila progetto** may
write an artifact.

## Current boundary

From 0.11.0, `ddf-vm-ir-v1` validates and executes all selected files as one
project-wide program. Breakpoints, stepping, runtime diagnostics and stack
frames retain their source document. Duplicate runtime declarations produce
`DDFB010`. Global initialization follows deterministic source/declaration order;
forward or cyclic global dependencies produce `DDFB011`.

Module identity, version and sorted exports are pinned reproducibly in the
receipt and fingerprint. Dependencies are also pinned, but cross-module package
resolution is not implemented yet and therefore cannot satisfy a source symbol.
See [the module-system specification](DDF_MODULE_SYSTEM_SPEC.md) for visibility,
initialization, dependency and directive rules.

## Planned .NET backend

The selected direction for 0.13.x is common IR to generated C#, compiled with
Roslyn into .NET assemblies with original-source debug mappings. Integrated
execution uses registered host APIs; standalone publishing produces a
self-contained executable with its runtime and dependencies. Missing host API
implementations must prevent standalone export. This backend is planned and
does not replace the current `ddf-vm-ir-v1` configuration yet.

See [the ecosystem architecture](ECOSYSTEM_ARCHITECTURE.md) and
[the roadmap](ROADMAP.md) for the migration, SDK, teaching and AI decisions.
