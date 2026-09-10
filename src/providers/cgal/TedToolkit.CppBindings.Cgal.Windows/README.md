# TedToolkit.CppBindings.Cgal.Windows

Ready-to-use `win-x64` bindings for the finite CGAL 6.2 `epick-windows-v2` profile. Public APIs use
the `TedToolkit.CppBindings.Cgal` namespace and reference
`TedToolkit.CppBindings.Cgal.Runtime` for exception and result semantics.

The package contains the matched generated native wrapper, its recursively resolved app-local DLL
dependencies, and CGAL/GMP/MPFR notices. Consumers do not run the Generator and do not need CGAL,
vcpkg, GMP, MPFR, Clang, CMake, or Visual Studio installed.

## Use

```xml
<PackageReference Include="TedToolkit.CppBindings.Cgal.Windows" Version="1.0.0" />
```

```csharp
using TedToolkit.CppBindings.Cgal;

var origin = Point_2Extensions.Create(0, 0);
var point = Point_2Extensions.Create(3, 4);
var squaredDistance = CgalKernel.SquaredDistance(origin, point); // 25
```

The generated namespace has no platform suffix. Value types preserve their native layouts, and
borrowed reference returns remain valid only while their original owner remains alive and
unmodified. Native CGAL and standard-library failures are copied and projected through the Runtime
package. Undeclared nonempty intersection alternatives throw `CgalUnknownResultException`.

## Supported surface

The package contains every declaration admitted by the versioned `epick-windows-v2` profile:
`Point_2`, `Point_3`, `Segment_2`, squared-distance operations, segment intersection, coordinate
access, and the associated result projection. It does not claim CGAL's unbounded template surface.

## Verify

From the repository root, run:

```powershell
pwsh -NoProfile -File Build/VerifyCgalWindowsPackage.ps1
```

The verifier generates and builds the locked surface serially, validates managed/native inventory
agreement and recursive PE dependency closure, packs without publishing, restores into an isolated
consumer, and performs real 2D/3D, intersection, empty-result, and precondition-failure calls.

[Provider overview](../README.md) · [Generator](../TedToolkit.CppBindings.Cgal.Generator/README.md) ·
[Runtime](../TedToolkit.CppBindings.Cgal.Runtime/README.md)
