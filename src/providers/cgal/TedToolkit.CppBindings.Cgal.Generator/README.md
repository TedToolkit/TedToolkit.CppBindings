# TedToolkit.CppBindings.Cgal.Generator

This package supplies configurable finite CGAL generation through the provider-neutral Shared
pipeline. The default `epick-windows-v1` profile targets CGAL 6.2 from vcpkg `x64-windows` and emits
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

The locked header inventory makes CGAL package drift visible. Set
`RequireLockedHeaderInventory = false` only for an explicitly configured development profile; that
run does not claim the `epick-windows-v1` package identity. The default also verifies installed
CGAL/GMP/MPFR versions and vcpkg ABIs plus the pinned CMake and MSVC versions.

Pass `ProfileManifestFile` and the matching `ProfileId` to select another explicit version-1 finite
profile. Its selected headers, closed signatures, source evidence, roots, and declaration
dependencies are snapshotted before generation. The bundled `profiles/epick-windows-v1` vcpkg
manifest and registry configuration reproduce the default package inputs.

Generated inventory files distinguish all installed public headers from the finite candidate set.
The Generator recursively closes real `#include <CGAL/...>` dependencies from the maintained roots,
reports other headers as `not-reachable-from-finite-profile`, and independently requires the source
evidence named by every candidate. Candidates without a provider semantic projection are emitted in
the unsupported inventory with a narrow reason. Admitted declarations become a nonempty Shared
semantic graph; Shared derives their managed/native files and function-table exports from that one
model. Per-declaration artifact inventories bind every admitted declaration to both emitted sides.
