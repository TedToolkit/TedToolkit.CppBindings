# TedToolkit.CppBindings.Cgal.Generator

This package supplies configurable finite CGAL generation through the provider-neutral Shared
pipeline. The default `epick-windows-v2` profile targets CGAL 6.2 from vcpkg `x64-windows` and emits
matched managed/native sources plus deterministic coverage inventories.

```csharp
var options = new CgalGenerationOptions
{
    VcpkgRoot = new DirectoryInfo(@"C:\vcpkg"),
    CSharpFolder = new DirectoryInfo("generated/csharp"),
    CppFolder = new DirectoryInfo("generated/cpp"),
    CSharpNamespace = "TedToolkit.CppBindings.Cgal",
    NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
    CppVersion = 20,
};

builder.AddCgalGenerators(options);
```

The Generator discovers public headers directly from the selected vcpkg include tree. By default,
it also compares that tree with vcpkg's installed CGAL package list so missing, added, or manually
changed package files fail closed. Set `RequireLockedHeaderInventory = false` only when the selected
vcpkg installation does not provide package-list metadata. The default separately verifies installed
CGAL/GMP/MPFR versions and vcpkg ABIs plus the pinned CMake and MSVC versions.

Pass `ProfileManifestFile` and the matching `ProfileId` to select another explicit version-1 finite
profile. Its selected headers, closed signatures, source evidence, roots, and declaration
dependencies are snapshotted before generation. The bundled `profiles/epick-windows-v2` vcpkg
manifest and registry configuration reproduce the default package inputs.

Generated inventory files distinguish all installed public headers and compiler-observed source
declarations from the finite candidate set.
The Generator recursively closes real `#include <CGAL/...>` dependencies from the maintained roots,
reports other headers as `not-reachable-from-finite-profile`, and uses Clang to enumerate every
public declaration originating in that closure. Direct non-template declarations enter the finite
candidate set and receive a narrow unsupported reason until a provider projection exists. Open
templates and their dependent declarations remain visible in `source-declaration-inventory.json`
but are not candidates. Clang compiles the profile's kernel alias, layouts, closed constructors,
operations, and return-reference categories before a supported closed entry can be admitted.

Admitted declarations become a nonempty Shared semantic graph, and Shared derives their
managed/native files and function-table exports from that one model. Per-declaration artifact
inventories name the actual emitted file and symbol on both sides. Value transports keep ABI storage
private. Native `const T&` accessors are emitted as `ref readonly T`; each returned reference is
borrowed from the receiver and remains subject to that receiver's lifetime and invalidation rules.
CGAL continues to own its finite adapter and tagged intersection alternatives; it submits CMake
dependency and target facts to Shared instead of rendering generic native-project boilerplate.
