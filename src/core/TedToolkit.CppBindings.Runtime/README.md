# TedToolkit.CppBindings.Runtime

Provider-neutral .NET 8 ownership and metadata contracts for generated C++ bindings. This package
contains no native library, declaration catalog, OCCT dependency, or embedded analyzer.

## Consume

These unreleased identities are built locally; no public feed availability is claimed.
Generated wrapper projects reference this project/package. Handwritten applications normally acquire
owners through their generated library's factories, not the generated-only constructors.

Opt into [TedToolkit.CppBindings.Analyzers](../../tools/TedToolkit.CppBindings.Analyzers/README.md)
directly with `PrivateAssets="all"`; diagnostics do not flow through Runtime.

## Capabilities

- `Owned<T>`, constrained by unmanaged `ICppRaii`, contains one native RAII value directly in managed
  storage. Generated code placement-constructs that value and supplies its matching non-throwing
  `cdecl void(T*)` destructor. Disposal/finalization invokes it at most once; it does not free
  native storage or perform intrusive reference counting.
- `ICppOwner<T>` exposes only non-owning `ref T Value`. It identifies the access contract for caller
  guidance and diagnostics; it supplies no acquisition, conversion, disposal, or shared generated
  handle-receiver API.
- `ICppRaii` marks an eligible exact-layout unmanaged RAII representation; it owns no object.
- `NativeTypeNameAttribute` preserves native type spelling as metadata.
- `GeneratedCodeOnlyAttribute` identifies wrapper implementation hooks. It is suppressible
  compiler guidance, not an authorization boundary.
- `NativeError` is the generated-only sequential carrier: one integer kind and three native
  diagnostic pointers. Category interpretation and cleanup belong to the producing provider.

All public types above use the `TedToolkit.CppBindings` namespace. OCCT intrusive handles and
exception interpretation belong to [OCCT Runtime](../TedToolkit.CppBindings.Occt.Runtime/README.md).

## Lifetime and compatibility

Assigning an `Owned<T>` aliases the same owner; it never copies or clones the native object.
Its returned reference remains subject to the native owner's lifetime and invalidation rules.
Do not retain it past disposal or overlap access with disposal. Generated native calls pin the
contained storage and keep the owner alive. Failed construction must suppress finalization.

The generator must prove layout/alignment and supply matching native construction/destruction.
Runtime cannot infer a callback's module origin; its module must remain loaded through cleanup.
There are no generated declarations, native imports, implicit borrowed-value copies, public
`Borrowed<T>` wrappers, or module leases in this package.

## Verify locally

From the repository root:

```powershell
dotnet run --project tests/TedToolkit.CppBindings.Runtime.Tests/TedToolkit.CppBindings.Runtime.Tests.csproj -c Release -- --report-trx
```

The existing small Runtime suite covers both generic contracts and the separate OCCT Runtime;
cross-wrapper/native fixtures remain separate consumers.

[Repository overview](../../../README.md) · [Platform architecture](../../../docs/architecture/cpp-bindings-platform.md)
