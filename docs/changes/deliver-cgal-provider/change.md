# Deliver a usable CGAL provider and broad Windows binding

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: approved -->
<!-- delivery-shape: multi-item -->

- Priority: P1
<!-- approval-source: The maintainer approved this exact contract and explicitly authorized continuation with “批准并继续。” in the Codex task on 2026-09-05. -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

Let .NET developers generate CGAL bindings and consume a ready `win-x64` package through real
`TedToolkit.CppBindings.Cgal.Generator`, `Runtime`, and `Windows` packages. The Windows package must
cover every representable declaration in a deterministic finite default EPICK profile, report each
unsupported declaration precisely, and coexist with a fully usable OCCT provider.

<!-- section: scope -->
## Scope and non-goals

- In scope: CGAL 6.2 discovery from vcpkg; configurable finite kernel and closed-template profiles;
  a default EPICK 2D/3D profile; CGAL-specific type, lifetime, check/exception, polymorphic-result,
  dependency, and native-build policies; deterministic admitted and unsupported inventories; real
  Generator, Runtime, and Windows packages; a provider-local locked vcpkg manifest; generated
  managed/native function-table artifacts; recursively app-local native dependencies and their
  notices; focused tests, package-consumer tests, native calls, and user documentation.
- Non-goals: claim infinitely many template instantiations, ship another kernel in the first Windows
  package, support non-Windows targets or another ABI/RID, include Qt/demo APIs or unavailable
  optional third-party features, publish packages, or weaken layout/lifetime proof to raise counts.
- Compatibility or deliberately preserved behavior: Shared remains provider-neutral; CGAL does not
  inherit OCCT `Standard_Transient` or `handle<T>` semantics; the OCCT Generator, Runtime, and
  Windows package remain buildable, tested, and behaviorally unchanged.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | CGAL generation | No CGAL provider exists | Generator accepts deterministic finite profiles, defaults to EPICK, closes dependencies, and emits matching managed/native outputs plus admitted and unsupported inventories | Shared generation and exact-match function-table rules |
| OB-02 | CGAL managed runtime | No CGAL Runtime exists | Runtime exposes the fixed CGAL failure taxonomy while generated operation-specific discriminated results preserve finite optional/variant/Object alternatives | Shared Runtime remains declaration-agnostic; exact declaration layouts remain generated |
| OB-03 | Ready Windows consumption | No CGAL Windows package exists | The package contains the generated managed assembly, compiled native wrapper, every recursively imported non-system native DLL, and third-party notices for the locked CGAL 6.2 EPICK `win-x64` matrix | Package/assembly suffix does not change the generated API namespace; consumers need no CGAL, vcpkg, GMP, or MPFR installation |
| OB-04 | Coverage accounting | CGAL support and omissions are unknown | Every declaration reachable from the maintained finite profile is either admitted or rejected with its narrow failed proof; repeated generation is deterministic | Unsupported semantics fail closed |

<!-- acceptance-case: AC-01 -->
### AC-01 — Generator produces deterministic CGAL profiles

```gherkin
Scenario: Generate an EPICK binding plan
  Given the locked CGAL 6.2 toolchain and the authoritative `epick-windows-v1` profile manifest
  When the Generator runs the default EPICK profile twice
  Then both runs produce identical source, candidate, admitted, unsupported, managed, and native inventories whose admitted and unsupported entries partition the finite candidate set
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Runtime preserves CGAL-specific failure and result semantics

```gherkin
Scenario: Project CGAL provider semantics without OCCT coupling
  Given compiled generated calls for every approved CGAL failure category and every declared alternative of an intersection result
  When the managed projection observes success, empty, known alternative, unknown tag, CGAL failure, standard exception, and unknown native failure outcomes
  Then it returns the exact admitted result or throws the fixed documented CGAL exception while consuming native diagnostics and temporaries exactly once without an OCCT type or dependency
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Windows package covers the admitted EPICK surface

```gherkin
Scenario: Build the default CGAL Windows artifact
  Given the provider-local vcpkg baseline and the pinned CGAL 6.2, GMP 6.3.0#5, MPFR 4.2.2#1, win-x64, MSVC 19.51.36256, and CMake 4.4.3 matrix
  When the Windows package generation and native build run
  Then every admitted EPICK 2D/3D declaration has matching managed and native artifacts and the package contains the recursive non-system DLL import closure, including `gmp-10.dll` and `mpfr-6.dll` when imported, plus applicable third-party notices
```

<!-- acceptance-case: AC-04 -->
### AC-04 — A package consumer performs real 2D and 3D work

```gherkin
Scenario: Consume representative generated CGAL APIs
  Given a clean application references the packed CGAL Windows and Runtime packages
  When it measures the squared distance from Point_2(0,0) to Point_2(3,4), measures the squared distance from Point_3(0,0,0) to Point_3(1,2,2), intersects Segment_2[(0,0),(2,0)] with Segment_2[(1,-1),(1,1)], intersects Segment_2[(0,0),(2,0)] with Segment_2[(0,1),(2,1)], and reads Cartesian index 2 from a 2D point
  Then the real native results are 25, 9, Point_2(1,0), and no intersection respectively, and the invalid index throws `CgalPreconditionException` with nonempty native diagnostics
```

## Default profile and public CGAL mappings

The repository-contained and package-embedded `epick-windows-v1` profile manifest is the sole
default-surface authority. It records the profile version, CGAL and toolchain identity, EPICK kernel
alias, every installed public header, every discovered public declaration, explicit closed class and
free-function template signatures, optional dependency state, and one stable disposition per source
declaration. Direct non-template declarations and listed closed instances form the finite candidate
set; recursive base, field, parameter, result, and required specialization closure is mandatory.
Qt, demos, and unavailable optional dependencies receive explicit source dispositions and do not
enter the candidate set. The admitted and unsupported inventories must partition that candidate set,
while the source inventory ensures an open template or excluded optional feature is visible rather
than silently omitted.

The provider-local vcpkg manifest and configuration pin the Microsoft vcpkg registry builtin
baseline `30ef65cad98f08e7197c9a1656fbd871bcb72f2d`, whose version database selects CGAL 6.2,
GMP 6.3.0#5, MPFR 4.2.2#1, and
`x64-windows`. The default Windows verifier requires MSVC compiler 19.51.36256 and CMake 4.4.3 and
records the resolved vcpkg package ABI identities. Generator callers may use other explicit finite
profiles, but those runs do not claim the `epick-windows-v1` Windows artifact identity.

`TedToolkit.CppBindings.Cgal.Runtime` exposes `CgalException` as the provider base. Native
`CGAL::Error_exception`, `Precondition_exception`, `Postcondition_exception`,
`Assertion_exception`, `Test_exception`, and a thrown `Warning_exception` map respectively to
`CgalErrorException`, `CgalPreconditionException`, `CgalPostconditionException`,
`CgalAssertionException`, `CgalTestException`, and `CgalWarningException`, all through
`CgalFailureException`. Other `CGAL::Failure_exception` values map to `CgalFailureException`;
`std::invalid_argument`, `std::out_of_range`, `std::bad_alloc`, `std::overflow_error` or
`std::underflow_error`, other `std::exception`, and non-standard failures map to
`CgalArgumentException`, `CgalArgumentOutOfRangeException`, `CgalOutOfMemoryException`,
`CgalArithmeticException`, `CgalStandardException`, and `CgalUnknownException`. Every exception
retains the copied native type name, message, and stack text. The projection clears the native-owned
diagnostics exactly once in a `finally` path, including UTF-8 conversion failure. The Windows
wrapper defines `CGAL_DEBUG` so ordinary assertions, preconditions, postconditions, and warnings
remain enabled in Release, preserves CGAL's default throw-on-error and continue-on-warning behavior,
and maps a warning only if native code is explicitly configured to throw one.

A supported `std::optional<std::variant<A...>>` or `CGAL::Object` return becomes a generated
operation-specific readonly discriminated result with `None` and one named case per profile-declared
alternative plus typed accessors. Empty native results produce `None`; a known alternative preserves
its value or owner semantics; a nonempty undeclared alternative or invalid native tag throws
`CgalUnknownResultException`. The native adapter destroys every temporary and polymorphic container
exactly once after transfer or failure; it never exposes `std::variant`, `boost::any`, or a borrowed
temporary across the ABI.

## Constraints and risks

- Governing constraints: follow [ADR-002](../../adr/ADR-002-cpp-bindings-platform.md),
  [ADR-003](../../adr/ADR-003-native-function-table-bootstrap.md),
  [ADR-004](../../adr/ADR-004-provider-extension-and-cgal-profile.md), and GEN-01 through GEN-05.
  “As complete as possible” means complete relative to the deterministic finite profile and never
  authorizes an unproved layout, transport, invocation, ownership, exception, or dependency path.
- Public and compatibility constraints: new package and namespace contracts are public; profile
  identity, kernel choice, generated native/managed pairing, exception mapping, and supported
  template instances must be deterministic and documented. A clean package consumer requires only
  compatible Windows, x64 .NET, and system runtime components; the package carries every imported
  non-system DLL and applicable CGAL/GMP/MPFR/dependency notices. CGAL licensing and optional
  package dependencies remain visible to package consumers.
- Material risks and recovery: template expansion can create extreme compile time, code size,
  ambiguous overloads, layout diversity, and optional dependency failures. Admission is incremental
  and evidence-driven; a failing specialization is rejected narrowly with diagnostics, while the
  last verified profile remains the recovery surface.
- Escalation triggers: EPICK cannot supply a useful 2D/3D package; a new public ownership category,
  shared Runtime feature, ABI, platform, or non-finite template rule is required; CGAL requires a
  Shared change beyond the approved provider contract; or the three package responsibilities cannot
  be implemented and proved through a coherent work-item map.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../establish-provider-extension-boundary/change.md contract=AC-01 -->
<!-- change-prerequisite: PRE-02 source=../establish-provider-extension-boundary/change.md contract=AC-03 -->
| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | Shared exposes the approved provider-neutral semantic model and paired emitter boundary | `../establish-provider-extension-boundary/change.md`, AC-01 | Source contract is completed on the selected Git baseline |
| PRE-02 | The approved shared/provider topology and dependency direction are mechanically enforced | `../establish-provider-extension-boundary/change.md`, AC-03 | Source contract is completed on the selected Git baseline |

<!-- section: delivery-brief -->
## Delivery brief

This Controlled change requires multiple independently verifiable deliveries for the Generator and
profile inventory, the provider Runtime contract, and the compiled Windows package with real native
consumption. After contract approval and continuation, `plan-work-items` will create the smallest
dependency-aware map for separate approval; it must not duplicate these acceptance contracts.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-03 purpose=boundary shape=integration -->
<!-- primary-proof: AC-04 purpose=journey shape=end-to-end -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Repeated real-header generation yields identical complete profile inventories and paired output identities | Run the CGAL Generator TUnit/integration project with `dotnet run ... -- --report-trx` against the pinned vcpkg triplet |
| AC-02 | Primary | A compiled generated wrapper proves every mapped failure category, empty/known/unknown result partition, and exact-once cleanup without OCCT dependencies | Run the CGAL native-boundary integration project with `dotnet run ... -- --report-trx` and its compiled fixture on the pinned toolchain |
| AC-03 | Primary | The locked native wrapper compiles and the packed artifact contains every admitted managed/native member, recursive non-system DLL import, and required notice | Run the CGAL Windows package verifier against the pinned manifest and toolchain |
| AC-04 | Primary | A clean packed-package consumer observes exact results 25, 9, Point_2(1,0), none, and `CgalPreconditionException` for the specified calls | Run the packaged CGAL Windows consumer executable prepared by the package verifier |
| AC-02 | Conditional | Focused pure Runtime tests cover public exception properties, inheritance, fallback mapping, malformed UTF-8, and exact-once cleanup | Run the CGAL Runtime TUnit project with `dotnet run ... -- --report-trx` |
| AC-01–AC-04 | Conditional | Existing OCCT focused tests and the complete repository build still pass with both providers enabled | Run the OCCT TUnit/package gates, then `dotnet run --project Build/Build.csproj -c Release` |

<!-- section: completion-criteria -->
## Completion

Complete when AC-01 through AC-04 pass on the integrated exact candidate, all approved work items are
verified, required independent implementation review accepts the public API, template admission,
exception/lifetime, packaging, and test adequacy, the full OCCT regression gates pass, and current
architecture plus consumer documentation records the implemented profile and licensing/dependency
boundary. Completion does not authorize record deletion; a later explicit cleanup continuation may
delete it and its work-item map only after the repository change-retention preconditions are
independently satisfied.
