# Generated binding system architecture

- Status: Active
- Owner: TedToolkit.Occt maintainers
- Scope and system boundary: Generation, native interop, managed representation, ownership,
  diagnostics, and packaging for generated OCCT bindings.
- Applicable product intent: None
- Governing principles: [Repository design principles](../principles/README.md)
- Last approved revision: Uncommitted working tree approved by the maintainer on 2026-08-26.

## Current architecture

### Dependency direction

```text
configured OCCT headers and target toolchain
                  |
                  v
         normalized semantic Model
            /                 \
           v                   v
generated C/C++ boundary   generated C# binding
           \                   /
            v                 v
          exact-match generated artifact set
                         |
                         v
     platform binding package (initially Windows win-x64)

generated C# binding --> TedToolkit.Occt.Runtime
consumer compilation --> TedToolkit.Occt.Runtime.Analyzers (planned)
```

The normalized Model is the only declaration authority. C# and C++ emitters consume it
independently; neither emitter reads or repairs the other's output. Generated source is never
hand-edited. Source-only generation may stop after emission, while ready-to-use packaging compiles
and verifies the emitted native project.

`TedToolkit.Occt.Runtime` contains only handwritten, declaration-agnostic managed mechanisms.
Concrete OCCT layouts, imports, exports, operation bodies, closed-generic registrations, release
functions, and expected native fingerprints belong to generated wrapper assemblies such as
`TedToolkit.Occt.Windows`. A wrapper uses Runtime's ordinary public API. Runtime grants no wrapper
friend access, caller identity privilege, `InternalsVisibleTo`, or Windows-specific capability.

### Native boundary and exact matching

Generated exports use C linkage, C11-compatible declarations, and `cdecl`. C++ types, references,
templates, exceptions, and allocator obligations never appear directly in the C declaration
surface. Every generated callable OCCT operation export uses `cdecl` and returns the private
C-compatible native error carrier by value, including operations whose source C++ declaration is
`noexcept`. A non-`void` source return is written to a required result output slot; a source `void`
operation has no result slot. The adapter itself is non-throwing, validates its transports, catches
every native exception, and returns either success or managed-projectable failure data.

Release and cleanup exports are the deliberate exception to the error-return rule. Handle release,
Owned destruction and storage release, native error clearing, and any other same-library cleanup
entry point use `cdecl void(...)`, are non-throwing, and return no error carrier. Their callers must
be able to run them during disposal or finalization without creating a second failure path.

The C boundary is monomorphic. One canonical operation produces one export for each distinct
native call shape. An additional type-specific export is generated only when a registered closed
C++ template specialization, concrete pointer adjustment, value ABI, or implementation actually
changes that call shape. Merely exposing a managed generic or accepting another derived type does
not create a Cartesian product of exports. Base conversion adjusts a pointer into the same proved
native object storage; it does not copy fields or map the object into another representation.

The native and managed outputs form one unversioned, inseparable artifact set. A generated
fingerprint records every layout- and lifetime-relevant input, including OCCT input identity,
compiler ABI, architecture, packing, closed specialization, native dependency, and cleanup
contract. Managed initialization rejects a missing or different fingerprint before resolving an
OCCT operation. The boundary does not promise independent native upgrades or major/minor ABI
compatibility.

Every allocation is destroyed and freed by the same native artifact that created it. Native
cleanup exports are non-throwing. Managed code invokes generated unmanaged function pointers
directly; it does not use `Marshal`, managed delegates, or declaration-specific Runtime imports.
The originating native module must remain loaded while any related owner is alive.

### Managed object model

Every supported C++ object has an exact unmanaged C# struct derived from the pinned compiler's
complete object layout. The generated representation uses `LayoutKind.Sequential`, typed fields,
explicit private padding, and alignment-preserving opaque storage. It does not use managed
`BaseType` fields, `FieldOffsetAttribute`, or a universal `Pack = 1`. Unsupported or unproved
layouts fail generation.

C++ inheritance is represented by generated C# interfaces. Generated instance operations are
extension methods over their semantic receiver: `in T` for const values, `ref T` for mutable
values, `Handle<T>` for transient objects, and `Owned<T>` for non-transient RAII objects. Exact
layout structs contain representation only and never implement `IDisposable` or declare lifetime
operations.

Generated public managed operations preserve the recognizable source C++ operation shape: operation
name, static or instance role, parameter order and meaning, const or mutable receiver semantics,
and projected business return type. Interop-only details do not appear in that public shape. In
particular, the native error return and result output slot remain private generated invocation
details, while C++ object parameters and returns use their approved exact-layout `struct`,
`Handle<T>`, or `Owned<T>` projection. Generic object operations constrain `T` with `unmanaged` and
the generated inheritance or lifetime interface required by the source declaration, so invocation
does not box or copy an object merely to satisfy a base operation.

The ownership categories are:

- Proved trivial native values are ordinary copyable structs and require no disposal.
- A `Standard_Transient` exact-layout struct is owned by Runtime's sealed invariant
  `Handle<T>`. Its only declared public members are
  `Handle(T* value, delegate* unmanaged[Cdecl]<T*, void> release)`, `ref T Value`, and
  `Dispose()`. Construction adopts one already-owned intrusive reference without retaining it.
  Disposal atomically detaches the managed owner slot and calls the matching non-throwing native
  `void(T*)` release export at most once. `Value` is a non-owning direct data view, throws after
  disposal, does not extend lifetime, and must not overlap disposal. A reference already returned
  cannot be revoked. Normal OCCT operations use Handle extension methods rather than `Value`.
- A supported non-transient object with native RAII state is owned by a separate sealed invariant
  `Owned<T>`. It owns stable, correctly aligned native storage, performs construction, destruction,
  and storage release through the originating native artifact, and creates a distinct object only
  through an explicit generated clone or copy operation. Its public `ref T Value` is the same simple
  non-owning data view: it throws when the owner is already disposed, does not extend lifetime, and
  must not overlap disposal. `Owned<T>` and `Handle<T>` have no public common owner base,
  inheritance, or conversion. Shared declaration-agnostic lifetime machinery may remain private to
  Runtime.

Generated operations may use an owner receiver's `Value` internally to obtain the address passed to
native code. Immediately after the last unmanaged use, and before managed error projection can
throw, generated code calls `GC.KeepAlive(owner)` for every finalizable owner involved in that use.
This establishes managed-owner liveness across the native call without a callback, invocation lease,
allocation, or additional public owner API. It prevents premature finalization only; it neither
synchronizes nor makes explicit concurrent `Dispose()` safe. Direct `Value` consumers likewise own
the obligation to keep the owner alive and prevent disposal for the complete native-reference use.
This requirement follows the documented [.NET `GC.KeepAlive` lifetime
contract](https://learn.microsoft.com/dotnet/api/system.gc.keepalive): an unmanaged pointer does not
itself keep its managed owner reachable, and the JIT may otherwise shorten that owner's lifetime.

### Managed failures and diagnostics

Native failures are caught before crossing the boundary and projected to stable managed exception
categories. Diagnostic strings remain native-owned only until the managed projection copies them;
the matching native artifact releases their storage. No last-error global or thread-local state is
part of the contract.

Every generated managed OCCT operation consumes its private native error return. Success continues
with the projected C++ result; any nonzero or unrecognized error kind consumes the matching
same-library diagnostic owner and throws the approved managed exception. The generated public API
does not expose the native error carrier and does not add a parallel `Try` or error-returning
surface. Release and cleanup remain non-throwing `void` paths and therefore never participate in
managed error projection.

The existing `TedToolkit.Occt.Analyzer` remains an OCCT-header source generator. Planned compiler
guardrails belong to a separate `TedToolkit.Occt.Runtime.Analyzers` compiler asset. It reports both
handwritten use of unavoidable public Runtime hooks reserved for generated implementations and the
supported suspicious lifetime uses of non-owning `Handle<T>.Value` or `Owned<T>.Value` references.
The latter includes recognizable escape, suspension, temporary-owner, post-disposal, and missing
owner-keepalive patterns. The analyzer remains declaration-agnostic and does not attempt complete
alias, concurrency, or lifetime proof. The detailed boundary is recorded in the active
[Runtime analyzer architecture](runtime-analyzer-boundary.md).

The analyzer is suppressible developer guidance, not authorization. Independently generated
wrappers use the same public Runtime contracts and generated-code convention without assembly-name
privilege or `InternalsVisibleTo`. Runtime validation remains authoritative for conditions observable
when access is obtained, but it cannot make an already-returned `Value` reference safe; an absent,
suppressed, or bypassed lifetime diagnostic leaves that risk with the caller.

### Package and target boundary

Runtime, generated managed bindings, their managed verification projects, and the initial consumer
package compile only for `net8.0`. Generator tooling may use its own build target.

The first ready-to-use binding package and managed assembly are named
`TedToolkit.Occt.Windows`; generated APIs retain the default `TedToolkit.Occt` namespace. Its first
and only current support matrix is the independently proved `win-x64` artifact set. The package
ships the complete native runtime closure and requires no consumer-side OCCT, vcpkg, Clang, CMake,
or Generator installation. Another OS, architecture, compiler ABI, or RID requires a separately
generated and proved platform binding artifact; replacing only the native asset is invalid.

## Constraints for change design

- Fail closed before emitting or invoking any declaration whose transport, layout, ownership,
  exception, or cleanup semantics are incomplete.
- Keep the Model as the single semantic authority and derive every paired native and managed
  operation deterministically.
- Use `cdecl` for every generated export. Give every callable OCCT operation a private native error
  return and transport its source result through a result slot; keep release and cleanup exports
  non-throwing `void` functions.
- Preserve the source C++ operation's recognizable business shape in the generated public managed
  API. Keep error carriers, result slots, pointer adjustment, and cleanup mechanics private.
- Generate type-specific native operation exports only for distinct proved native call shapes;
  never expand every managed generic and derived-type combination by default or copy an object to
  perform a base conversion.
- Prove complete native and managed layout equality for every shipped type and supported closed
  generic specialization on its exact target matrix.
- Keep layout structs non-owning and non-disposable. Keep transient and non-transient ownership in
  their distinct Runtime reference owners.
- Preserve `Handle<T>`'s exact three-member public surface and direct `T*` release contract unless
  this architecture is explicitly revised first.
- Expose only a simple non-owning `ref T Value` data view from each owner. Do not add a callback,
  invocation lease, `DangerousValue`, or a second address-access API to make direct access appear
  lifetime-safe.
- Keep each finalizable owner alive through every generated unmanaged use with `GC.KeepAlive`
  immediately after the last such use and before error projection. Do not claim this protects
  against explicit concurrent disposal.
- Keep Runtime declaration-agnostic and wrappers equally capable through public API only.
- Contain all native exceptions and perform every cleanup through the originating native artifact.
- Use Runtime analyzers for unavoidable public generated-only hooks and best-effort `Value` lifetime
  diagnostics. Keep layout validation, owner-state checks, native safety, and behavior expressible
  by API shape in their owning compiler or runtime layers; analyzer suppression transfers the
  low-level lifetime risk to the caller.
- Do not claim package coverage, RID support, independent native compatibility, or publication
  without corresponding generated and integration evidence.

## Decision links and exceptions

This record is the current architecture authority. The repository intentionally retains no
separate historical decision-log set while it is unpublished. A proposed exception to a Required principle or to this architecture
must update the affected current-truth document and receive explicit maintainer approval before
implementation; a change record alone cannot redefine the architecture.

## Review triggers

Reassess this architecture when any of the following occurs:

- a second platform, architecture, compiler ABI, RID, or managed target is proposed;
- native and managed artifacts need independent versioning or upgrade compatibility;
- an exact native layout cannot be represented or proved with the selected CLR strategy;
- a new ownership category, public pointer surface, callback, or module-unloading model is needed;
- owner finalization is removed, generated calls cannot place `GC.KeepAlive` after their last
  unmanaged use, or direct `Value` access is expected to become safe during concurrent disposal;
- Runtime needs declaration-specific knowledge or a wrapper requests privileged access;
- generated output requires handwritten declaration-specific code or a second semantic authority;
- a public non-throwing, `Try`, native-error-returning, or otherwise non-C++-shaped managed operation
  surface is proposed;
- an operation would omit the native error return, a cleanup path would return an error, or eager
  per-derived-type export expansion is required;
- analyzer enforcement is proposed as a security or runtime correctness boundary; or
- the first public package baseline is prepared for publication.
