# Deliver the Manifold Windows binding provider

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: 2026-09-08 user message "批准并继续。" -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

Deliver a generated, independently consumable Manifold 3.5.2 Windows provider whose public native
semantics cover the mesh Boolean, translation, status, and mesh-exchange operations used by
`KitchenSink.Geometry.Manifold`. Consumers need this capability without taking dependencies on the
OCCT, CGAL, or FCL providers, and the new package must coexist with the current OCCT and CGAL
packages so a later FCL delivery can prove the final four-provider set.

<!-- section: scope -->
## Scope and non-goals

- In scope: a versioned finite Manifold Generator profile; provider-specific Runtime and Windows
  NuGet packages; owned Manifold lifetime; exact `Error` and `OpType` values; xyz/triangle mesh
  bulk input and output; `Status`, `Boolean`, `Translate`, `NumTri`, and default `GetMeshGL64`
  behavior; native exception projection; exact native dependency closure; notices; and local
  standalone/coexistence verification.
- Non-goals: binding Manifold's entire API; exposing `std::vector`, callbacks, optional MeshGL
  properties, `manifoldc`, or KitchenSink geometry types; provider-to-provider geometry or
  collection conversions; adding a provider-native continuous-collision algorithm; Linux or other
  architectures; and publishing to a remote NuGet feed.
- Compatibility: existing OCCT and CGAL packages, public APIs, native basenames, and runtime behavior
  remain unchanged. The application owns conversion between its mesh model and the provider-local
  scalar/index transport. The KitchenSink continuous-collision behavior remains expressible by
  composing Manifold operations, but its application-specific bisection and one-call optimization
  do not become native Manifold API.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Manifold binding generation | No Manifold provider or generation profile exists | A locked Manifold 3.5.2 `x64-windows` profile emits one matched managed/native artifact set and complete deterministic inventories | Shared remains provider-neutral and the profile is finite rather than a claim over the whole library |
| OB-02 | Managed Manifold semantics | Consumers need a separate handwritten bridge and heap-pointer wrappers | `TedToolkit.CppBindings.Manifold` exposes the locked enums, span-based mesh factory, owned Manifold operations, and owned managed mesh output defined below | Native status is data, borrowed inputs remain call-scoped, and each returned Manifold is an independent owned value |
| OB-03 | Windows packaging and coexistence | No standalone Manifold NuGet participates in provider collision checks | One Manifold Windows NuGet contains its unique binding DLL and exact recursive non-system import closure and runs with OCCT and CGAL in one consumer | No provider-to-provider package dependency or cross-provider conversion is introduced |

<!-- acceptance-case: AC-01 -->
### AC-01 — The finite Generator profile is reproducible

```gherkin
Scenario: Generate the supported Manifold profile
  Given Manifold 3.5.2 from the locked vcpkg baseline and supported Windows toolchain
  When the `manifold-3.5.2-windows-v1` Generator profile creates its plan twice
  Then both plans expose every declared Error and OpType name, value, and native underlying type plus the same admitted, unsupported, layout, ownership, managed, native, export, and toolchain inventories and byte-identical generated sources
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Manifold ownership and native status are preserved

```gherkin
Scenario: Use valid and invalid profile meshes
  Given xyz doubles and unsigned 64-bit CCW triangle indices whose lengths are multiples of three
  When a consumer creates an owned Manifold, reads Status, applies Add, Subtract, Intersect and Translate, reads NumTri, and obtains its mesh
  Then valid values return NoError and provider-owned xyz/index arrays while nonfinite, nonmanifold, and out-of-range-index values expose their exact ManifoldError without an implicit policy exception

Scenario: Dispose a returned Manifold owner
  Given Boolean and translation return independent native values
  When their managed owners are disposed or finalized
  Then every successfully constructed value is destroyed exactly once by the Manifold binding module, incomplete construction is never destroyed, dispose/finalizer races never destroy twice, and later use is rejected
```

<!-- acceptance-case: AC-03 -->
### AC-03 — KitchenSink Manifold functionality is covered

```gherkin
Scenario: Reproduce the KitchenSink mesh solver behavior
  Given canonical tetrahedron and overlapping translated-box fixtures plus a moving mesh
  When a consumer performs Add, Subtract, Intersect, sequential list folds, and the documented Boolean/Translate/NumTri bisection
  Then canonicalized CCW triangle coordinate sets match their fixture oracles within 1e-12, empty list folds preserve the input, initial contact returns zero, an endpoint miss returns NaN, and a detected contact is within the supplied length tolerance after at most 100 iterations
```

<!-- acceptance-case: AC-04 -->
### AC-04 — The Windows package is standalone and exact

```gherkin
Scenario: Restore only the Manifold Windows package
  Given a fresh local NuGet feed containing the generated Manifold package family
  When an isolated `net8.0` `win-x64` consumer restores and executes a real Manifold call
  Then it loads the unique Manifold binding module, its packaged DLL inventory equals the independently derived recursive non-system import closure, the Manifold, Clipper2, TBB, and any additional profile-selected linked or packaged dependency notices are present under `third-party-notices`, and no OCCT, CGAL, FCL, Generator, or Clang package is restored
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Manifold coexists with the existing providers

```gherkin
Scenario: Use OCCT, CGAL, and Manifold together
  Given locally built Windows packages for all three providers
  When one isolated consumer restores and calls each package
  Then all native calls succeed and every overlapping native filename is byte-identical before execution
```

## Constraints and risks

- [ADR-005](../../adr/ADR-005-provider-native-package-isolation.md) requires a unique binding
  basename, recursive non-system import closure, byte-identical overlapping assets, serial native
  builds, authenticated caches, and task-owned disk cleanup.
- The default profile is Manifold 3.5.2 on vcpkg baseline
  `30ef65cad98f08e7197c9a1656fbd871bcb72f2d`, `x64-windows`, CMake 4.4.3, and MSVC
  19.51.36256, with stable profile ID `manifold-3.5.2-windows-v1`. Any profile identity
  change is an escalation trigger.
- The compiler must prove sizes, alignments, enum values, and Value/Owned classification. The
  nontrivial Manifold type is RAII-owned; value returns must never become borrowed pointers or
  OCCT-style intrusive handles.
- The public namespace is `TedToolkit.CppBindings.Manifold`. `ManifoldOp : sbyte` contains Add=0,
  Subtract=1, Intersect=2. `ManifoldError : int` contains, in order from zero, NoError,
  NonFiniteVertex, NotManifold, VertexOutOfBounds, PropertiesWrongLength,
  MissingPositionProperties, MergeVectorsDifferentLengths, MergeIndexOutOfBounds,
  TransformWrongLength, RunIndexWrongLength, FaceIDWrongLength, InvalidConstruction,
  ResultTooLarge, InvalidTangents, and Cancelled. The compiler verifies the native `char` and
  default-enum underlying types and every numeric value.
- `Manifold.Create` accepts `ReadOnlySpan<double>` xyz properties and
  `ReadOnlySpan<ulong>` triangle indices; both lengths must be multiples of three or an
  `ArgumentException` is thrown before native execution. Inputs are copied during the call and
  never retained. `ManifoldMeshData` owns fresh managed `double[]` xyz and `ulong[]` index arrays,
  exposes only three position properties, and preserves CCW index triples. `NumTri` projects the
  native `size_t` as `nuint` without narrowing.
- The generated `Manifold` RAII record is used through `Owned<Manifold>` and exposes Status,
  Boolean with a borrowed same-provider owner and ManifoldOp, Translate with three doubles, NumTri,
  and `GetMesh` returning `ManifoldMeshData`. Boolean and Translate return new
  `Owned<Manifold>` values; argument/receiver borrowing is call-scoped.
- Native C++ failures map through the provider error carrier to public
  `ManifoldArgumentException`, `ManifoldArgumentOutOfRangeException`,
  `ManifoldOutOfMemoryException`, `ManifoldOverflowException`, `ManifoldException`, and
  `ManifoldUnknownException`, then the same module clears copied diagnostics. A non-`NoError`
  Manifold status remains an explicit result, not an automatically thrown exception.
- Full package verification enforces the 25 GiB free-space floor and 12 GiB task-scratch ceiling,
  builds providers and compiler workers serially, and retains only packages and compact evidence.
- Escalate for a different native profile, public callback model, cross-provider type or package
  dependency, changed linkage model, inability to prove exact closure/layout/ownership, or a
  requirement to ship the KitchenSink-specific continuous-collision algorithm as native API.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../native-package-dependency-closure/change.md contract=AC-01 -->
<!-- change-prerequisite: PRE-02 source=../native-package-dependency-closure/change.md contract=AC-02 -->
<!-- change-prerequisite: PRE-03 source=../native-package-dependency-closure/change.md contract=AC-03 -->
<!-- change-prerequisite: PRE-04 source=../native-package-dependency-closure/change.md contract=AC-04 -->
<!-- change-prerequisite: PRE-05 source=../native-package-dependency-closure/change.md contract=AC-05 -->
| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | Exact recursive native package closure | `../native-package-dependency-closure/change.md`, AC-01 | Source contract is completed on the selected Git baseline |
| PRE-02 | Deterministic overlapping native assets | `../native-package-dependency-closure/change.md`, AC-02 | Source contract is completed on the selected Git baseline |
| PRE-03 | Fail-closed differing asset collision handling | `../native-package-dependency-closure/change.md`, AC-03 | Source contract is completed on the selected Git baseline |
| PRE-04 | Bounded serial native packaging and owned cleanup | `../native-package-dependency-closure/change.md`, AC-04 | Source contract is completed on the selected Git baseline |
| PRE-05 | Native input cache authentication | `../native-package-dependency-closure/change.md`, AC-05 | Source contract is completed on the selected Git baseline |

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target area: add the Manifold provider family under `src/providers/manifold`, its
  focused tests, Windows generation/package verification, solution/build integration, package
  coexistence coverage, and current provider documentation.
- One embedded delivery is sufficient because the public managed/native pair, package, and boundary
  proof must advance together and are not independently releasable.
- Private implementation choices left open: internal profile/catalog decomposition, internal
  generated symbols, compiler-probe implementation, and test-project organization. Prefer a
  provider-specific span facade over widening Shared solely for Manifold.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=structural shape=integration -->
<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-03 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-04 purpose=boundary shape=end-to-end -->
<!-- primary-proof: AC-05 purpose=boundary shape=end-to-end -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Two locked-profile plans have identical inventories, exports, sources, layout, ownership, toolchain identity, and the complete enum catalog/ABI | `dotnet run --project tests/TedToolkit.CppBindings.Manifold.Generator.Tests/TedToolkit.CppBindings.Manifold.Generator.Tests.csproj -c Release -- --report-trx` |
| AC-02 | Primary | Valid/invalid meshes preserve exact status, public bulk transport, native-width count, and successful/failed/concurrent owner destruction cardinality | `pwsh -NoProfile -File Build/VerifyManifoldWindowsPackage.ps1` |
| AC-03 | Primary | Canonical triangle-coordinate oracles and explicit zero/NaN/tolerance assertions reproduce every KitchenSink partition | `pwsh -NoProfile -File Build/VerifyManifoldWindowsPackage.ps1` |
| AC-04 | Primary | Isolated restore proves the exact package graph, native closure, named/derived notice inventory, and real call | `pwsh -NoProfile -File Build/VerifyManifoldWindowsPackage.ps1` |
| AC-05 | Primary | A combined consumer calls OCCT, CGAL, and Manifold after collision comparison | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1 -Providers Occt,Cgal,Manifold` |
| Existing provider regression | Conditional | Existing OCCT and CGAL package behavior remains green | `pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1`; `pwsh -NoProfile -File Build/VerifyCgalWindowsPackage.ps1` |
| Repository structure | Conditional | Provider dependency direction, solution membership, managed gate, and serial package rules include Manifold | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1`; `pwsh -NoProfile -File Build/VerifyManagedTestGate.ps1` |

<!-- section: completion-criteria -->
## Completion

Complete when AC-01 through AC-05 pass on one exact candidate, the Manifold Generator/Runtime/Windows
packages and current documentation describe the supported profile and ownership obligations,
required notices and compact package/hash evidence are retained, independent implementation review
is Ready, durable provider documentation is current, and no operational handoff remains. Record
cleanup is a later explicit continuation after merge and after no tracked dependent references it.
