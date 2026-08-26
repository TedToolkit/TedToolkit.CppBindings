# TedToolkit.Occt.Runtime

`TedToolkit.Occt.Runtime` contains the handwritten managed contracts shared by generated OCCT
bindings. It provides native-name metadata, managed exception projection, and the common transient
owner; it does not contain generated operation imports. Generated managed libraries depend on
Runtime, but Runtime depends on neither the Generator nor a generated binding assembly.

## Admission boundary

Runtime is intentionally small. A new Runtime mechanism must be necessary for shared correctness or
a stable public contract, reusable across generated declaration sets, independent of concrete OCCT
declarations and symbols, and more appropriate to centralize than to generate.

Declaration-specific structs, interfaces, extensions, imports, native symbols, layouts,
closed-generic registrations, expected fingerprints, pointer adjustments, and type-specific
construction or cleanup adapters remain in generated output. Runtime may provide only their
declaration-agnostic metadata, exception, loading, leasing, module-lifetime, and ownership
mechanisms.

## Current surface

`NativeTypeNameAttribute` preserves the canonical source OCCT spelling on generated types, fields,
parameters, and return values. It is metadata only and does not define the native ABI.

`OcctErrorKind` preserves every native error category, including reserved nonzero values.
Library-provided `Occt*Exception` types combine familiar .NET catch behavior with the common
`IOcctException` diagnostics: the exact error kind, optional native type name, and optional native
stack text. Consumers can catch these types but cannot construct or derive them. Native stack text
is kept separate from the managed `Exception.StackTrace`.

Runtime has one internal, ABI-name-neutral exception projection path. On failure it copies available
UTF-8 diagnostics, calls the supplied clear entry point exactly once, and then throws the mapped
exception. A successful carrier returns without cleanup. Generated binding integration remains with
its owning change and must use ordinary public Runtime contracts rather than friend access. Runtime
does not provide generated imports, public invocation bodies, or native stack-capture setup.

`IStandard_Transient` marks exact-layout unmanaged projections without adding behavior or ownership.
`Handle<T>` is the shared sealed owner of one already-owned intrusive native reference. It exposes
only its public low-level constructor, non-owning `ref T Value`, and `Dispose()`. The constructor
accepts the target `T*` and its matching non-throwing `cdecl void(T*)` release function; disposal and
finalization atomically claim that target and call the function at most once. No marshalling,
managed delegate, wrapper privilege, or generated Runtime source is involved.

## Compatibility

Runtime and its managed tests target only .NET 8.

The native library remains platform- and architecture-specific. Generated managed and native
artifacts must be an exact matched set. The first public binding package and assembly are
`TedToolkit.Occt.Windows`, initially proved only for `win-x64`.

Runtime itself remains platform-family neutral because it owns no concrete native layout, import,
symbol, specialization, or generated-set identity. A platform-bound generated assembly supplies
those facts through Runtime's declaration-agnostic mechanisms.

Every OCCT C++ target, including `Standard_Transient` descendants, remains an exact-layout generated
struct. Runtime provides `Handle<T>` only for transient intrusive ownership; generated interface
constraints express inheritance and generated operations use Handle extension methods. The planned
Runtime analyzer may later reserve handwritten constructor use; that compiler guidance is not part
of the Handle lifetime implementation. Runtime does not introduce descriptor classes, a covariant
`Handle<out T>` interface, or non-transient Handle use.
Non-transient RAII ownership uses the separate sealed reference-type `Owned<T>`. The two owners have
no public inheritance relationship or common public owner base, while Runtime may reuse
declaration-agnostic lifetime machinery internally.

Generated code supplies concrete supported-type provenance and target-specific retain/release or
cleanup capabilities to these shared mechanisms. Runtime does not maintain another declaration
catalog, operation registry, native symbol list, or generated-set contract identity.

## Verification

Build the runtime from the repository root:

```powershell
dotnet build src/core/TedToolkit.Occt.Runtime/TedToolkit.Occt.Runtime.csproj -c Release
```

Run its TUnit project after the build preparation step:

```powershell
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj `
  -c Release --no-build -- --report-trx
```

Build and run the two real C++ release-export fixtures with a configured C++ toolchain:

```powershell
cmake -S tests/native/handle-fixtures -B out/build/handle-fixtures
cmake --build out/build/handle-fixtures --config Release
ctest --test-dir out/build/handle-fixtures -C Release --output-on-failure
```

The legacy ABI-major-1 managed boundary proof remains a deliberately minimal fixture in
`AbiV1ManagedBoundaryTests`; it is not Runtime production code. Runtime exception behavior is
covered by the Runtime TUnit project.

## Related documentation

- [Repository overview](../../../README.md)
- [Generator design](../TedToolkit.Occt.Generator/README.md)
- [Generated binding architecture](../../../docs/architecture/generated-binding-system.md)
- [Repository design principles](../../../docs/principles/README.md)
