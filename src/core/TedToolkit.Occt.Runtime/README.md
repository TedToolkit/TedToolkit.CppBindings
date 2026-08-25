# TedToolkit.Occt.Runtime

`TedToolkit.Occt.Runtime` currently contains the managed metadata contract used by generated OCCT
types. The pre-version raw-pointer ownership helpers, unversioned exception bridge, and handwritten
P/Invoke prototype have been removed.

## Current surface

`NativeTypeNameAttribute` preserves the canonical source OCCT spelling on generated types, fields,
parameters, and return values. It is metadata only and does not define the native ABI.

The runtime does not yet provide production ABI-major-1 imports, public invocation bodies, error
projection, or lifetime abstractions. Those components must consume the canonical
`ted_toolkit_occt_v1.h` contract and release native-owned values through the same versioned library
that allocated or retained them.

## Compatibility

The target frameworks come from `AlmostAllFrameworks.props`:

- .NET 6, 7, 8, 9, and 10;
- .NET Framework 4.7.2 and 4.8; and
- .NET Standard 2.0 and 2.1.

The native library remains platform- and architecture-specific. The current verified ABI matrix is
documented in [C interoperability ABI major 1](../../../docs/interop-abi-v1.md).

## Verification

Build the runtime from the repository root:

```powershell
dotnet build src/core/TedToolkit.Occt.Runtime/TedToolkit.Occt.Runtime.csproj -c Release
```

Run its TUnit project after the build preparation step has generated the triplet-specific
`InternalsVisibleTo` declarations:

```powershell
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj `
  -c Release --no-build -- --report-trx
```

The ABI-major-1 managed boundary proof remains a deliberately minimal fixture in
`AbiV1ManagedBoundaryTests`; it is not Runtime production code.

## Related documentation

- [Repository overview](../../../README.md)
- [Generator design](../TedToolkit.Occt.Generator/README.md)
- [C interoperability ABI major 1](../../../docs/interop-abi-v1.md)
