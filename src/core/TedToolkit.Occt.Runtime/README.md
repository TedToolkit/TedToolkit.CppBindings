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

Concrete library-provided `Occt*Exception` types are the sole public managed failure
classification. The native discriminator remains private to the interop transport, including for
reserved nonzero values, which project to `OcctUnknownException`. `OcctFailureException`,
`OcctStandardException`, and `OcctUnknownException` keep OCCT, standard C++, and unknown native
failures distinct. The common `IOcctException` diagnostics expose the optional native type name and
optional native stack text. Consumers can catch these types but cannot construct or derive them.
Native stack text is kept separate from the managed `Exception.StackTrace`.

Runtime exposes one ABI-name-neutral, generated-code-only exception bridge for independently
generated wrapper assemblies. Its public carrier is a direct sequential ABI record containing the
native discriminator and three diagnostic pointers; it declares no managed constructor, reset
method, properties, or other behavior. The public projection accepts the producing library's
unmanaged `cdecl` clear function pointer. On failure it copies available UTF-8 diagnostics, calls
that entry point exactly once, and then throws the mapped exception. A successful carrier returns
without cleanup. The bridge does not require friend access or a managed delegate and does not
provide generated imports, public invocation bodies, or native stack-capture setup.

`IStandard_Transient` marks exact-layout unmanaged projections without adding behavior or ownership.
`Handle<T>` is the shared sealed owner of one already-owned intrusive native reference. It exposes
only its public low-level constructor, non-owning `ref T Value`, and `Dispose()`. The constructor
accepts the target `T*` and its matching non-throwing `cdecl void(T*)` release function; disposal and
finalization atomically claim that target and call the function at most once. The Handle stores only
the C++-owned object's address; it does not contain or own the object's storage. No marshalling,
managed delegate, wrapper privilege, or generated Runtime source is involved.

`IOcctRaii` is the empty representation marker for generated exact-layout non-transient RAII
structs. `Owned<T> where T : unmanaged, IOcctRaii` is their separate sealed reference owner. It
contains one private `T` field as the object's managed storage and exposes only the generated-only
`Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)` constructor, non-owning `ref T Value`, and
`Dispose()`. The constructor validates the destructor before admitting the owner. Generator model
validation rejects a native type whose `alignof(T)` cannot be proved safe for the direct managed
field representation; Runtime does not accept or validate an alignment value.
Generated factories placement-construct the field under `fixed`, suppress finalization on every
failed construction path, and keep the owner alive through native use. Disposal and finalization
invoke the matching non-throwing C++ destructor once; neither intrusive `Release` nor native storage
free is involved.

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
constraints express inheritance and generated operations use Handle extension methods. The raw
Handle constructor carries `GeneratedCodeOnlyAttribute`; handwritten construction reports
`TTOCCT001`, while source-generator output and files recognized as generated by Roslyn may use it.
The diagnostic is suppressible compiler guidance and does not replace constructor validation.
Runtime does not introduce descriptor classes, a covariant `Handle<out T>` interface, or
non-transient Handle use.
The Runtime NuGet package embeds its internal Runtime analyzer for direct and transitive consumers
as a compiler-only asset. It is neither published as a separate package nor loaded while Runtime
itself is compiled, and the analyzer assembly is not copied to application output.
The transient and non-transient owners have no public inheritance relationship, conversion, or
common public owner base. Runtime may still reuse declaration-agnostic lifetime machinery
internally.

Generated code supplies concrete supported-type provenance and target-specific retain/release or
cleanup capabilities to these shared mechanisms. Runtime does not maintain another declaration
catalog, operation registry, native symbol list, or generated-set contract identity.
Generated exact-match initialization authenticates the native artifact, publishes its validated
function table, and keeps that module loaded for the process lifetime. `Handle<T>` and `Owned<T>`
validate only the pointer, cleanup function, and owner state available to them; they cannot infer a
cleanup function's module origin from its address and do not own module leases.

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
