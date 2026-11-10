# TedToolkit.CppBindings.Generator

Provider-neutral generation stages for C++ bindings. This .NET 10 tool package owns source-path
validation, public header discovery, publication, and generation of the native function table and
its managed loader. Provider-specific native dependencies and ownership rules stay with their providers.

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

`GenerationOptions` supplies dedicated, non-overlapping `CSharpFolder` and `CppFolder` directories as
`DirectoryPath` values from `TedToolkit.FileSystem`. Relative paths are resolved when the options
are initialized. For example:

```csharp
using TedToolkit.CppBindings.Generator;
using TedToolkit.FileSystem;

var options = new GenerationOptions
{
    CSharpFolder = new DirectoryPath("generated/csharp"),
    CppFolder = new DirectoryPath("generated/cpp"),
};
```

Existing `DirectoryInfo` inputs can be assigned through FileSystem's implicit conversion; reading
the properties now returns `DirectoryPath`. Provider vcpkg roots use `DirectoryPath`, and explicit
CGAL profile documents use `FilePath`. This changes the public API types, so existing compiled
consumers must be rebuilt. The other settings include
`CSharpNamespace` and a portable `NativeLibraryBaseName`. `RuntimeIdentifier` is available to
binding providers and defaults to `win-x64` for existing callers. Shared accepts any target RID.
The generated function table uses the C++ compiler's target to choose its export declaration.
Builds with a different export convention can define `TEDTOOLKIT_CPPBINDINGS_EXPORT` when compiling
the generated function table.
`CppVersion` defaults to 17 and is available to the provider's native build description;
`IsInternal` is available to its managed emitters. Set `NativeLibraryVersion` to a `System.Version`
when the provider's primary native package must match exactly. Leaving it unset preserves the
provider's existing unversioned resolver behavior; it does not promise that every resolver chooses
the globally newest release.

## Generate from headers

`GenerationOptions.Headers` supplies the public inputs for shared discovery. The usual
`AddCppGenerators` pipeline automatically includes external C-linkage functions declared in those
headers. Definitions in headers included only as dependencies are used for type resolution.

```csharp
using ModularPipelines;
using TedToolkit.CppBindings.Generator;
using TedToolkit.FileSystem;

var options = new GenerationOptions
{
    CSharpFolder = new DirectoryPath("generated/csharp"),
    CppFolder = new DirectoryPath("generated/cpp"),
    CSharpNamespace = "Example.Bindings",
    NativeLibraryBaseName = "example_bindings",
    Headers =
    [
        new(new FilePath("include/Geometry.h")),
        new(new FilePath("include/Utilities.h"), WrapperClassName: "Control"),
    ],
    LinkLibraries = ["C:/example/lib/example.lib"],
};

var pipeline = await Pipeline.CreateBuilder().AddCppGenerators(options).BuildAsync();
await pipeline.RunAsync();
```

The default managed class name uses the header stem followed by `Wrapper`. Both single declarations
and blocks of `extern "C"` are recognized. C++ namespaces qualify source lookup while C-linkage names
identify the underlying symbols. Repeated declarations of the same C symbol share one generated call.

Shared resolves C++ records in function signatures against the existing semantic model first.
With an existing `SemanticGenerationProvider`, pass the same options to
`AddCppGenerators(options, provider)` to include the header functions in the provider's function table.
Complete plain value records discovered from headers pass through the same exact-layout C# emitter.
Typed pointers and borrowed references preserve the admitted type. Value transport uses pointers
to caller storage. Unproved layouts and value lifetimes produce a diagnostic naming the signature.

Shared header discovery currently targets the Windows x64 MSVC ABI. The default CMake project uses
`IncludeDirectories`, `CompileDefinitions`, and `LinkLibraries`. An existing provider supplies its
own native build metadata and must use matching compilation settings for its public inputs.
Build the generated CMake project with MSVC. If the vendor ships only a DLL, obtain its import
library or generate one with MSVC `lib /DEF` from verified named function exports. Deploy the DLL
and its native dependencies alongside the adapter. The managed project compiling generated code
references `TedToolkit.CppBindings.Runtime` and enables unsafe code.

## Provider contract

`NativeSources` returns resolved `NativeSourceInputs` for the binding provider. Use the headers
and include paths for declaration discovery and pass the source directory and library paths to
the native build as needed.

```csharp
var inputs = NativeSources.FromHeaders(
    new DirectoryPath("sdk/include"),
    ["widget.h"],
    dependencyIncludeDirectories: [new DirectoryPath("dependency/include")],
    dependencyLibraries: [new FilePath("dependency/lib/libwidget.a")]);

builder.AddCppGenerators(options, new WidgetGenerationProvider(inputs));
```

When creating a `BindingCMakeProjectDefinition`, the binding provider passes `IncludeRoot` and
`DependencyIncludeDirectories` as `includeDirectories` and the explicit `DependencyLibraries`
files as `linkLibraries`. Use quoted CMake arguments and forward slashes in paths. The emitted project uses
`target_include_directories` to compile the bridge and `target_link_libraries` to resolve symbols
from the dependency. A library search directory helps locate a library. The link step still needs
the selected file or a CMake target. A header-only dependency can leave `dependencyLibraries`
empty when its headers supply all required implementations.

`NativeSources.FromVcpkg` takes a populated `installed` or `vcpkg_installed` directory, an explicit
triplet, and entry headers. `NativeSources.FromSourceDirectory` takes the C++ source directory,
its relative include directory, and entry headers. Construct `NativeSourceInputs` directly for a
custom layout.

`CreatePlanAsync` runs once after every declared preparation module succeeds. A direct
`IGenerationProvider` returns a `GenerationPlan` containing copied, read-only C# and C++ source
inventories and one exact native export order. Both sides must derive their slot indexes from that
same order. Preparation and renderer failures or cancellation propagate instead of becoming an
empty successful plan.

`AddCppGenerators` calls the options-aware `CreatePlanAsync(options, cancellationToken)` overload.
Existing providers that only implement the cancellation-only overload remain source compatible.
Their default options-aware implementation delegates when `NativeLibraryVersion` is unset and
throws `NotSupportedException` when an exact version is requested, so a provider cannot silently
ignore the selection.

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
