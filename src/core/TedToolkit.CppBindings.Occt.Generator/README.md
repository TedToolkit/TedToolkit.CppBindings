# TedToolkit.CppBindings.Occt.Generator

Generate matching C# and C++ OCCT bindings from installed headers and compiler-proved facts.
This .NET 10 tool package supplies the OCCT provider for
[the reusable generation platform](../TedToolkit.CppBindings.Generator/README.md), not the
ready-to-use [Windows bindings](../TedToolkit.CppBindings.Occt.Windows/README.md).

The package is unreleased. Use a locally packed package or a repository project reference.

## Generate sources

Install OCCT 8.0.1 in vcpkg's `x64-windows` triplet and set `VCPKG_ROOT`. The supported profile
is Windows x64, MSVC ABI, a compatible Clang parser/probe, and CMake 3.28 or later.

```csharp
using ModularPipelines;
using TedToolkit.CppBindings.Occt.Generator;

var output = new DirectoryInfo("output/generated");
var pipeline = await Pipeline.CreateBuilder()
    .AddOcctGenerators(new OcctGenerationOptions
    {
        DeclOptions = [new OcctDeclarationOptions("Geom2d_BSplineCurve")],
        CSharpFolder = output.CreateSubdirectory("csharp"),
        CppFolder = output.CreateSubdirectory("cpp"),
    })
    .BuildAsync();
await pipeline.RunAsync();
```

Output roots must be dedicated and non-overlapping: validated preparation clears their old
contents. Generation performs native compiler probes but does not build the final binding DLL.
Compile `cpp/CMakeLists.txt` separately against the matching OCCT libraries. Managed sources and
the resulting DLL are one inseparable artifact set.

## Capabilities and configuration

| Task | Entry or option | Contract |
| --- | --- | --- |
| Select root records | Required `DeclOptions` | Each `OcctDeclarationOptions` accepts a header stem or generated enum value. The exact `<stem>.hxx` must exist; dependencies are discovered recursively. |
| Select public headers | `GenerateAllPublicHeaders = true`, `DeclOptions = []` | Complete-header discovery reports excluded headers in `unsupported-headers.txt` beside the language roots. |
| Configure native discovery | `Triplet`, `CommandLineArgs` | An omitted triplet selects a compatible installed one; extra arguments go to Clang. Only the proved `win-x64` runtime profile is supported. |
| Configure output | Inherited `CSharpFolder`, `CppFolder`, `CSharpNamespace`, `NativeLibraryBaseName`, `IsInternal` | Default namespace is `TedToolkit.CppBindings.Occt`; basename is `ted_toolkit_occt`. Namespace applies to layouts, extensions, enums and loader without changing native identities. |
| Configure language | Inherited `CppVersion` | Defaults to 17; passed to parsing, probes and emitted CMake. Another language version does not establish another supported ABI. |
| Select projected fields | `FieldTypeToGenerate` | Filters modeled fields; native layout still determines storage. |
| Retained layout option | `GetFieldOffsetByRunning` | Compatibility property, currently unused. Layout comes from Clang and compiler proof. |
| Discover header selectors | Bundled `OcctHeaderTypeGenerator` | Generates `TedToolkit.CppBindings.Occt.Generator.OcctHeaderType` when installed headers exist. The non-packable SourceGenerators component ships here as analyzer assets, not as consumer lifetime diagnostics. |

## Preparation and output boundary

```text
OcctParseModule → OcctCompilerProbeModule → one validated GenerationPlan
  → CleanGenerationOutputModule → GenerateCSharpModule + GenerateCppModule
```

The first two stages are provider-owned; the final three are generic. Both emitters use the same
finalized private model and exact export order. Preparation failures, invalid paths, collisions,
duplicate exports and cancellation fail the pipeline instead of yielding a successful empty set.

The provider owns classification, physical layouts, error projection, headers, CMake and native
dependencies. The core owns publication, loader and function table. Record/template-family and enum
renderer limits remain unchanged; this migration does not adopt a benchmark strategy.

Native source filenames use a readable stem capped at 80 characters, with a stable hash for long
template names. This leaves room for CMake's object-directory suffix in nested worktrees without
changing managed names or native exports. The Windows build rejects unsafe CMake object-path
warnings before compilation; use a shorter output root if the checkout still exceeds that budget.

Each supported object has one unmanaged physical representation. Reference returns remain
`ref readonly T` or `ref T`, subject to native owner lifetime and invalidation rules.
Transient operations expose separate direct `Handle<T>` and `in handle<T>` receivers invoking
the same slot; only the owning overload checks owner liveness. Lowercase `handle<T>` is non-owning
storage, not an owner interface. Handles returned by value use owning `Handle<T>`; non-transient
RAII uses generic `Owned<T>`.

Overlapping ordinary fields, including union members, use same-named `ref T` properties over shared
sequential storage (`ref readonly T` for const storage). References alias native bytes without
copying, retaining or allocating. Non-overlapping fields remain fields; bitfields remain value
properties. Reference accessors also preserve aliasing through `in` or `ref readonly` containing
receivers, without defensive copies. Reflection and field-specific syntax must account for the property distinction.
Callers must obey native union active-member, construction/destruction, owner-lifetime and
invalidation rules; accessing a generated property does not activate a union member.

Representable template type arguments stay generic; non-type values and unrepresentable arguments
such as `void` are fixed into the family name. A shared managed family requires a valid physical
graph. Each selected closed native specialization retains direct invocation slots without runtime
generic native dispatch.

Explicit full specializations retain their own closed declarations. Template families with distinct
native base identities also remain closed; fixed-base families can share a managed type while
retaining the exact base interface. Generic base dependence is not inferred from coincident type
spellings. Family compatibility is finalized only after native ownership classification.

## Fail-closed limitations

The installed headers and DLLs are authoritative. Incomplete/inaccessible declarations,
unrepresentable layout/transport, unsafe ownership, unavailable copying/destruction and uncallable
members are rejected at the narrowest safe boundary. Templates, streams and smart pointers are
not excluded merely by category; each concrete specialization needs proof.

All-header discovery excludes missing/broken include dependencies and their dependants, the
installed broken `MathLin_Jacobi.hxx`, and incompatible desktop/GLES declarations from
`OpenGl_GLESExtensions.hxx`. It reports exclusions without patching installed headers.
Exact members proved absent from the Windows libraries are recorded in
[`UnsupportedNativeExports.txt`](Resources/UnsupportedNativeExports.txt). Recreate linker evidence
when the native installation changes; do not replace a member failure with broad type exclusion.

The repository has no vcpkg manifest; maintainers supply the documented installation.
No Linux, ARM, alternate compiler ABI, replaceable native DLL, or published package is implied.

## Verify

From the repository root, run all-header source generation:

```powershell
$env:VCPKG_ROOT = 'C:\vcpkg'
dotnet run --project tests/TedToolkit.CppBindings.Occt.Console -c Release
```

`dotnet build TedToolkit.CppBindings.slnx -c Release` additionally builds the Windows project's
matched native artifact using Visual Studio's MSVC and Ninja tools. The separate
`TedToolkit.CppBindings.Occt.GeneratedSmoke` executable exercises native Value, Owned, Handle,
inheritance and error behavior. TUnit regressions live in
[`TedToolkit.CppBindings.Occt.Generator.Tests`](../../../tests/TedToolkit.CppBindings.Occt.Generator.Tests).
The independent generic package consumer proves the public provider boundary, not OCCT correctness.

`pwsh -NoProfile -File Build/VerifyOcctGeneratorPackage.ps1` packs and consumes this provider in an
isolated project, checks header source-generator assets and public identities, and compares two
real-header generation runs. After a successful Release solution build,
`pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1` consumes the matching Windows package and
its direct analyzer package through the native smoke test without generating code in the consumer.

[Repository overview](../../../README.md) · [Runtime](../TedToolkit.CppBindings.Occt.Runtime/README.md)
· [Platform architecture](../../../docs/architecture/cpp-bindings-platform.md)
