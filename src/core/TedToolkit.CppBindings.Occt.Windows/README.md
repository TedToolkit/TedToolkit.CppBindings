# TedToolkit.CppBindings.Occt.Windows

Generated OCCT bindings for the pinned `win-x64`, OCCT 8.0.1 artifact set. Public APIs use the
`TedToolkit.CppBindings.Occt` namespace and reference `TedToolkit.CppBindings.Occt.Runtime` for ownership and error
projection.

The package contains its matched generated native library and the complete `x64-windows` runtime
DLL set required by the generated surface. Consumers do not run the Generator and do not need OCCT,
vcpkg, Clang, or CMake.

## Use

This .NET 8 package is unreleased. After a maintainer builds and packs it locally, reference it from
that local feed together with the opt-in diagnostics package:

```xml
<PackageReference Include="TedToolkit.CppBindings.Occt.Windows" Version="1.0.0" />
<PackageReference Include="TedToolkit.CppBindings.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

```csharp
using TedToolkit.CppBindings.Occt;

var point = gp_Pnt2dExtensions.Create(1.25, 2.5);
point.SetCoord(3.5, 4.75);
```

The namespace has no platform suffix. Value layouts, generic `Owned<T>`, owning OCCT `Handle<T>`
and non-owning `handle<T>` retain their separate native semantics. Borrowed references require the
original owner to remain alive and obey the native invalidation rules. See the
[Runtime contracts](../TedToolkit.CppBindings.Occt.Runtime/README.md) for lifetime obligations.
Consumer diagnostics are a direct opt-in dependency; this package does not embed them.

## Supported surface

The Windows generation host selects every public OCCT header. Types and members are retained when
they can be represented and linked against the delivered OCCT binaries; exact members proven absent
or uninstantiable are omitted. No other Windows architecture is currently supported.

## Verify package delivery

From the repository root, build `TedToolkit.CppBindings.slnx` in Release, then run
`pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1`. It packs without publishing, verifies the
native artifact and dependency inventory, restores fresh packages into an isolated consumer, and
runs the same native smoke source without local generator project references.

[Repository overview](../../../README.md) · [Source generation](../TedToolkit.CppBindings.Occt.Generator/README.md)
