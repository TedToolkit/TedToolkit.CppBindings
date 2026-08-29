# ADR-001: Load and Cache Generated Native Exports

- Status: Accepted
- Date: 2026-08-26
- Decision owner: TedToolkit.Occt maintainers
- Decision scope: Windows generated native-module loading, resolved-export storage, generated
  dispatch, and finalizable owner cleanup.
- Applicable product intent: None
- Applicable principles: [GEN-01 through GEN-04](../../principles/README.md)
- Supersedes: None
- Superseded by: None
- Approval source: Explicit maintainer approval of the simplified no-fingerprint, no-runtime-manifest
  generated `NativeApi` design in the Codex task on 2026-08-27.

## Decision at a glance

Windows generated bindings resolve required exports with the Win32 loader into one private static
managed function table, while each finalizable owner retains its exact module-specific cleanup
function pointer. The generated table itself is the compiled operation inventory; there is no
separate binding manifest, fingerprint bootstrap, or unloadable table object.

## Context

`Handle<T>` owns one intrusive native reference and must invoke the C ABI `Release` export during
`Dispose` or finalization. Its current shape stores a
`delegate* unmanaged[Cdecl]<T*, void>`. Generated bindings need a way to obtain that pointer while
supporting consumers of a `netstandard2.0` runtime assembly, including .NET Framework 4.8.1.

`System.Runtime.InteropServices.NativeLibrary` is a convenient loader on modern .NET, but its custom
loading APIs are not part of the `netstandard2.0` contract. Static `DllImport` is compatible, but a
generic `Handle<T>` cannot receive a raw native export address from a static P/Invoke declaration
without changing its cleanup representation to a managed delegate or moving cleanup into generated
owner types.

The decision therefore separates three concerns:

1. Load a native module and resolve an export once.
2. Store and dispatch the resolved addresses used by generated bindings.
3. Preserve the exact module-specific cleanup address for every finalizable owner.

## Decision

For generated Windows bindings, import `LoadLibraryW` and `GetProcAddress` from
`kernel32` using `DllImport`. Resolve all required native exports once, validate them before
publishing the binding, and cache their addresses in one generated, process-lifetime static
function table. Use a managed `IntPtr[]` for the initial implementation. Generated calls read a
constant slot and cast it directly to the exact unmanaged Cdecl function-pointer signature.

When generated code constructs a `Handle<T>` or `Owned<T>`, it reads the release or destructor
address from the table once and passes that typed pointer to the owner. The owner continues to store
the pointer itself. Finalization must not depend on a mutable global table, a table index, or a table
object owned by another assembly.

Do not convert the address to a managed delegate for the normal `Handle<T>` cleanup path. Do not
require `NativeLibrary` in the `netstandard2.0` runtime surface.

The generated binding layer owns module loading and the table. The module must remain loaded while
any generated call or finalizable owner can use an address from it. The initial implementation keeps
generated native modules and their tables for process lifetime; `FreeLibrary` and table replacement
are reserved for a future lifetime design that can prove no live or finalizable owner retains an
address from that module.

Load the package-owned native library from the generated assembly's resolved package location. The
initial contract intentionally omits a managed/native fingerprint and does not support substituting
another native build. A wrong or incomplete module fails when a required export cannot resolve; the
package's isolated-consumer proof, unique artifact ownership, and complete export resolution are the
accepted boundary. Adding independent native replacement later requires a new compatibility design.

All native cleanup exports must use an unmangled C ABI, the declared `cdecl` calling convention,
and signatures that exactly match the managed function-pointer type.

## Evidence

The compile probe targets `netstandard2.0` and successfully compiles both the `kernel32` imports and
the unmanaged Cdecl function-pointer cast. The same path executes successfully under .NET Framework
4.8.1. The benchmark also covers .NET 8.0.30, .NET 9.0.19, and .NET 10.0.11.

The primary loader result is that the loader does not determine steady-state call cost. Once
resolved, the `kernel32` and `NativeLibrary` candidates both call a cached unmanaged function
pointer:

| Runtime | Kernel32 pointer, lifecycle mean | NativeLibrary pointer, lifecycle mean | Difference |
|---|---:|---:|---:|
| .NET 8 | 33.13 ns | 31.74 ns | +4.4% |
| .NET 9 | 33.24 ns | 33.00 ns | +0.7% |
| .NET 10 confirmation | 36.44 ns | 37.16 ns | -1.9% |

The .NET 10 confirmation used three launches, five warmups, and fifteen measured iterations because
the initial full run showed thermal drift. Its distributions overlap. No measured function-pointer
path allocated managed memory per operation.

Converting a `GetProcAddress` result to a managed delegate was consistently slower. Its pure
invocation mean was 9.16 ns, 9.26 ns, and 7.11 ns on .NET 8/9/10, compared with 3.17 ns, 2.54 ns,
and 2.35 ns for the same address called as an unmanaged function pointer. On .NET Framework 4.8.1,
the corresponding means were 14.51 ns and 4.08 ns.

Repeated load-resolve-free cost was similar for the two loaders on modern .NET: 248.2 vs 244.6 us
on .NET 8, 249.7 vs 235.5 us on .NET 9, and 224.8 vs 230.1 us on .NET 10 (kernel32 vs
`NativeLibrary`). This is an initialization diagnostic, not a per-owner cost.

The function-table extension compared an instance field, a static typed field, managed and
unmanaged indexed tables, and a Hexa-style unmanaged table on .NET Framework 4.8.1 and .NET
8/9/10. Every dispatch path was allocation-free and in the same few-nanosecond range, but rankings
reversed between runtimes and showed frequency drift. There is no evidence that indexing a table is
inherently faster than calling the instance pointer.

For a finalizable Handle-shaped owner, removing the per-instance cleanup pointer reduced measured
allocation from 32 B to 24 B only when cleanup relied on one process-global table. Preserving module
identity by storing a table reference and index increased it to 40 B. This rejects table lookup from
generic owner finalization. The static table remains useful for centralized resolution, validation,
and generated dispatch—not as an owner-memory optimization.

See the [evidence index](evidence/README.md), [benchmark report](evidence/benchmark/report.md),
[function-table report](evidence/benchmark/function-table-report.md), and
[evaluation matrix](evidence/evaluation-matrix.md).

## Alternatives considered

### `NativeLibrary` plus unmanaged function pointer

This is the modern baseline and has equivalent hot-path behavior, but it cannot be the only loader
in a `netstandard2.0` assembly. Multi-targeting solely to select a different loader would add product
and test complexity without a demonstrated steady-state benefit.

### `kernel32` plus marshaled delegate

This compiles for `netstandard2.0`, but adds a reverse interop delegate thunk to every cleanup and
requires changing `Handle<T>`'s storage. It is retained only as an experimental comparison.

### Static `DllImport` for each release export

Direct static P/Invoke is fast and compatible, but one declaration per export bypasses the selected
single validation and publication boundary. It also cannot directly supply the raw native address
stored by the current owner architecture; using a cached managed delegate wrapper reintroduces an
extra managed call. It is not selected for the generated binding set.

### Hexa-style unmanaged function table used directly by owners

Hexa.NET.ImGui demonstrates a generated static `FunctionTable` whose unmanaged `void**` storage is
indexed at each call. This is a valid generated-dispatch pattern, but it does not make the native
`calli` itself faster in these measurements. Its unmanaged storage needs explicit lifetime
management and moves the slot storage outside the managed allocation counter rather than eliminating
it. More importantly, a generic finalizable owner must either assume one mutable process-global
module or retain a table reference and slot. The first weakens module correctness; the second costs
more memory than the current pointer.

### One static typed field per export

This has the same dispatch mechanism and avoids array indexing, but produces one generated field per
export. A table gives one validation and publication boundary and scales better when a generated
module has hundreds or thousands of exports. The benchmark found no stable hot-path advantage for
either storage form.

## Consequences

- `Handle<T>` keeps its unmanaged Cdecl function-pointer representation across target frameworks.
- `Owned<T>` likewise keeps its module-specific destructor pointer; it invokes the destructor but
  does not perform intrusive `Release` or free its managed storage.
- Generated Windows bindings gain a small loader abstraction, an indexed export table, and explicit
  Win32 error handling.
- The managed table costs approximately `8 * slotCount` bytes plus one array header on x64. The
  1,536-slot benchmark allocated 12,312 B on modern .NET and 12,336 B on .NET Framework, once.
- Native module lifetime becomes a documented invariant rather than an implicit assumption.
- Cleanup and table dispatch have no per-call managed allocation. The measured dispatch forms stay
  in the same few-nanosecond range, with no stable cross-runtime speed advantage for table indexing.
- This loader is Windows-specific. A future non-Windows target needs an equivalent `dlopen`/`dlsym`
  binding behind the generated loader boundary, without changing `Handle<T>`.
- `FreeLibrary` must not run while any owner can still invoke an export from the module.

## Downstream delivery constraints

- The generated binding, not the platform-neutral Runtime, owns the concrete loader imports, module
  identity, export names, slot identities, and table.
- Required exports are validated privately and the table is published only when complete; generated
  calls never observe partial initialization.
- Function-table slots are deterministic generated identities derived from the same semantic model
  as their native exports. The table is the compiled inventory; no separate binding manifest is
  loaded at runtime.
- Every slot is invoked only through its exact generated unmanaged Cdecl signature.
- Owner factories copy the exact cleanup address into `Handle<T>` or `Owned<T>`; generic Runtime
  owners never depend on a generated table, mutable global slot, or table index.
- Generated owner calls continue to stabilize `owner.Value` with `fixed` and retain the finalizable
  owner with `GC.KeepAlive(owner)` after its last unmanaged use.

Microsoft documents `DllImport` as the pre-.NET 7 P/Invoke mechanism and recommends matching native
signatures exactly. It also documents that custom `NativeLibrary` import resolution is available
only on .NET Core 3.1 and .NET 5+:

- <https://learn.microsoft.com/dotnet/standard/native-interop/best-practices>
- <https://learn.microsoft.com/dotnet/standard/native-interop/native-library-loading>
- <https://learn.microsoft.com/dotnet/standard/net-standard>

## Follow-ups and review triggers

Acceptance approves the direction; it does not claim that the Runtime, Generator, or generated
packages already implement it. Delivery remains owned by the active generated-binding change.

Reassess this decision if generated modules must unload, multiple replaceable module instances must
coexist behind one generated binding assembly, a supported platform is not served by the Win32
loader boundary, managed function-table storage creates a demonstrated material GC cost, or a
representative benchmark shows a sustained regression beyond the declared 5% dispatch threshold.
