# Generated binding system architecture

- Status: Active
- Owner: TedToolkit.Occt maintainers
- Scope and system boundary: Generation, native interop, managed representation, ownership,
  diagnostics, and packaging for generated OCCT bindings.
- Applicable product intent: None
- Governing principles: [Repository design principles](../principles/README.md)
- Governing platform boundary: [C++ bindings platform architecture](cpp-bindings-platform.md)
- Related ADR: [ADR-002](../adr/ADR-002-cpp-bindings-platform.md)
- Last approved revision: Uncommitted working tree approved by the maintainer on 2026-08-26;
  declaration-level alignment admission clarified and approved on 2026-09-04 with "是的，继续。";
  cyclic handle field reference projection approved on 2026-09-04 with "同意。";
  ordinary overlapping-field reference projection approved on 2026-09-05 with "批准。".

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
consumer compilation --> Runtime package embedded analyzer
```

The normalized Model is the only declaration authority. C# and C++ emitters consume it
independently; neither emitter reads or repairs the other's output. Generated source is never
hand-edited. Source-only generation may stop after emission, while ready-to-use packaging compiles
and verifies the emitted native project.

Every declaration-specific artifact derives from the completed Model, including layouts,
ownership categories and operation lifetime flows, managed representation and API, imports and
invocation glue, deterministic function-table slot identities, C declarations, C++ adapters,
construction and cleanup paths, required-export tables, and native build descriptions. ABI-v1 and
any other legacy boundary are never Model
inputs, comparison authorities, fallbacks, or compatibility targets. A legacy boundary may remain
physically present only as an inactive migration recovery artifact until the generated replacement
passes, after which current source, build, fixtures, output, and documentation remove it.

`TedToolkit.Occt.Runtime` contains only handwritten, declaration-agnostic managed mechanisms.
Concrete OCCT layouts, imports, exports, function tables, operation bodies, closed-generic
registrations, and release functions belong to generated wrapper assemblies such as
`TedToolkit.Occt.Windows`. A wrapper uses Runtime's ordinary public API. Runtime grants no wrapper
friend access, caller identity privilege, `InternalsVisibleTo`, or Windows-specific capability.

### Native boundary and generated loading

Generated exports use C linkage and `cdecl`. C++ references are transported as pointers, record
values are placement-constructed in caller-provided storage, and no exception crosses the export
boundary. An operation that may throw receives a final `NativeError*` output, catches OCCT,
standard-library, allocation, and unknown failures, and leaves diagnostics owned by the native
module. A source operation proved `noexcept` keeps the shorter signature and does not pay for an
error carrier. The generated type-independent `NativeError.h` and `NativeError.cpp` provide only
the shared carrier, diagnostic copying, and cleanup; they contain no declaration-specific catalog.

Release and cleanup exports are the deliberate exception to the error-return rule. Handle
intrusive-reference release, Owned destruction, native error clearing, and any other same-library
cleanup entry point use `cdecl void(...)`, are non-throwing, and return no error carrier. Their
callers must be able to run them during disposal or finalization without creating a second failure
path. Owned destruction is not a Handle release operation and does not free the managed storage that
contains the object.

The C boundary is monomorphic. One canonical operation produces one export for each distinct
native call shape. An additional type-specific export is generated only when a registered closed
C++ template specialization, concrete pointer adjustment, value ABI, or implementation actually
changes that call shape. Merely exposing a managed generic or accepting another derived type does
not create a Cartesian product of exports. Base conversion adjusts a pointer into the same proved
native object storage; it does not copy fields or map the object into another representation.

The native and managed outputs form one unversioned, inseparable artifact set produced and packaged
together. The generated binding loads only its package-owned native module, resolves every required
operation and cleanup export into a private static managed `IntPtr[]`, validates the complete table,
and publishes it once. The generated table is the compiled operation inventory; no separate binding
manifest or fingerprint is required at runtime. No generated call observes a partially initialized
table. The boundary does not support independent native upgrades, module substitution, or
major/minor ABI compatibility.

Every native allocation is destroyed and freed by the same native artifact that created it. Owned
object bytes are the exception: they reside directly in managed storage whose address is stabilized
with `fixed` whenever native code receives it, so the originating native artifact constructs and
destructs the object but does not allocate or free its backing memory. Native cleanup exports are
non-throwing. Managed code invokes generated unmanaged function pointers directly; it does not use
managed delegates or declaration-specific Runtime imports. Ordinary generated calls read their
deterministic slot from the generated table and cast it to the exact unmanaged Cdecl signature.
When a factory creates `Handle<T>` or `Owned<T>`, it copies the exact release or destructor address
from its slot into the owner; disposal and finalization do not look up mutable global state. After
initialization, the generated binding keeps its native module and table available until
process termination.
Module unloading is unsupported, and individual owners neither acquire nor release module leases.
Runtime owner constructors validate only the pointer and function inputs they receive; they cannot
authenticate a cleanup function's module origin from its address. The generated loader and factory
are therefore responsible for supplying cleanup pointers only from the package-owned
process-lifetime table.

### Managed object model

Every supported C++ object has an exact unmanaged C# struct derived from the pinned compiler's
complete object layout. The generated representation uses `LayoutKind.Sequential`, typed fields,
explicit private padding, and alignment-preserving opaque storage. It does not use managed
`BaseType` fields, `FieldOffsetAttribute`, or a universal `Pack = 1`. Unsupported or unproved
layouts are rejected before paired callable emission and reported at the narrowest affected boundary.

C++ inheritance is represented by generated C# interfaces. Generated instance operations are
extension methods over their semantic receiver: `in T` for const values, `ref T` for mutable
values, `Handle<T>` for transient objects, and `Owned<T>` for non-transient RAII objects. Exact
layout structs contain representation only and never implement `IDisposable` or declare lifetime
operations.

The Model records every direct base relation and classifies its pointer conversion once. A sole,
non-virtual direct base under the pinned Windows/MSVC ABI is `Identity` and uses a managed pointer
reinterpretation. Multiple inheritance, virtual inheritance, and any unproved relation are
`NativeAdjust` and use one generated C++ adjustment operation for the required derived/base pair.
Methods are emitted once for their declaring type; derived types reuse base extensions through
interface constraints instead of receiving duplicate native exports.

The generic receiver exists only for a declaring type with generated derived records. Types with no
inheritance reuse surface keep their concrete receiver, so ordinary value types such as `gp_XYZ`
do not expose `TReceiver`. In the inheritance case, C# does not permit an `in TReceiver` extension
receiver; generated const operations therefore use `ref TReceiver` to avoid a value copy while the
native call remains const.

Generated public managed operations preserve the recognizable source C++ operation shape: operation
name, static or instance role, parameter order and meaning, const or mutable receiver semantics,
and projected business return type. Interop-only details do not appear in that public shape. In
particular, the native error return and result output slot remain private generated invocation
details, while C++ object parameters and returns use their approved exact-layout `struct`,
`Handle<T>`, or `Owned<T>` projection. Generic object operations constrain `T` with `unmanaged` and
the generated inheritance or lifetime interface required by the source declaration, so invocation
does not box or copy an object merely to satisfy a base operation.

Each native record is emitted into one C++ source and each managed record into one C# source. Native
exports have C linkage and no C++ namespace. Names use `Type_Operation`; same-name overloads receive
one-based suffixes in header declaration order. Parameters and results cross the boundary only as
approved scalar values or pointers: C++ references are reconstructed inside the adapter, record
results use caller-provided storage, transient construction returns one retained pointer, and
non-transient construction placement-constructs caller-provided storage.

After parsing and before either emitter runs, a compiler-probe module generates, compiles, and runs
one temporary C++ executable over the selected record graph. It reports `sizeof`, `alignof`,
`std::is_trivially_copyable`, and `std::is_trivially_destructible` for every record. The Model
rejects missing, malformed, duplicate, or size-disagreeing results, then classifies each record as
Value, Owned, or Handle. The temporary source and executable are removed after collection; callers
provide no trait configuration.

The ownership categories are:

- Proved trivial native values are ordinary copyable structs and require no disposal.
- A `Standard_Transient` exact-layout struct is owned by Runtime's sealed invariant
  `Handle<T>`. Its only declared public members are
  `Handle(T* value, delegate* unmanaged[Cdecl]<T*, void> release)`, `ref T Value`, and
  `Dispose()`. Construction adopts one already-owned intrusive reference without retaining it. The
  Handle stores the address of an object whose storage and final destruction remain governed by C++
  reference counting; it does not contain or own that storage.
  Disposal atomically detaches the managed owner slot and calls the matching non-throwing native
  `void(T*)` intrusive-release export at most once. `Value` is a non-owning direct data view, throws
  after disposal, does not extend lifetime, and must not overlap disposal. A reference already
  returned cannot be revoked. Normal OCCT operations use Handle extension methods rather than
  `Value`.
- A supported non-transient object with native RAII state is owned by a separate sealed invariant
  `Owned<T>` constrained by `where T : unmanaged, IOcctRaii`. Runtime's empty `IOcctRaii` marker
  identifies only generated exact-layout structs classified as supported non-`Standard_Transient`
  RAII; eligible `TCollection_*` types implement it, while trivial values and
  `Standard_Transient` projections do not. The owner object contains one private `T` field as the
  exact-layout object's managed storage instead of adopting a separately allocated C++ object or
  allocating a second storage object. Generated construction, invocation, and destruction
  stabilize that field with `fixed` whenever native code receives its address. The
  originating native artifact placement-constructs the object in that storage. Disposal invokes
  the matching C++ destructor exactly once, but never calls the `Standard_Transient`
  intrusive-release operation or a native storage-free function; the GC reclaims the managed
  backing storage. A distinct object is
  created only through an explicit generated clone or copy operation. Its public `ref T Value` is
  the same simple non-owning data view: it throws when the owner is already disposed, does not
  extend lifetime, and must not overlap disposal. `Owned<T>` and `Handle<T>` both implement
  `IOcctOwner<T>`, which exposes only that `ref T Value` access for generated invocation. They have
  no ownership inheritance or conversion, and the interface defines no cleanup semantics.

Model normalization assigns exactly one of those three categories before any emitter runs. A
proved `Standard_Transient` descendant is eligible for `Handle<T>` only when its complete intrusive
reference lifecycle is known. A non-transient object is eligible for ordinary value projection only
when native evidence proves safe value copying and absence of required lifetime cleanup. Every
other non-transient object is eligible for `Owned<T>` only when its construction, copy,
destruction, alignment, and same-library cleanup contracts are complete and representable.

The Model owns both the classification and its compiler-backed evidence; emitters do not reclassify
a type. Failure to select exactly one eligible category is an unsupported disposition, not a
default category. The complete operation model additionally records receiver, parameter, result,
borrowing, transfer, construction, and cleanup semantics. Missing or contradictory lifetime flow
rejects the whole operation before any callable layer is emitted.

Borrowing remains an operation-level fact and does not create a fourth public object category.
Generated bindings preserve direct C++-like non-owning access through the applicable exact-layout
value, `ref T` view, or already-approved low-level native-pointer boundary. They do not generate
`Borrowed<T>`, a per-declaration borrowed reference class, an owner-retaining facade, a lease, or a
managed object per native pointer, iterator, or subobject. The Runtime Analyzer reports supported
suspicious lifetime patterns, but it is suppressible and incomplete; callers remain responsible
for keeping the native owner live and for avoiding use-after-free or native invalidation.

A C++ `const T&` result is projected as `ref readonly T`; a C++ `T&` result is projected as
`ref T`. The generator never replaces either result with a hidden copy, clone, retain, allocation,
`Owned<T>`, or `Handle<T>`. Such a replacement would change native semantics and make a policy
decision for the consumer. The returned reference retains the C++ owner-lifetime and invalidation
requirements, which generated API documentation must state explicitly.

OCCT smart-pointer storage has two distinct managed projections. Lowercase `handle<T>` is a
pointer-sized exact-layout struct with one private `T*` field and a non-owning `ref T Value` view;
it exposes no raw pointer, construction, conversion, retention, release, or disposal API.
C++ `const opencascade::handle<T>&` and
`opencascade::handle<T>&` therefore become `ref readonly handle<T>` and `ref handle<T>`.
Uppercase `Handle<T>` remains the managed owner of one intrusive reference received from an owning
native result. An `opencascade::handle<T>` returned by value therefore becomes `Handle<T>`, while
the lowercase type remains the representation for fields, parameters, and borrowed references.
The compiler probe proves the native handle specialization has pointer size and alignment before
generated code relies on the lowercase layout.

When a direct handle<T> field participates in an evidenced cyclic managed type-loading failure on
the pinned runtime, its containing layout uses private pointer-sized storage at the same native
offset and exposes a same-named ref handle<T> property (ref readonly for const native storage).
This is an exact typed view of the original bytes, not a copied handle, new owner or public pointer.
The Model selects only the responsible storage edges; loadable self-references, acyclic handle
fields and other non-overlapping ordinary fields remain fields. Metadata consumers must account for the approved
field-to-property distinction. The property preserves native-name metadata, allowed ref/in access,
writability and caller-owned lifetime/invalidation obligations, without retain, release, allocation
or pointer escape. It must remain an interior managed reference when its containing storage moves.

This refines the existing sequential storage/typed-view boundary under GEN-01 through GEN-05; it
does not change Runtime ownership or add a representation category. Keeping the failing generic
field graph, including explicit-layout variants, does not establish loadability on the pinned
runtime; dropping its declarations would unnecessarily remove native capabilities. The selected
view must prove exact native storage and loading for the complete supported closed-type set before
shipping. Reconsider this projection if the supported runtime's loading rules change or either
reference identity or complete layout equality cannot be established.

Ordinary native fields whose byte ranges overlap, including representable named unions, share one
private sequential physical range. Their same-named typed `ref T` properties alias the original
bytes; const native storage uses `ref readonly T`. Pointer-slot constness is distinct from pointee
constness. Non-overlapping fields retain their existing field representation, and native bitfields
remain value properties without an address/ref surface. Native-name metadata and complete native
size, alignment, offsets, padding and neighboring storage remain authoritative.

These views are interior managed references across relocation, not copied values, escaping raw
pointers or owners. Field-reference accessors, including cyclic handle views, are readonly members
independently of their ref/ref-readonly return shape: an in/ref-readonly containing receiver must
not trigger a defensive copy. Return constness still follows the native field storage qualification.
They introduce no allocation, retain/release or lifetime extension. The caller
remains responsible for native union active-member rules, explicit construction/destruction, owner
lifetime and invalidation; generated accessors do not select or activate a member. Reflection and
field-specific source syntax must account for the field-to-property distinction. A supported
closed specialization still needs its own exact-layout proof. Reconsider the view if those storage
or reference guarantees cannot be established; do not silently drop representable overlap or use
Explicit layout to bypass the sequential-storage boundary.

Each applicable transient operation is generated as two direct extension overloads: one accepts
`IOcctOwner<T>` and therefore supports owning `Handle<T>` or `Owned<T>`, and one accepts borrowed
`in handle<T>`. Both call the same generated `NativeApi` slot directly; no generated `Core`
forwarding method is emitted. The owner overload performs the required `GC.KeepAlive`, while the
borrowed overload introduces no ownership, boxing, retention, or allocation.

`Owned<T>` has no public construction-completion state or method. Its only declared public members
are the generated-only
`Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)` constructor, `ref T Value`, and `Dispose()`.
`destroy` must be non-null and non-throwing. The constructor creates the owner whose private `T`
field is the construction destination. A generated factory placement-constructs that field through
`Value` and exposes the owner only after native success. A local success flag and `finally`
suppress finalization on every managed exit before native success is established. Native
construction failure is projected only after that suppression; generated code does not call
`Dispose()`, the destructor, or a native storage-free operation for the unconstructed object.
Suppressing the generated-only constructor diagnostic and using the unconstructed owner manually
is outside the supported Runtime contract.

The constructor is public solely for ordinary cross-assembly access from generated wrappers and is
marked `GeneratedCodeOnly`. `TTOCCT001` reports handwritten `new Owned<T>(...)` as an error. Normal
consumers have no direct construction path and receive `Owned<T>` only from generated projections of
C++ factories or copy operations. The marker is compiler guidance rather than authentication, so
the constructor still validates the destructor input available at runtime.

Direct-field ownership is eligible only when the pinned CLR target proves that the private `T`
field representation satisfies the native `alignof(T)` requirement. This is a model and generator
admission rule, not a Runtime constructor check: observing one fixed address cannot prove alignment
after later GC relocation. A type whose required alignment cannot be proved for this representation
is rejected before callable binding emission; the generator does not silently select a second
storage allocation or allow a possibly misaligned native call.

For the pinned win-x64 sequential storage model, supported native alignments are 1, 2, 4 and 8.
Larger native alignment is not supplied by setting a larger Pack or observing a favorable pinned
address. Reject the unrepresentable declaration from the supported managed/native operation set and
report its native identity, alignment and failed target guarantee. Dependency closure removes and
reports only members or dependent declarations that cannot retain their exact typed representation;
unrelated members and independently representable nested declarations remain supported. Native-only
header requirements do not imply managed representation dependency. Do not erase typed references
to void pointers or introduce allocation/copying fallbacks. Reconsider admission only when stronger
target storage guarantees are established or a new ownership architecture is explicitly approved.

Generated operations obtain every owner-derived pointer inside a lexical `fixed` scope over
`owner.Value` through `IOcctOwner<T>` and keep that pointer inside the scope. For `Owned<T>`, the
scope pins its managed backing storage for the native call; for `Handle<T>`, it provides the same
generated syntax over an already-stable native address. Generated code does not call
`Unsafe.AsPointer` or expose another
pointer member. Immediately after each finalizable owner's last unmanaged use, and before managed
error projection can throw, generated code calls
`GC.KeepAlive(owner)`. The `fixed` scope and `GC.KeepAlive` have separate responsibilities: the
former stabilizes the referenced storage for the pointer use, while the latter prevents premature
owner finalization. Neither synchronizes nor makes explicit concurrent `Dispose()` safe. Direct
`Value` consumers likewise own the obligation to keep the owner alive and prevent disposal for the
complete native-reference use.
This requirement follows the documented [.NET `GC.KeepAlive` lifetime
contract](https://learn.microsoft.com/dotnet/api/system.gc.keepalive): an unmanaged pointer does not
itself keep its managed owner reachable, and the JIT may otherwise shorten that owner's lifetime.

### Managed failures and diagnostics

Native failures are caught before crossing the boundary and projected to stable, concrete managed
exception types. The private native error discriminator exists only to transport failure identity
across the C boundary; it is not exposed as a parallel public managed classification. The concrete
exception type is the managed classification authority. Diagnostic strings remain native-owned
only until the managed projection copies them; the matching native artifact releases their storage.
No last-error global or thread-local state is part of the contract.

Every generated managed OCCT operation consumes its private native error return. Success continues
with the projected C++ result; each recognized failure category consumes the matching same-library
diagnostic owner and throws its approved concrete managed exception type. OCCT failures, standard
C++ exceptions, and unknown native failures remain distinct managed exception types; an
unrecognized discriminator is projected as the unknown-native-failure type. The generated public
API exposes neither the native error carrier nor its discriminator and does not add a parallel
`Try` or error-returning surface. Release and cleanup remain non-throwing `void` paths and therefore
never participate in managed error projection.

Generated wrappers in independent assemblies access the Runtime error carrier and projection entry
point through public contracts marked `GeneratedCodeOnlyAttribute`. Public visibility provides CLR
accessibility only: these contracts never appear in consumer-facing generated operation signatures,
and handwritten operational use is reported by the Runtime analyzer. The carrier is a direct
sequential ABI record: it exposes only the native discriminator and three diagnostic-pointer fields
in layout order and declares no managed construction, reset, property, or other behavior. The public
projection entry point accepts the originating native error-clear export as an unmanaged `cdecl`
function pointer. It does not introduce a managed cleanup delegate, friend assembly, or
declaration-specific Runtime import.

The existing `TedToolkit.Occt.Analyzer` remains an OCCT-header source generator. Consumer compiler
guardrails are built by an internal, non-packable Analyzer project and embedded directly in the
Runtime package. Runtime does not load them while compiling itself. They report handwritten use of
unavoidable public Runtime hooks reserved for generated implementations and supported suspicious
lifetime uses of non-owning `Handle<T>.Value` or `Owned<T>.Value` references. They remain
declaration-agnostic and do not attempt complete alias, concurrency, or lifetime proof. The detailed
boundary is recorded in the active
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
- Generate every declaration-specific binding and build artifact from the completed Model. Do not
  use ABI-v1, legacy fixtures, emitted text, or handwritten bindings as generator input, an oracle,
  a fallback, or a compatibility target.
- Classify every supported object exactly once as a proved value, transient `Handle<T>`, or
  non-transient `Owned<T>` projection. Keep the classification and operation-level borrowing,
  transfer, construction, and cleanup facts in the Model; reject ambiguity without partial output.
- Use `cdecl` for every generated export. Give every callable OCCT operation a private native error
  return and transport its source result through a result slot; keep release and cleanup exports
  non-throwing `void` functions.
- Derive one deterministic generated function-table slot for every required operation and cleanup
  export. Load the package-owned module, validate all required exports privately, and publish one
  complete static managed table. Do not emit a separate binding manifest or fingerprint protocol.
- Invoke ordinary generated operations through their exact typed table slots. Copy release and
  destructor pointers into finalizable owners at construction; never make Runtime owners retain a
  table, index a mutable global table during cleanup, or depend on generated table types.
- Preserve the source C++ operation's recognizable business shape in the generated public managed
  API. Keep error carriers, result slots, pointer adjustment, and cleanup mechanics private.
- Generate type-specific native operation exports only for distinct proved native call shapes;
  never expand every managed generic and derived-type combination by default or copy an object to
  perform a base conversion.
- Prove complete native and managed layout equality for every shipped type and supported closed
  generic specialization on its exact target matrix.
- Keep layout structs non-owning and non-disposable. Mark only supported non-transient RAII layouts
  with `IOcctRaii`, and keep transient and non-transient ownership in their distinct Runtime
  reference owners.
- Preserve `Handle<T>`'s exact three-member public surface and direct `T*` release contract unless
  this architecture is explicitly revised first.
- Expose only a simple non-owning `ref T Value` data view from each owner. Do not add a callback,
  invocation lease, `DangerousValue`, or a second address-access API to make direct access appear
  lifetime-safe.
- Keep borrowing as an operation-level contract, not a public owner category. Do not generate
  `Borrowed<T>`, declaration-specific borrowed classes, owner-retaining facades, leases, or managed
  wrapper allocation solely to guard native pointers, references, iterators, or subobjects against
  use-after-free. Preserve the applicable direct C++-like value, `ref T`, or approved low-level
  pointer surface and leave its lifetime obligations with the caller.
- Preserve reference-return semantics exactly: `const T&` becomes `ref readonly T` and `T&`
  becomes `ref T`. Do not insert an implicit copy, clone, retain, allocation, or ownership wrapper.
- Generate owner pointer use with a lexical `fixed` scope over `owner.Value`. Keep every derived
  pointer inside that scope; do not call `Unsafe.AsPointer` or introduce a generated-only pointer
  method or static pointer gateway. Use the value-only `IOcctOwner<T>` interface for owner receivers.
- Keep each finalizable owner alive through every generated unmanaged use with `GC.KeepAlive`
  immediately after that owner's last such use and before error projection. Do not treat `fixed` as
  a substitute for owner liveness or claim either mechanism protects against explicit concurrent
  disposal.
- Keep Runtime declaration-agnostic and wrappers equally capable through public API only.
- Contain all native exceptions and perform every cleanup through the originating native artifact.
- Use Runtime analyzers for unavoidable public generated-only hooks and best-effort `Value` lifetime
  diagnostics. Keep layout validation, owner-state checks, native safety, and behavior expressible
  by API shape in their owning compiler or runtime layers; analyzer suppression transfers the
  low-level lifetime risk to the caller.
- Do not claim package coverage, RID support, independent native compatibility, or publication
  without corresponding generated and integration evidence.

## Decision links and exceptions

This record is the current architecture authority. The native-loader and function-table direction
is approved by [ADR-001](../adr/ADR-001-native-release-binding/README.md). A proposed exception to a Required principle or to this architecture
must update the affected current-truth document and receive explicit maintainer approval before
implementation; a change record alone cannot redefine the architecture.

## Review triggers

Reassess this architecture when any of the following occurs:

- a second platform, architecture, compiler ABI, RID, or managed target is proposed;
- native and managed artifacts need independent versioning or upgrade compatibility;
- an exact native layout cannot be represented or proved with the selected CLR strategy;
- a new ownership category, public pointer surface, callback, or module-unloading model is needed;
- a managed borrowed-reference wrapper, owner-retaining facade, lease, or per-borrow allocation is
  proposed;
- owner finalization is removed, generated calls cannot place `GC.KeepAlive` after their last
  unmanaged use, or direct `Value` access is expected to become safe during concurrent disposal;
- a generated module must unload or replace its published function table while any operation or
  finalizable owner can still retain one of its addresses;
- profiling demonstrates a material managed function-table cost or a different table storage form
  produces a sustained representative benefit beyond the ADR threshold;
- Runtime needs declaration-specific knowledge or a wrapper requests privileged access;
- generated output requires handwritten declaration-specific code or a second semantic authority;
- an emitter needs to reclassify ownership, a declaration cannot fit exactly one existing category,
  or a generated operation lacks complete borrowing, transfer, or cleanup semantics;
- a generated path, build, or acceptance proof requires ABI-v1 as an input, oracle, fallback, or
  compatibility target;
- a public non-throwing, `Try`, native-error-returning, or otherwise non-C++-shaped managed operation
  surface is proposed;
- an operation would omit the native error return, a cleanup path would return an error, or eager
  per-derived-type export expansion is required;
- analyzer enforcement is proposed as a security or runtime correctness boundary; or
- the first public package baseline is prepared for publication.
