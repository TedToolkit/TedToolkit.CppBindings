# Implement the managed OCCT non-transient RAII lifetime

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: draft -->

- Priority: P1
- Approval: None while Draft. The ownership category and public name are part of the active
  architecture; the exact public wrapper integration surface remains a start condition.

<!-- section: goal-rationale -->
## Goal and rationale

`TedToolkit.Occt.Runtime` provides one reference-type `Owned<T>` lifetime for generated exact-layout
non-`Standard_Transient` RAII structs. It keeps placement-constructed native storage stable across
calls, aliases one disposal state, and invokes destruction and storage release exactly once through
the originating native module without placing ownership in the copyable struct.

<!-- section: scope -->
## Scope and non-goals

- In scope: the public sealed invariant `Owned<T> : IDisposable` contract; its public but
  generated-only construction and scoped-invocation boundary for any wrapper assembly; constraints
  to approved generated RAII layout types; generated factory admission; correctly aligned
  stable storage; placement-construction failure recovery; lease-scoped generated access; aliasing;
  deterministic disposal; finalization; explicit generated clone/copy admission; exactly-once
  destruction and same-library free; module lifetime; Runtime tests and documentation.
- In scope: reuse of the declaration-agnostic internal lifetime core delivered with `Handle<T>`.
  Generated assemblies own type identities and target-specific construction, copy, destruction,
  alignment, pointer-adjustment, and cleanup operations and supply them through Runtime's normal
  public owner contract.
- Non-goals: generating `Owned<T>` or generated source into Runtime; `InternalsVisibleTo`; a
  Windows-only or otherwise privileged wrapper path; an extra wrapper-only integration layer; changing `Handle<T>`
  semantics; trivial values; a public common owner base, interface,
  inheritance, or conversion; public constructors from an address; public raw pointers, `.Value`,
  retain, or ownership duplication; generated OCCT operations; native adapter generation;
  packaging; or publication.
- Compatibility: no supported public RAII owner has been released. This introduces the first
  non-transient owner surface.
- Preserved behavior: construction, destruction, and storage release use the same native artifact;
  an active invocation keeps its storage and module alive; OCCT operation thread safety remains the
  caller's responsibility.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Public RAII ownership | Runtime has no non-transient owner | One sealed invariant `Owned<T> : IDisposable` owns only approved generated RAII layouts | Exact native representations remain structs |
| OB-02 | Construction | No stable managed RAII construction path exists | Generated factories placement-construct `T` in correctly aligned owner storage and recover failed construction | C++ `new T` is not the generated ownership path |
| OB-03 | Aliasing and disposal | No owner state exists | Assignment aliases one owner; leases finish before one destructor and same-library free | Disposal does not claim operation thread safety |
| OB-04 | Copy semantics | Struct assignment could be mistaken for native copying | Only an explicit generated clone/copy operation creates a distinct owned native object | Ordinary owner assignment remains aliasing |
| OB-05 | Ownership separation | A common owner hierarchy was undecided | `Owned<T>` and `Handle<T>` have no public inheritance, conversion, or common owner base | Internal lifetime machinery may be shared |

<!-- acceptance-case: AC-01 -->
### AC-01 — Runtime exposes one separate constrained RAII owner

```gherkin
Scenario: Inspect the public Runtime ownership API
  Given the Runtime assembly
  When its public ownership surface is inspected
  Then Owned<T> is a sealed invariant reference type implementing IDisposable
  And T is limited to approved generated non-transient RAII layouts
  And low-level construction, admission, lease, and address hooks are marked for generated code only
  And Owned<T> has no public ownership base, conversion, or inheritance relationship with Handle<T>
  And no public address constructor, raw pointer, IntPtr, Value, Retain, or copyable owning struct is exposed
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Construction owns stable aligned storage or nothing

```gherkin
Scenario: Construct a generated RAII object
  Given a supported generated factory and matching native module lifetime input
  When placement construction succeeds
  Then the returned Owned<T> holds correctly aligned stable storage from that module
  And when construction fails, any acquired storage and module lifetime are released exactly once
  And no destructor runs for an object whose construction did not complete
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Disposal waits logically for leases and cleans up once

```gherkin
Scenario: Dispose aliases while an invocation lease is active
  Given two variables alias one constructed Owned<T>
  And one generated operation holds a lease
  When both aliases are disposed repeatedly
  Then new leases fail before native access
  And the existing lease keeps the storage and module alive
  And after the final lease, the destructor and same-library storage release each run exactly once
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Explicit cloning creates one distinct owned object

```gherkin
Scenario: Clone a supported RAII object
  Given one live Owned<T> whose generated copy semantics are supported
  When its explicit generated clone or copy operation succeeds
  Then the result owns distinct correctly aligned storage constructed through the mapped C++ copy semantics
  And disposing either owner does not invalidate or release the other
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Unsupported ownership never reaches native memory

```gherkin
Scenario: Use an unsupported type, construction input, module, or disposed owner
  Given the public Runtime owner contract cannot establish the requested RAII ownership state
  When construction, cloning, or lease acquisition is requested
  Then it fails before native address access or invocation
  And no destructor or storage free is attempted for memory it does not own
```

## Constraints and risks

- [GEN-02](../../principles/README.md#gen-02-reproduce-every-supported-native-object-layout-exactly),
  [GEN-03](../../principles/README.md#gen-03-separate-native-representation-from-ownership-and-behavior),
  [GEN-04](../../principles/README.md#gen-04-keep-the-shared-runtime-minimal-and-declaration-agnostic),
  and the [generated binding architecture](../../architecture/generated-binding-system.md) govern
  this change.
- Generated layout structs never implement `IDisposable`; `Owned<T>` owns storage and native
  lifecycle, not a copied struct value.
- Runtime does not own concrete layout, alignment, constructor, copy, destructor, native symbol, or
  closed-specialization facts. Any generated wrapper supplies them through the same narrow public
  owner construction and scoped-invocation contract; no assembly receives friend access.
- Generated-only metadata permits the Runtime analyzer to guide handwritten callers but grants no
  authority. Runtime validates every admission, storage, module, and owner-state invariant when the
  analyzer is absent or suppressed.
- Constructor failure, destructor failure containment, concurrent disposal, and finalization require
  deterministic proof without timing sleeps. Native cleanup remains non-throwing at the managed
  disposal boundary.
- Escalate if implementation requires `new T`, a routine long-lived public pointer or address
  view, public owner polymorphism, bitwise ownership copying, cleanup through another module,
  generated code in Runtime, `InternalsVisibleTo`, or declaration-specific Runtime authority.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: one Runtime delivery implements the `Owned<T>` public contract,
  generated-only wrapper construction and scoped-invocation boundary, reuse of the internal lifetime
  core, owner state transitions, tests, and documentation.
- Real start conditions: completed `Handle<T>` lifetime core and an approved exact `net8.0` public
  ownership integration surface usable by independently named wrapper assemblies without friend or
  private-member access.
- Likely touchpoints (non-binding): Runtime ownership source, Runtime tests, Runtime README, and
  public API inspection.
- Private choices left open: aligned allocation mechanism, internal strategy representation,
  synchronization primitive, internal lease helper, finalizer helper, and the representation of
  per-owner construction and cleanup inputs.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 / AC-05 | Acceptance and compatibility | Public API Contract plus compile-time wrapper consumers | Reflection and two independently named wrapper fixtures prove owner separation, exact constraints, invalid-input rejection, absence of prohibited APIs, and no friend access | Build Runtime and wrapper fixtures for `net8.0`; run Runtime public-surface cases |
| AC-02 | Acceptance and regression | Unit through controlled construction inputs | Successful construction owns aligned storage; each failure point releases only acquired resources and never destructs an unconstructed object | Run Runtime placement-construction failure-matrix cases |
| AC-03 | Acceptance and concurrency regression | Component with controlled coordination | Aliases share disposal, active leases keep storage/module alive, and destructor/free occur once without timing sleeps | Run Runtime lease/dispose/finalize interleaving cases |
| AC-04 | Acceptance and lifetime regression | Component through wrapper-supplied copy operations | Explicit clone owns independent storage and cleanup while assignment remains aliasing | Run Runtime clone/copy ownership cases |
| Repository gate | Structural regression | Release build and Runtime suite | Runtime builds without warnings and all intended tests pass | `dotnet build TedToolkit.Occt.slnx -c Release`; `dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release --no-build -- --report-trx` |

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-05 pass; Runtime documents one separate `Owned<T>` RAII owner and no public common
owner base, conversion, pointer, `.Value`, or address constructor; construction, failure recovery,
leases, aliasing, clone, disposal, finalization, module liveness, destruction, and same-library free
are proved; the `net8.0` Runtime builds cleanly; generated operations and packaging remain
outside this change.
