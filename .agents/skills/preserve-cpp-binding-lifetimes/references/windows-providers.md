# Windows bindings

## Cgal

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Confirm the operation is in the package’s finite EPICK surface and check the documented ownership of the result.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.

```csharp
using TedToolkit.CppBindings.Cgal;

var origin = Point_2.Create(0, 0);
var point = Point_2.Create(3, 4);
var squaredDistance = CgalKernel.SquaredDistance(origin, point); // 25
```

## Fcl

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Check `FclModelBuildResult.Code` and dispose every successful model owner. Use the provider’s contact semantics for initial overlap, endpoint contact, and a miss.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.

## Manifold

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Dispose the owner returned by `Manifold.Create`. Treat mesh data returned by `GetMesh` as a fresh managed result and check status before subsequent geometry operations.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.

## Occt

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Use generated factories for owning values. Keep borrowed `ref` and `ref readonly` returns inside the native owner’s lifetime and invalidation rules.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.

```csharp
using TedToolkit.CppBindings.Occt;

var point = gp_Pnt2d.Create(1.25, 2.5);
point.SetCoord(3.5, 4.75);
```

## Smlib

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Use `Solids.CreateBox` in a `using` scope for an owned solid. Check a representative `ComputeVolume` call before extending the consumer.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.

```csharp
using TedToolkit.CppBindings.Smlib;

using var box = Solids.CreateBox(0, 0, 0, 2, 3, 4);
double volume = box.ComputeVolume(1e-7); // 24
```
