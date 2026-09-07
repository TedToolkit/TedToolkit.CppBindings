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
