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
`RequireLockedHeaderInventory = false` only for an explicitly configured non-Windows development
profile; that run does not claim the `epick-windows-v1` package identity.

Generated inventory files distinguish all installed public headers from the finite candidate set.
Headers outside the maintained roots are reported as `not-selected-by-finite-profile`; they are not
silently treated as supported or unsupported declarations.
