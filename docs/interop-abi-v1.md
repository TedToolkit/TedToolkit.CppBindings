# C interoperability ABI major 1

> Historical migration fixture: the current architecture replaced this versioned protocol with one
> unversioned exact-match generated boundary and replaced its semantic-value and opaque-object
> projection with exact native-layout structs and separate owners. This document describes the
> retained ABI-v1 verification fixture only; it is not current product architecture or a public
> compatibility commitment.

TedToolkit.Occt defines its native interoperability boundary through the generated
`ted_toolkit_occt_v1.h` header. The header is the ABI authority; source C++ spellings, generated C++
implementation text, P/Invoke declarations, and public C# names do not define the protocol.

## Current delivery state

The generator materializes the canonical ABI-major-1 header and a CMake project whose internal
target is `ted_toolkit_occt_abi_v1`. Its artifact basename defaults to `ted_toolkit_occt` and may be
configured independently. The pre-version record-by-record C++ wrapper generator,
unversioned exception bridge, and managed raw-pointer prototypes have been removed. Raw C++
references, templates, STL types, and OCCT handles cannot become active exports through an
alternate generation path.

The `ted-occt-abi-v1-consumer` presets build the generated project with OCCT 8.0.1 and execute a
plain C11 consumer. That consumer proves point values, transient ownership, UTF-8 storage, error
precedence, reserved failures, idempotent cleanup, and nonthrowing diagnostic-allocation failure.
The minimal P/Invoke boundary fixture loads that exact artifact, rejects an incompatible major
before resolving any operation export, compares managed layouts with native layout queries, and
proves representative success, reserved/unknown failures, optional diagnostics, and cleanup. This
is boundary proof, not the final generated managed invocation layer.

## Projection layers

Every candidate operation carries five independent projections:

1. source C++ type, used only for native adapter code and diagnostics;
2. versioned C ABI transport identifier and C11 spelling;
3. explicit C++ adapter target;
4. managed transport type used by P/Invoke; and
5. public managed type.

No layer falls back to another layer's spelling. An incomplete value rejects its complete operation
before symbol naming or declaration emission. The deterministic `TEDOCCTABI001` diagnostic records
the source declaration, location, value, source type, direction, ownership, and missing rule.

## Canonical surface

- Header: `ted_toolkit_occt_v1.h`
- Default native library basename: `ted_toolkit_occt` (configurable)
- Internal CMake target: `ted_toolkit_occt_abi_v1`
- Identifier prefix: `ted_occt_v1_`
- Macro prefix: `TED_OCCT_V1_`
- Version encoding: `(major << 16) | minor`; version 1.0 is `0x00010000`
- Operation symbol: `ted_occt_v1_<owner>_<operation>__<128-bit SHA-256 prefix>`

The canonical header includes fixed-width error kinds, the frozen `ted_occt_v1_pnt2d` semantic
value, a typed incomplete `ted_occt_v1_geom2d_cartesian_point` handle, and borrowed/owned byte
carriers. It includes only `<stdint.h>` and compiles as both strict C11 and C++ without OCCT include
paths.

## Supported verification matrix

ABI major 1 initially targets Windows x64, the MSVC x64 ABI, cdecl, and OCCT 8.0.1 installed by
vcpkg for `x64-windows`. Portable C declarations do not claim support for another architecture,
triplet, compiler ABI, or OCCT version.

## Ownership and rejection rules

Owned errors, buffers, ordinary objects, and transient handles must eventually be released by the
same ABI library that allocated or retained them. `Standard_Transient` handles use intrusive
retain/release and must never be directly deleted. Unsupported types are omitted as one complete
operation; the generator never emits a partial declaration or substitutes a C++ or C# spelling.

The current production direction is defined by the
[generated binding architecture](architecture/generated-binding-system.md).

## Native consumer proof

Build the Release solution first so the ABI materializer is available, then run:

```powershell
$env:VCPKG_ROOT = 'C:\vcpkg'
cmake --preset ted-occt-abi-v1-consumer
cmake --build --preset ted-occt-abi-v1-consumer
ctest --preset ted-occt-abi-v1-consumer --output-on-failure
```

Ninja and `clang-cl` must be on `PATH`. The generated header is under the preset build directory at
`generated/ted_toolkit_occt_v1.h`; the Windows library is
`abi-v1/ted_toolkit_occt.dll`. The boundary proof owns
`tests/native/abi-v1-consumer/ted_toolkit_occt_v1_test.h` and explicitly enables its fault hooks;
production generation emits neither that header nor those exports.

Run the managed proof against the built artifact with:

```powershell
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj `
  -c Release --no-build -- `
  --treenode-filter '/*/TedToolkit.Occt.Generator.Tests.Interop/AbiV1ManagedBoundaryTests/*' `
  --report-trx
```

Every nonzero error kind, including an unrecognized reserved value, is failure. Native-owned error
diagnostics and byte buffers are released only by cleanup exports from the loaded ABI library, and
repeated cleanup is valid only on the same authoritative slot after it has been cleared.
