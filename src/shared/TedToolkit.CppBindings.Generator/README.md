# TedToolkit.CppBindings.Generator

Provider-neutral generation stages for C++ bindings. This .NET 10 tool package owns source-path
validation, publication, and the existing Windows x64 native function-table/loading mechanics.
It contains no Clang declarations, OCCT rules, native package dependencies, or ownership classification.

## Use

The package is unreleased; pack it locally before adding a direct package reference:

```xml
<PackageReference Include="TedToolkit.CppBindings.Generator" Version="1.0.0" />
```

Implement `IGenerationProvider` directly, or derive from `SemanticGenerationProvider` when the
provider uses the shared semantic extension boundary. Register its preparation module types on a
`PipelineBuilder`, then call `AddCppGenerators(options, provider)`. The complete executable example is
the [independent packed consumer](../../../tests/TedToolkit.CppBindings.Generator.Tests/Fixtures/PackageConsumer/Program.cs).
It references only this package and needs neither a provider assembly nor friend access.

`GenerationOptions` supplies dedicated, non-overlapping `CSharpFolder` and `CppFolder` directories,
`CSharpNamespace`, and a portable `NativeLibraryBaseName`. The built-in target is `win-x64`, with
Cdecl function pointers and the MSVC native ABI. Other runtime identifiers are rejected before
preparation. `CppVersion` defaults to 17 and is available to the provider's native build description;
`IsInternal` is available to its managed emitters.

## Provider contract

`CreatePlanAsync` runs once after every declared preparation module succeeds. A direct
`IGenerationProvider` returns a `GenerationPlan` containing copied, read-only C# and C++ source
inventories and one exact native export order. Both sides must derive their slot indexes from that
same order. Preparation and renderer failures or cancellation propagate instead of becoming an
empty successful plan.

Each `GeneratedSource` has an output-relative path and an asynchronous streaming renderer. Write to
the supplied `TextWriter`, observe cancellation, and neither retain nor dispose the writer. A provider
can preserve its own renderer concurrency limits; the core owns file creation and disposal. Rendering
starts only after the entire plan passes path, collision and export checks.

The core reserves `NativeApi.g.cs` and `NativeFunctionTable.cpp`; the native project automatically
receives the latter in its complete compiled-source inventory. `BindingCMakeProjectDefinition` lets
providers submit dependency targets, compile facts, target properties, and optional bounded
family/depth unity grouping while Shared owns deterministic CMake rendering. Providers still own
their native headers, local error support, dependency choices, and declaration-specific semantics.
Do not return either reserved path yourself.

## Semantic extension boundary

`BindingSemanticEngine` composes provider-owned `IBindingTypeRule`, `IBindingLayoutPolicy`,
`IBindingTemplatePolicy`, and named `IBindingEmitterPrimitive` implementations. A
`SemanticGenerationProvider` overrides `CreateProviderModelAsync` and returns a
`BindingProviderModel`: normalized declarations and enums, explicit roots and dependency identities,
a finite emission profile, supplemental sources and exports, and native build metadata.

Shared owns the normalized record, member, type, transport, lifetime, layout, and template model. It
closes roots over dependencies, rejects incomplete semantics, assigns the single export-slot order,
and emits both declaration bodies before creating the final `GenerationPlan`. Providers supply
compiler-derived facts and finite syntax/runtime policy; they do not submit declaration-renderer
delegates or reconstruct a second model for either language. Supplemental provider renderers are
limited to genuinely library-specific support such as adapter types or polymorphic result projection;
generic native-project boilerplate belongs to Shared.

Finite handwritten native profiles use `BindingFiniteProfileApi` when their ABI is not a direct set
of C++ member declarations. The API contains collections of named status, value, owner, and result
types plus an ordered set of composable operations; it does not prescribe one provider's operation
topology. Explicit buffer definitions drive span validation and pointer/length transport. Operations
select conditional or direct owner construction, scalar or composite projection, and counted
two-phase buffer copying. Public managed types, managed ABI transports, and native transports remain
distinct inputs. The explicit export order drives both managed slots and
`NativeFunctionTable.cpp`. Providers retain only their native algorithm bodies and
dependency-specific build policy. Shared contains no provider-name branch or provider assembly
dependency, and validates the complete graph before publishing a plan.

Type rules are evaluated in registration order and the first match wins. Layout and template
policies must return `BindingAdmission.Admitted` or a rejected result with a stable reason. Emitter
primitive names are unique and case-sensitive. Keep these policies in the provider package; Shared
must never reference a provider assembly.

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
pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1
dotnet run --project tests/TedToolkit.CppBindings.Generator.Tests/TedToolkit.CppBindings.Generator.Tests.csproj -c Release
pwsh -NoProfile -File Build/VerifyGeneratorPackage.ps1
```

This PowerShell 7.5 verification packs Shared, builds an isolated consumer with a fresh package
cache, checks the restored package hash and dependency graph, and exercises real Shared-produced
managed/native declaration bodies, preparation ordering, detached semantic graph snapshots, exact export ordering,
deterministic output, narrow semantic rejection, path rejection, failure, and cancellation. It
retains logs and results under `out/verification`. NuGet access is required to restore ordinary tool
dependencies. This proves the generic boundary, not a complete OCCT build.

[OCCT provider](../../providers/occt/TedToolkit.CppBindings.Occt.Generator/README.md) · [Repository overview](../../../README.md)
