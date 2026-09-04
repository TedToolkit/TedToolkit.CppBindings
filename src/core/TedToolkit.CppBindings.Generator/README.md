# TedToolkit.CppBindings.Generator

Provider-neutral generation stages for C++ bindings. This .NET 10 tool package owns source-path
validation, publication, and the existing Windows x64 native function-table/loading mechanics.
It contains no Clang declarations, OCCT rules, native package dependencies, or ownership classification.

## Use

The package is unreleased; pack it locally before adding a direct package reference:

```xml
<PackageReference Include="TedToolkit.CppBindings.Generator" Version="1.0.0" />
```

Implement `IGenerationProvider`, register its preparation module types on a `PipelineBuilder`, then
call `AddCppGenerators(options, provider)`. The complete executable example is the
[independent packed consumer](../../../tests/TedToolkit.CppBindings.Generator.Tests/Fixtures/PackageConsumer/Program.cs).
It references only this package and needs neither a provider assembly nor friend access.

`GenerationOptions` supplies dedicated, non-overlapping `CSharpFolder` and `CppFolder` directories,
`CSharpNamespace`, and a portable `NativeLibraryBaseName`. The built-in target is `win-x64`, with
Cdecl function pointers and the MSVC native ABI. Other runtime identifiers are rejected before
preparation. `CppVersion` defaults to 17 and is available to the provider's native build description;
`IsInternal` is available to its managed emitters.

## Provider contract

`CreatePlanAsync` runs once after every declared preparation module succeeds. Return a `GenerationPlan`
containing copied, read-only C# and C++ source inventories and the exact ordered native export names.
Both sides must derive their slot indexes from that same order; the core never sorts it. Preparation
and renderer failures/cancellation propagate instead of becoming an empty successful plan.

Each `GeneratedSource` has an output-relative path and an asynchronous streaming renderer. Write to
the supplied `TextWriter`, observe cancellation, and neither retain nor dispose the writer. A provider
can preserve its own renderer concurrency limits; the core owns file creation and disposal. Rendering
starts only after the entire plan passes path, collision and export checks.

The core reserves `NativeApi.g.cs` and `NativeFunctionTable.cpp`. A provider's native build description
must include the latter. The provider owns its native headers, error support, library dependencies,
and declaration-specific sources. Do not return either reserved path yourself.

Output directories are exclusively for generated artifacts: successful preparation clears stale
contents while preserving the roots. Invalid plans do not clear existing output. Rendering failure
can leave partial files and must never be treated as successful publication. Filesystem roots,
overlapping roots, traversal paths, collisions, and filesystem links are rejected. Callers must not
concurrently replace output paths while a run is active.

## Native lifetime

Generated loading uses `NativeLibrary.Load`, one exported `NativeApi_GetFunctionTable` entry, and a
native pointer table retained for process lifetime. It introduces no fingerprint, runtime manifest,
table replacement, or unloading. Package-owned native artifacts must match the generated managed
binding; this package does not authorize substituting a different native build.

## Verify

From the repository root:

```powershell
pwsh -NoProfile -File Build/VerifyGeneratorPackage.ps1
```

This PowerShell 7.5 verification packs the core, builds an isolated consumer with a fresh package
cache, checks the restored package hash and dependency graph, and exercises preparation ordering,
immutable snapshots, exact export ordering, deterministic output, path rejection, failure and
cancellation. It retains logs and results under `out/verification`. NuGet access is required to
restore ordinary tool dependencies. This proves the generic boundary, not a complete OCCT build.

[OCCT provider](../TedToolkit.CppBindings.Occt.Generator/README.md) · [Repository overview](../../../README.md)
