# Implement the managed OCCT non-transient RAII lifetime

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: candidate-ready -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user-approved-2026-08-26 -->
<!-- candidate-binding: commit:fb9d0d6725fc53843b68ed23672e51355b162406 -->

<!-- section: goal-rationale -->
## Goal and rationale

`TedToolkit.Occt.Runtime` provides one reference-type `Owned<T>` lifetime for generated exact-layout
non-`Standard_Transient` RAII structs. It keeps the object in managed storage whose address is
stabilized with `fixed` during native use, exposes simple non-owning `Value` access, aliases one
disposal state, and invokes destruction exactly once through the originating native module. The
owner directly contains one private `T` field as that storage and has no public construction-
completion phase. It never invokes intrusive `Release` or a native storage-free function; the GC
owns the backing storage.

<!-- section: scope -->
## Scope and non-goals

- In scope: the empty public Runtime marker `IOcctRaii`; the public sealed invariant
  `Owned<T> : IDisposable where T : unmanaged, IOcctRaii` contract; its generated-only
  `Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)` constructor using the matching non-throwing
  destructor; public non-owning `ref T Value`;
  `GeneratedCodeOnly` marking and `TTOCCT001` rejection of handwritten construction; constraints to
  approved generated RAII layout types; controlled generated-shape factory fixtures; managed
  storage held directly in one private `T` field; lexical `fixed` native access; placement-
  construction failure recovery through a local success flag and `finally` suppression of an
  unreturned temporary;
  aliasing; deterministic disposal; finalization; explicit generated clone/copy admission;
  exactly-once destruction without intrusive release or native storage free; Runtime tests and
  documentation.
- In scope: extraction or reuse of declaration-agnostic disposed-state and cleanup machinery where
  it preserves `Handle<T>` behavior. No pre-existing reusable Handle lifetime core is assumed.
  Generated assemblies own type identities and target-specific construction, copy, destruction,
  alignment, pointer-adjustment, and cleanup operations and supply them through Runtime's normal
  public owner contract.
- Non-goals: generating `Owned<T>` or generated source into Runtime; `InternalsVisibleTo`; a
  Windows-only or otherwise privileged wrapper path; an extra wrapper-only integration layer;
  changing `Handle<T>` semantics; trivial values; a public common owner base, interface,
  inheritance, or conversion; public constructors from an address; `GetPointer`, another raw-pointer
  member, or a static pointer gateway; construction-completion API or state; callback or lease-based
  invocation; per-owner module leases or native module unloading; retain or ownership duplication;
  generated OCCT operations beyond the approved `Value` access contract; generated function-table
  initialization, native adapter generation, native error projection, real declaration
  classification, or generator implementation of native `alignof(T)` admission or rejection;
  supported handwritten `new Owned<T>(...)`; packaging; or publication.
- Compatibility: no supported public RAII owner has been released. This introduces the first
  non-transient owner surface.
- Preserved behavior: construction and destruction use the same native artifact; managed backing
  storage is never released through that artifact. Generated invocation uses `GC.KeepAlive` to
  prevent premature finalization; explicit concurrent disposal, use-after-free, and OCCT operation
  thread safety remain caller responsibilities. The generated exact-match native module remains
  loaded until process termination; owners do not acquire or release module leases.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Public RAII ownership | Runtime has no non-transient owner or RAII category marker | Empty `IOcctRaii` marks supported generated non-transient RAII layouts; one sealed invariant `Owned<T> : IDisposable where T : unmanaged, IOcctRaii` directly contains one private `T` field | Exact native representations remain structs |
| OB-02 | Construction | No managed RAII construction path exists | Generated factories create an owner through `Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)`, placement-construct its field under `fixed`, return it only on success, and use a local success flag plus `finally` to suppress finalization on every exit before native success is established | C++ `new T`, a second storage object, and a public completion phase are not the generated ownership path |
| OB-03 | Aliasing and disposal | No owner state exists | Assignment aliases one owner; disposal and finalization detach it once before one destructor, without intrusive release or native storage free | Disposal does not wait for or synchronize native operations |
| OB-04 | Copy semantics | Struct assignment could be mistaken for native copying | Only an explicit generated clone/copy operation creates a distinct owned native object | Ordinary owner assignment remains aliasing |
| OB-05 | Ownership separation | A common owner hierarchy was undecided | `Owned<T>` and `Handle<T>` have no public inheritance, conversion, or common owner base | Internal lifetime machinery may be shared |
| OB-06 | Low-level access | No non-transient owner access contract exists | `Value` returns a non-owning `ref T` after a disposed-state check | Access does not extend lifetime or tolerate concurrent disposal |

<!-- acceptance-case: AC-01 -->
### AC-01 — Runtime exposes one separate constrained RAII owner

```gherkin
Scenario: Inspect the public Runtime ownership API
  Given the Runtime assembly
  When its public ownership surface is inspected
  Then Owned<T> is a sealed invariant reference type implementing IDisposable
  And T is constrained by unmanaged and the empty Runtime marker IOcctRaii
  And a controlled generated-shape non-transient RAII struct can implement IOcctRaii
  And controlled trivial and Standard_Transient structs without IOcctRaii cannot close Owned<T>
  And its generated-only constructor is Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)
  And destroy is matching, non-null, and non-throwing
  And handwritten new Owned<T>(...) reports TTOCCT001 as an error
  And independently named generated wrappers can call that constructor through ordinary public access
  And the owner directly contains one private T field as the native object storage
  And public ref T Value remains available for low-level data access
  And Owned<T> has no public ownership base, conversion, or inheritance relationship with Handle<T>
  And no construction-completion method, public address constructor, pointer member, IntPtr, static pointer gateway, Retain, or copyable owning struct is exposed
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Construction uses admitted managed storage or nothing

```gherkin
Scenario: Construct a generated RAII object
  Given a controlled generated-shape factory for a type already admitted by generator validation
  When placement construction succeeds
  Then the returned Owned<T> contains the object directly in its private T field
  And native construction receives its address only inside a lexical fixed scope
  And no separate storage object or native allocation contains that object
  And a local success flag plus finally suppresses finalization on every exit before native success is established
  And when controlled construction fails before native success, the factory suppresses finalization before the failure escapes
  And it neither returns the temporary owner nor calls Dispose or the destructor
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Disposal and finalization clean up once without invocation leases

```gherkin
Scenario: Dispose aliases of one constructed owner
  Given two variables alias one constructed Owned<T>
  When both aliases are disposed repeatedly
  Then the shared state detaches once
  And the matching C++ destructor runs exactly once
  And no intrusive Release or native storage-free operation runs
  And the GC remains responsible for reclaiming the managed backing storage
  And later Value access fails before native access
  And no invocation lease or blocking disposal protocol is introduced
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Explicit cloning creates one distinct owned object

```gherkin
Scenario: Clone a supported RAII object
  Given one live Owned<T> whose generated copy semantics are supported
  When its explicit generated clone or copy operation succeeds
  Then the result owns distinct direct storage constructed through the mapped C++ copy semantics
  And disposing either owner does not invalidate or release the other
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Unsupported ownership never reaches native memory

```gherkin
Scenario: Use an invalid Runtime owner input or disposed owner
  Given a null destructor or an already disposed Owned<T>
  When construction or Value access is requested
  Then it fails before native address access or invocation
  And no destructor is attempted for an object it does not own
```

## Constraints and risks

- [GEN-02](../../principles/README.md#gen-02-reproduce-every-supported-native-object-layout-exactly),
  [GEN-03](../../principles/README.md#gen-03-separate-native-representation-from-ownership-and-behavior),
  [GEN-04](../../principles/README.md#gen-04-keep-the-shared-runtime-minimal-and-declaration-agnostic),
  and the [generated binding architecture](../../architecture/generated-binding-system.md) govern
  this change.
- Generated layout structs never implement `IDisposable`; `Owned<T>` owns the managed storage and
  native object lifecycle rather than a separately allocated C++ object.
- Runtime does not own concrete layout, alignment, constructor, copy, destructor, native symbol, or
  closed-specialization facts. Any generated wrapper supplies them through the same narrow public
  owner construction and `Value` contract; no assembly receives friend access.
- Generator admission requires target-specific proof that direct managed-field storage satisfies
  native `alignof(T)`. Runtime receives no alignment value and cannot turn one observed field
  address into a relocation guarantee; an unproved type fails closed before binding emission
  instead of receiving another storage representation.
- Generated-only metadata permits the Runtime analyzer to guide handwritten callers but grants no
  authority. Runtime validates the destructor input and owner state available to it. Generated
  exact-match initialization validates module identity, supplies cleanup pointers from the
  published process-lifetime table, and owns real declaration, alignment, and call-flow proof.
- Public constructor visibility exists only for independently generated wrappers. Generated C++
  factory and copy projections are the sole supported producer APIs for consumers; direct
  handwritten construction remains unsupported even if `TTOCCT001` is suppressed.
- Constructor failure, destructor failure containment, disposal, and finalization require
  deterministic proof without timing sleeps. Native cleanup remains non-throwing at the managed
  disposal boundary. Runtime checks disposed state when `Value` is obtained but cannot revoke an
  already-returned reference.
- The generated-only constructor establishes an unsafe pre-construction window so generated code can
  placement-construct the contained field. Handwritten constructor use is diagnosed by the Runtime
  Analyzer; suppressing that diagnostic accepts the risk of using or finalizing an unconstructed
  value. Controlled generated-shape fixtures demonstrate suppression before a construction failure
  escapes; actual emitted error-projection ordering belongs to the Generator change.
- Escalate if implementation requires `new T`, `GetPointer` or another public pointer member, a
  common owner interface, a static pointer gateway, an invocation lease, public owner polymorphism,
  bitwise ownership copying, cleanup through another module, generated code in Runtime,
  `InternalsVisibleTo`, or declaration-specific Runtime authority.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

No cross-change prerequisite. The required Handle and analyzer contracts are established repository
baselines rather than active delivery records.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: one Runtime delivery implements the `Owned<T>` public contract,
  generated-only destructor constructor, direct `T` field storage, `Value`, any justified internal
  lifetime-mechanism reuse, owner state transitions, tests, and documentation.
- Real start conditions: completed `Handle<T>` public lifetime contract; the generic `TTOCCT001`
  rule for `[GeneratedCodeOnly]` use; and an approved exact `net8.0` public ownership integration
  surface usable by independently named wrapper assemblies without friend or private-member access.
- Likely touchpoints (non-binding): Runtime ownership source, Runtime tests, Runtime README, and
  public API inspection.
- Private choices left open: internal strategy representation, disposed-state synchronization,
  `Value` validation method, and finalizer helper. The generator change owns the concrete target-
  alignment proof mechanism.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=component -->
<!-- primary-proof: AC-03 purpose=acceptance shape=component -->
<!-- primary-proof: AC-04 purpose=acceptance shape=component -->
<!-- primary-proof: AC-05 purpose=acceptance shape=unit -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Reflection and two independent wrapper fixtures expose the exact one-destructor constructor, direct field, marker constraint, `Value`, and no pointer or common-owner surface; handwritten construction reports `TTOCCT001` | Run the Runtime and Runtime-analyzer TUnit projects in Release |
| AC-02 | Primary | Successful controlled construction uses the direct field under `fixed`, while failed native construction suppresses finalization and performs no destructor or storage-free action | Run the Runtime TUnit project in Release |
| AC-03 | Primary | Aliases share one disposal state; disposal and finalization invoke the destructor at most once and later `Value` access fails | Run the Runtime TUnit project in Release |
| AC-04 | Primary | Explicit clone owns independent storage and cleanup while ordinary owner assignment aliases | Run the Runtime TUnit project in Release |
| AC-05 | Primary | Null destructor and disposed access fail before native entry, and no destructor runs for unconstructed storage | Run the Runtime TUnit project in Release |
| Repository gate | Conditional | Runtime and affected fixtures build without warnings and all intended tests pass | `dotnet build TedToolkit.Occt.slnx -c Release`; run both affected TUnit projects with `dotnet run` |

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-05 and OB-06 pass; Runtime documents empty `IOcctRaii` and one separate
`Owned<T> where T : unmanaged, IOcctRaii` RAII owner with
non-owning `Value` and no public pointer member, common owner base, conversion, static pointer
gateway, construction-completion method, or address constructor; direct field construction, failure
recovery, generated-only admission, handwritten-construction rejection, aliasing, clone,
disposal, finalization, exactly-once destruction, and absence of intrusive release
or native storage free are proved; the `net8.0` Runtime builds cleanly; generated operation bodies
and packaging remain outside this change.
