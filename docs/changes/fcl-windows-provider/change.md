# Deliver the FCL Windows binding provider and four-provider coexistence

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: 2026-09-08 user message "批准。" approving the revised AC-03 -->
<!-- candidate-binding: commit:8437ca279905fcc04eebb3d48db38f2a66065acf -->

<!-- section: goal-rationale -->
## Goal and rationale

Deliver a generated, independently consumable FCL 0.7.0 Windows provider for the triangle-mesh
continuous-collision behavior used by `KitchenSink.Geometry.Fcl`, and prove that the final OCCT,
CGAL, Manifold, and FCL Windows packages can be installed and called in one process without package
or native-module interference.

<!-- section: scope -->
## Scope and non-goals

- In scope: a versioned finite FCL Generator profile; provider-specific Runtime and Windows NuGet
  packages; compiler-proved xyz and triangle-index transport; owned
  `BVHModel<OBBRSS<double>>` construction with provider validation and explicit FCL build status; fixed-self,
  linearly-translated-other conservative-advancement collision; collision flag and time of contact;
  native exception projection; exact native dependency closure; notices; standalone verification;
  and final four-provider coexistence verification.
- Non-goals: binding the complete FCL/Eigen/Octomap API; exposing `std::vector`, Eigen matrices,
  `CollisionObject`, transforms, contact transforms, callbacks, or KitchenSink geometry types;
  reverse mesh conversion; provider-to-provider geometry or collection conversions; Linux or other
  architectures; and publishing to a remote NuGet feed.
- Compatibility: existing OCCT, CGAL, and Manifold package identities and behavior remain unchanged.
  The application converts its mesh arrays to the provider-local scalar/index transport. The new
  result preserves native `is_collide` in addition to `time_of_contact`; the selected FCL algorithm
  reports both an exact endpoint hit and a miss as `false, 1`, and the package documents rather than
  hides that native ambiguity.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | FCL binding generation | No FCL provider or finite generation profile exists | The locked `fcl-0.7.0-obbrss-double-windows-v1` profile emits one matched managed/native artifact set and complete deterministic inventories | Shared remains provider-neutral and the supported surface is finite |
| OB-02 | Triangle-mesh BVH construction | KitchenSink uses heap wrappers, temporary native vectors, and discards FCL build statuses | A provider-local bulk factory validates lengths and indices, copies inputs, returns the exact native BVH code for operations FCL executes, and yields an owned model only on BVH_OK | Native `size_t` triangle indices, call-scoped borrowing, and same-module destruction are preserved |
| OB-03 | Continuous collision | KitchenSink sets request fields ignored by the selected OBBRSS path and exposes only time | A narrow parameterless-profile query fixes the first model, linearly translates the second, uses FCL conservative advancement, confirms its FCL 0.7 pure-translation false negatives with FCL's exact translation solver, and returns collision plus time | Ignored controls are not exposed as effective inputs and endpoint ambiguity is documented |
| OB-04 | Four-provider package set | No FCL package participates in provider isolation verification | Separate OCCT, CGAL, Manifold, and FCL Windows packages restore and execute together after deterministic collision validation | No provider package depends on another provider or supplies cross-provider conversions |

<!-- acceptance-case: AC-01 -->
### AC-01 — The finite Generator profile is reproducible

```gherkin
Scenario: Generate the supported FCL profile
  Given FCL 0.7.0#5 and its locked dependency/toolchain identity
  When the `fcl-0.7.0-obbrss-double-windows-v1` Generator creates the profile plan twice
  Then both plans expose every declared BVH code name and value plus the same admitted, unsupported, layout, ownership, managed, native, export, and toolchain inventories and byte-identical generated sources
```

<!-- acceptance-case: AC-02 -->
### AC-02 — BVH construction preserves layout, status, and ownership

```gherkin
Scenario: Construct valid and invalid triangle-mesh models
  Given xyz doubles and native-width unsigned CCW triangle indices
  When the provider builds an OBBRSS double-precision BVH model
  Then mismatched triple lengths fail with ArgumentException, an index outside the vertex range fails with ArgumentOutOfRangeException before FCL execution, valid input returns BVH_OK with one independent owned model, and empty input returns BVH_ERR_BUILD_EMPTY_MODEL without leaking temporary collections or a partial owner

Scenario: Dispose a model owner
  Given a build attempt may construct native BVH storage before returning its status
  When the build succeeds, fails, or a successful managed owner is disposed or finalized
  Then every successfully exposed model and every failed-attempt temporary is destroyed exactly once by the FCL binding module, no failed result exposes an owner, dispose/finalizer races never destroy twice, and later owner use is rejected
```

<!-- acceptance-case: AC-03 -->
### AC-03 — KitchenSink continuous collision semantics are covered

```gherkin
Scenario: Query linear triangle-mesh continuous collision
  Given two built models and a three-double movement vector
  When the first model remains fixed and the second is linearly translated using FCL conservative advancement with exact FCL translation confirmation for a reported miss
  Then initial overlap reports true with time zero, an impact before the endpoint reports true with time in [0,1), and both exact endpoint contact and no impact report false with time one as the provider profile defines
```

<!-- acceptance-case: AC-04 -->
### AC-04 — The FCL Windows package is standalone and exact

```gherkin
Scenario: Restore only the FCL Windows package
  Given a fresh local NuGet feed containing the generated FCL package family
  When an isolated `net8.0` `win-x64` consumer restores and executes a real BVH collision query
  Then it loads the unique FCL binding module, its packaged DLL inventory equals the independently derived recursive non-system import closure, notices for FCL, libccd, Eigen, Octomap, and any additional profile-selected linked or packaged dependency are present under `third-party-notices`, and no other provider, Generator, or Clang package is restored
```

<!-- acceptance-case: AC-05 -->
### AC-05 — All four provider packages coexist

```gherkin
Scenario: Use OCCT, CGAL, Manifold, and FCL together
  Given independently built Windows packages for the four providers
  When one isolated consumer restores and calls every package
  Then all four native calls succeed and every overlapping native filename is byte-identical before execution
```

## Constraints and risks

- [ADR-005](../../adr/ADR-005-provider-native-package-isolation.md) governs unique binding names,
  exact closure, collision handling, serial native builds, authenticated caches, and owned cleanup.
- The default profile is FCL 0.7.0#5, ccd 2.1#4, Eigen 5.0.1, and Octomap 1.10.0 on vcpkg
  baseline `30ef65cad98f08e7197c9a1656fbd871bcb72f2d`, `x64-windows`, CMake 4.4.3,
  and MSVC 19.51.36256, with stable profile ID
  `fcl-0.7.0-obbrss-double-windows-v1`. Any profile identity change is an escalation trigger.
- The compiler must prove Eigen vector transport, `size_t` triangle-index conversion, model
  size/alignment, and Value/Owned classification. The nontrivial BVH model is RAII-owned; its
  static FCL implementation does not justify treating it as a managed value or pointer owner.
- The public namespace is `TedToolkit.CppBindings.Fcl`. `FclVector3` contains compiler-proved x/y/z
  doubles. `FclBvhReturnCode : int` exposes BVH_OK=0 and the exact native codes -1 through -8. A
  `FclModelBuildResult` exposes that native code and an `Owned<FclBvhModel>` only for BVH_OK.
- `FclBvhModel.Create` accepts copied `ReadOnlySpan<double>` xyz values and
  `ReadOnlySpan<nuint>` CCW triangle indices. Lengths that are not multiples of three throw
  `ArgumentException`; an index not less than the vertex count throws
  `ArgumentOutOfRangeException` before FCL executes. No span or pointer is retained. Empty spans
  reach FCL and return BVH_ERR_BUILD_EMPTY_MODEL. Other native BVH codes remain exact status data.
- `FclContinuousCollision.Query` accepts two borrowed same-provider owners and an `FclVector3`
  translation and returns `FclContinuousCollisionResult` with `bool IsCollide` and
  `double TimeOfContact`. Its primary query preserves `CCDM_LINEAR`,
  `CCDC_CONSERVATIVE_ADVANCEMENT`, and the profile's default GJK solver. FCL 0.7 can normalize a
  zero separation vector and lose a pure-translation OBBRSS mesh hit, so a reported miss is
  confirmed by FCL's `CCDM_TRANS` polynomial solver; only a confirmed contact with `toc < 1` is
  promoted. The selected OBBRSS conservative-advancement dispatch ignores request
  iteration/tolerance fields, so this profile exposes neither as an effective input. Endpoint
  contact and a miss both return `false, 1`. Contact transforms remain outside this profile.
- Native C++ failures map through the provider error carrier to public `FclArgumentException`,
  `FclArgumentOutOfRangeException`, `FclOutOfMemoryException`, `FclException`, and
  `FclUnknownException`, then the same module clears copied diagnostics. Managed span validation is
  distinct from native BVH status data.
- The FCL static library is large and may import shared ccd or runtime DLLs only through the final
  binding. Builds are serial, use one compiler worker, enforce the 25 GiB free-space floor and
  12 GiB scratch ceiling, and derive the closure after linking rather than hardcoding DLL names.
- Escalate for a broader FCL public surface, different result/status policy, callback or retained
  buffer model, cross-provider dependency/conversion, changed linkage/profile, or failure to prove
  native layout, closure, and same-module lifetime.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../native-package-dependency-closure/change.md contract=AC-01 -->
<!-- change-prerequisite: PRE-02 source=../native-package-dependency-closure/change.md contract=AC-02 -->
<!-- change-prerequisite: PRE-03 source=../native-package-dependency-closure/change.md contract=AC-03 -->
<!-- change-prerequisite: PRE-04 source=../native-package-dependency-closure/change.md contract=AC-04 -->
<!-- change-prerequisite: PRE-05 source=../native-package-dependency-closure/change.md contract=AC-05 -->
<!-- change-prerequisite: PRE-06 source=../manifold-windows-provider/change.md contract=AC-05 -->
| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | Exact recursive native package closure | `../native-package-dependency-closure/change.md`, AC-01 | Source contract is completed on the selected Git baseline |
| PRE-02 | Deterministic overlapping native assets | `../native-package-dependency-closure/change.md`, AC-02 | Source contract is completed on the selected Git baseline |
| PRE-03 | Fail-closed differing asset collision handling | `../native-package-dependency-closure/change.md`, AC-03 | Source contract is completed on the selected Git baseline |
| PRE-04 | Bounded serial native packaging and owned cleanup | `../native-package-dependency-closure/change.md`, AC-04 | Source contract is completed on the selected Git baseline |
| PRE-05 | Native input cache authentication | `../native-package-dependency-closure/change.md`, AC-05 | Source contract is completed on the selected Git baseline |
| PRE-06 | A verified Manifold package that coexists with OCCT and CGAL | `../manifold-windows-provider/change.md`, AC-05 | Source contract is completed on the selected Git baseline |

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target area: add the FCL provider family under `src/providers/fcl`, focused tests,
  Windows generation/package verification, solution/build integration, final four-provider
  consumer coverage, and current provider documentation.
- One embedded delivery is sufficient because the public managed/native pair, package, collision
  result, and final coexistence proof must advance together and are not independently releasable.
- Private implementation choices left open: internal profile/catalog decomposition, internal
  generated symbols, compiler-probe implementation, and test-project organization. Prefer
  provider-specific typed spans over widening Shared solely for FCL.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=structural shape=integration -->
<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-03 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-04 purpose=boundary shape=end-to-end -->
<!-- primary-proof: AC-05 purpose=boundary shape=end-to-end -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Two locked-profile plans have identical inventories, exports, sources, layout, ownership, and toolchain identity | `dotnet run --project tests/TedToolkit.CppBindings.Fcl.Generator.Tests/TedToolkit.CppBindings.Fcl.Generator.Tests.csproj -c Release -- --report-trx` |
| AC-02 | Primary | Validation is distinct from exact FCL build codes; success exposes one owner, failure exposes none, and successful or temporary native models are each destroyed exactly once | `pwsh -NoProfile -File Build/VerifyFclWindowsPackage.ps1` |
| AC-03 | Primary | Real initial-overlap, pre-endpoint impact, endpoint, and miss cases preserve the selected algorithm's exact collision-flag/time pairs | `pwsh -NoProfile -File Build/VerifyFclWindowsPackage.ps1` |
| AC-04 | Primary | Isolated restore proves the exact package graph, native closure, notices, and real query | `pwsh -NoProfile -File Build/VerifyFclWindowsPackage.ps1` |
| AC-05 | Primary | One isolated consumer verifies collisions and calls all four provider packages | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1 -Providers Occt,Cgal,Manifold,Fcl` |
| Existing provider regression | Conditional | OCCT, CGAL, and Manifold behavior remains green | Their provider package verification commands |
| Repository structure | Conditional | Provider dependency direction, solution membership, managed gate, and serial package rules include FCL | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1`; `pwsh -NoProfile -File Build/VerifyManagedTestGate.ps1` |

<!-- section: completion-criteria -->
## Completion

Complete when AC-01 through AC-05 pass on one exact candidate, the FCL Generator/Runtime/Windows
packages and current documentation describe the profile, status, collision, and ownership
obligations, required notices and compact package/hash evidence are retained, independent
implementation review is Ready, and no operational handoff remains. Record cleanup is a later
explicit continuation after merge; remove the FCL referrer before its unreferenced prerequisites.
