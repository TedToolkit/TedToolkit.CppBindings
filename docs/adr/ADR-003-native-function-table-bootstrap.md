# ADR-003: Bootstrap an inseparable native function table

- Status: Accepted
- Date: 2026-09-05
- Decision owner: TedToolkit maintainers
- Decision scope: Generated native-module loading, function-slot storage, dispatch, and finalizable
  owner cleanup for the supported .NET and `win-x64` binding artifact.
- Applicable product intent: None
- Applicable principles: [GEN-01 through GEN-04](../principles/README.md)
- Supersedes: [ADR-001](ADR-001-native-release-binding/README.md)
- Superseded by: None
- Approval source: On 2026-09-05, the maintainer explicitly approved preserving the verified
  `NativeLibrary` and native pointer-table direction and authorized this replacement ADR.

## Decision at a glance

Load the package-owned native module with `NativeLibrary`, resolve one bootstrap export, and retain
its immutable process-lifetime native function table as the generated binding's deterministic slot
inventory, while each finalizable owner stores its exact cleanup pointer.

## Context and decision question

ADR-001 selected Win32 loader imports and a managed `IntPtr[]` because the Runtime had to support
`netstandard2.0` and .NET Framework. The maintained platform now targets modern .NET, and the
generated boundary has one native `NativeApi_GetFunctionTable` bootstrap that returns the ordered
native pointer table emitted from the same completed generation plan as the managed slot access.

The decision is whether to restore ADR-001's loader and managed table solely to preserve that
historical implementation choice, or make the current modern-.NET, native-table boundary the
enduring authority. This record does not decide independent native replacement, module unloading,
or a versioned ABI.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Managed and native outputs are one exact-match, inseparable artifact set | [Generated binding architecture](../architecture/generated-binding-system.md) | Must |
| Hard constraint | Both sides derive identical deterministic slot order from one completed model | [GEN-01](../principles/README.md) | Must |
| Hard constraint | Cleanup remains usable during finalization and uses the originating module | [GEN-03](../principles/README.md) | Must |
| Hard constraint | Runtime remains declaration-agnostic and does not own generated slots or module identity | [GEN-04](../principles/README.md) | Must |
| Decision driver | Supported managed targets provide `NativeLibrary`; the old `netstandard2.0` loader constraint no longer applies | [Platform projects](../../src/core/) | High |
| Decision driver | Avoid a second per-export name inventory and publication step when the generated native table is already the compiled inventory | [Native table generator](../../src/core/TedToolkit.CppBindings.Generator/Generators/NativeFunctionTableGenerator.cs) | High |
| Decision driver | Do not claim a performance advantage that existing measurements do not establish | [ADR-001 benchmark evidence](ADR-001-native-release-binding/evidence/benchmark/function-table-report.md) | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Restore Win32 imports and a managed `IntPtr[]` | Documented by ADR-001 and its historical compatibility probe; high confidence | Partly | Preserves an obsolete target constraint but duplicates generated slot storage and per-export resolution | Rejected |
| Use `NativeLibrary` with the generated native pointer table | Current generated boundary and package-consumer proof; high confidence | Yes | Requires inseparable packaging because the table has no independent version or length negotiation | Selected |
| Emit one static P/Invoke per operation | Platform documentation; medium confidence | Partly | Avoids table indexing but duplicates declarations and cannot directly supply owner cleanup addresses | Rejected |
| Add a fingerprint, count, or version negotiation protocol | Architectural analysis; medium confidence | Not required | Could diagnose independent substitution, but creates a compatibility protocol for a boundary that explicitly forbids substitution | Rejected for the current boundary |

## Decision

The generated binding uses `NativeLibrary.Load` with its assembly context and package-owned native
library base name. It resolves only `NativeApi_GetFunctionTable`, calls that `cdecl` bootstrap once,
and retains the returned `nint*` for process lifetime. Generated operation and cleanup calls index
the immutable native table by deterministic slots and cast each address to its exact unmanaged
`cdecl` signature.

The table carries neither a length nor a fingerprint. The managed and native artifacts are built,
tested, versioned, and packaged together, and independent native substitution is unsupported. The
module is not unloaded. A factory copies its exact release or destructor pointer into every
finalizable `Handle<T>` or `Owned<T>`; owner cleanup never depends on a later table lookup or a
module lease.

This decision does not select the native table because it is faster. ADR-001's retained
measurements did not establish a stable dispatch advantage among pointer-table storage forms.

## Why this decision now

The modern .NET target removes the constraint that decided ADR-001's Win32 imports. The selected
direction follows GEN-01 because one generation plan defines both table entries and managed slot
use, GEN-03 because each owner retains its exact cleanup pointer, and GEN-04 because generated code
rather than Runtime owns the concrete module and slots. Restoring a managed table would add a
second storage and resolution path without satisfying a current compatibility requirement.

The no-fingerprint boundary is acceptable only because independent native replacement and module
unloading remain explicit non-features. A supported older managed target, separable native upgrade,
multiple module instance, or unload requirement invalidates that premise and requires a new
decision.

## Evidence and links

- [Generated managed loader](../../src/core/TedToolkit.CppBindings.Generator/Generators/NativeApiGenerator.cs)
- [Generated native table](../../src/core/TedToolkit.CppBindings.Generator/Generators/NativeFunctionTableGenerator.cs)
- [Neutral package-consumer verification](../../Build/VerifyGeneratorPackage.ps1)
- [ADR-001 retained benchmark and compatibility evidence](ADR-001-native-release-binding/evidence/README.md)

## Consequences and accepted trade-offs

- Initialization resolves one named bootstrap rather than every operation export by name.
- Generated dispatch reads native pointer storage directly and creates no managed slot array.
- A mismatched or truncated substituted native table is not diagnosed by per-slot validation. Such
  substitution is outside the supported package boundary; packaging and integration proof must
  keep the pair exact.
- The module and table consume process-lifetime resources and cannot be unloaded or replaced.
- Finalizable owners remain independent of table object lifetime because they retain exact cleanup
  pointers, but those pointers still require the originating module to stay loaded.
- ADR-001 and all of its benchmark and compatibility evidence remain historical records. Their
  measurements do not become evidence of a speedup for this decision.

## Downstream delivery constraints

- Emit the bootstrap, native table, and managed slot identities from one completed deterministic
  generation plan.
- Keep native and managed artifacts inseparable and reject claims of independent native upgrade,
  ABI compatibility, or module substitution.
- Resolve and publish the bootstrap result once; generated calls must not observe a partially
  initialized table.
- Invoke each slot only through its exact generated unmanaged `cdecl` signature.
- Copy release and destructor pointers into finalizable owners at successful construction; Runtime
  owners must not retain a generated table, table index, or module lease.
- Keep the native module loaded for as long as any generated call or finalizable owner can use one
  of its addresses.
- Preserve same-library allocation, destruction, error cleanup, and intrusive release.

## Exit requirements

A replacement must preserve deterministic cross-language slot identity, exact-signature dispatch,
same-library cleanup, and a safe lifetime for every retained function pointer. If native and
managed artifacts become independently replaceable, the replacement must add an explicit
compatibility and completeness boundary before permitting substitution.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Keep current architecture and consumer guidance aligned with this decision | TedToolkit maintainers | Before completing the active repository migration | Complete |
| Reassess compatibility protocol | TedToolkit maintainers | Native and managed artifacts need independent versioning or replacement | Open |
| Reassess module lifetime | TedToolkit maintainers | Unload, reload, or multiple module instances become required | Open |
| Reassess loader portability | TedToolkit maintainers | A supported managed target lacks `NativeLibrary` or a non-Windows binding is delivered | Open |
