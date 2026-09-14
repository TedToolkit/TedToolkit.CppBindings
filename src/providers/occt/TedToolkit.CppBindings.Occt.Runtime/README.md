# TedToolkit.CppBindings.Occt.Runtime

OCCT-specific .NET 8 ownership and exception interpretation, built on the provider-neutral
[Runtime](../../../shared/TedToolkit.CppBindings.Runtime/README.md). This package has no generated declarations
or native imports. All its public types use `TedToolkit.CppBindings.Occt`.

## Consume

This identity is unreleased; generated OCCT wrappers reference the locally built project/package.
Applications acquire owners through generated factories. Opt into
[TedToolkit.CppBindings.Analyzers](../../../tools/TedToolkit.CppBindings.Analyzers/README.md) directly;
this Runtime does not embed or transitively deliver analyzer assets.

## Native ownership and borrowing

- `IStandard_Transient` marks an exact-layout unmanaged transient projection.
- `Handle<T>` owns one already-acquired intrusive reference. Its generated-only constructor adopts
  a non-null native address and matching non-throwing release callback without retaining it again.
  Assignment aliases the same owner; disposal/finalization calls the callback at most once.
- Lowercase `handle<T>` is pointer-sized non-owning storage matching native
  `opencascade::handle<T>`. Its only public member is non-owning `ref T Value`; it has no ownership,
  constructor, conversion, raw-pointer, retain, release, or disposal API.

`Handle<T>.Value` checks disposal; lowercase storage does not supply owner liveness checks.
Both references remain subject to the original C++ owner's lifetime and invalidation rules.
Neither access extends native lifetime, and neither permits use after or concurrently with disposal.
Keep the producing native module loaded until all owned callbacks have completed.

`Handle<T>` implements generic `ICppOwner<T>` for access metadata, but generated OCCT operations
use separate direct `Handle<T>` and `in handle<T>` receiver overloads, not a common interface
receiver. The lowercase layout does not implement `ICppOwner<T>`.

## Native errors

`NativeErrorProjection.ThrowIfFailed` is a thin generated-code facade over Shared projection. Shared
copies UTF-8 diagnostics, consumes the carrier exactly once, and owns common kinds 1 through 8 and
255. OCCT extends that contract only for local kind 9, which maps `Standard_Failure` to
`OcctFailureException`; ordinary `std::exception` remains Shared kind 8 and becomes
`NativeStandardException`.

`IOcctException` extends the common `INativeException` diagnostic contract for OCCT-local failures.
Native stack text is distinct from managed `Exception.StackTrace`. Generated-only
projection/constructors are diagnostic hooks, not a security boundary or an alternative handwritten
calling convention.

## Verify locally

From the repository root:

```powershell
dotnet run --project tests/TedToolkit.CppBindings.Runtime.Tests/TedToolkit.CppBindings.Runtime.Tests.csproj -c Release -- --report-trx
dotnet build tests/TedToolkit.CppBindings.Runtime.NativeIntegration/TedToolkit.CppBindings.Runtime.NativeIntegration.csproj -c Release
```

The managed suite checks Runtime contracts; the native integration runner consumes the two libraries
from `tests/native/handle-fixtures` through that fixture's CMake/CTest gate. Native OCCT generated
binding proof remains a separate generator/Windows integration gate.

[Repository overview](../../../../README.md) · [Platform architecture](../../../../docs/architecture/cpp-bindings-platform.md)
