# Isolate native assets in Windows provider packages

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: 2026-09-07 user message "批准并继续。" -->
<!-- candidate-binding: commit:ed33774535d93bd2b6f4c24dc4de5c4e5ea51295 -->

<!-- section: goal-rationale -->
## Goal and rationale

Make each Windows provider NuGet contain its own binding module and exactly the non-system runtime
DLL closure that module imports. This prevents an unrelated port installed in the same vcpkg
triplet from changing package contents and establishes the isolation required before Manifold and
FCL packages are added.

<!-- section: scope -->
## Scope and non-goals

- In scope: reusable recursive Windows import discovery and staging; OCCT migration away from the
  vcpkg `bin\*.dll` package wildcard; preservation of the existing CGAL closure; cache invalidation
  from the selected native inputs; and verification that independently derives the packaged closure.
- Non-goals: static-linking every dependency into one DLL; changing generated APIs; changing OCCT
  or CGAL declaration coverage; adding Manifold or FCL; introducing cross-provider geometry
  conversions; publishing packages to a remote feed.
- Compatibility: managed assembly, package, namespace, native binding basename, function-table,
  ownership, exception, and native-call behavior remain unchanged. Removing unrelated DLLs from a
  package is intentional; a consumer that depended on an unrelated transitive native file was
  outside the supported provider boundary.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | OCCT Windows NuGet native assets | Includes every DLL in the configured vcpkg triplet `bin` directory | Includes the OCCT binding DLL and exactly its recursive non-system import closure | OCCT package identity and runtime behavior |
| OB-02 | Provider package collision handling | No common check proves that overlapping native filenames are equivalent | A tested package set accepts overlapping filenames only when SHA-256 hashes match | Providers have no package dependency on one another |
| OB-03 | Native package build storage | Verification may retain large provider-specific intermediate trees | Verification runs at most one provider build and one compiler worker at a time, isolates task-owned scratch, and enforces the configured free-space and scratch-size guards between phases | Pre-existing outputs are neither counted as task scratch nor deleted; final package and compact evidence remain available |

<!-- acceptance-case: AC-01 -->
### AC-01 — Package contains the exact native dependency closure

```gherkin
Scenario: Build an independently consumable Windows provider package
  Given the provider binding DLL and its pinned native installation
  When the Windows NuGet package is produced
  Then an independent traversal from the packaged binding DLL finds exactly every other packaged non-system DLL and no unresolved import
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Co-installed native assets are deterministic

```gherkin
Scenario: Restore the existing Windows provider packages into one consumer
  Given locally built OCCT and CGAL Windows packages
  When one isolated consumer restores and runs both packages
  Then every overlapping native filename is byte-identical and both provider calls succeed
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Differing same-name assets fail closed

```gherkin
Scenario: Two provider packages contain different bytes under one native filename
  Given an isolated package-set fixture with a same-name native asset whose SHA-256 hashes differ
  When package coexistence verification runs
  Then verification fails before the consumer executes and identifies the conflicting filename and packages
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Native packaging is bounded and non-destructive

```gherkin
Scenario: Reach a guarded native package phase
  Given the configured free-space floor or task-owned scratch budget is exceeded before that phase
  When verification checks the boundary before generation, compilation, packing, or consumer execution
  Then it stops before starting the phase and reports the measured free and scratch space

Scenario: Run provider verification with pre-existing ignored outputs
  Given provider builds are scheduled together and a sentinel exists outside the task-owned scratch root
  When package verification completes or fails
  Then providers and compiler workers ran serially, the sentinel is unchanged, and only the task-owned root was eligible for cleanup

Scenario: Complete package verification successfully
  Given every guarded phase remains within its limits
  When the consumer proof finishes
  Then the final NuGet packages and compact hash/result evidence remain available without retaining disposable extraction and restore-cache trees
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Selected native input changes invalidate cached packaging

```gherkin
Scenario: A selected app-local dependency changes
  Given a previously authenticated provider output and a changed hash for one DLL in its derived import closure
  When cache validity is evaluated
  Then the prior output is rejected and the dependency closure and package evidence are regenerated
```

## Constraints and risks

- [ADR-005](../../adr/ADR-005-provider-native-package-isolation.md) requires recursive import
  closure, unique binding basenames, and byte-identical overlapping assets.
- The staged closure must distinguish Windows system imports from app-local dependencies and fail
  closed when an import cannot be resolved.
- Native allocation, cleanup, and exception payloads must continue to use the provider module that
  produced them.
- The implementation must not delete pre-existing ignored outputs or caches. Its own disposable
  scratch paths may be cleaned only after their resolved paths are validated beneath the intended
  provider workspace.
- Package isolation verification runs no more than one provider build at a time and invokes native
  compilation with one compiler worker. The default floor is 25 GiB free and the task scratch
  budget is 12 GiB.
- Task scratch consists only of the verification-owned report root and its generated sources,
  native build, local feed, extracted packages, restore cache, and consumer directories. Existing
  repository `out`, `output`, `artifacts`, vcpkg installations, and download caches are not charged
  to that run and are never deleted by it.
- The guard measures free space and task scratch before generation, native compilation, packing,
  and consumer execution. Crossing either limit stops before the next phase and reports both
  measurements; it does not claim to interrupt a compiler safely in the middle of one phase.
- Cleanup is restricted to resolved disposable children of the task-owned report root. Successful
  verification retains the built NuGet packages and compact result, dependency, and hash evidence;
  extracted packages, the isolated consumer, and its task-owned restore cache are disposable.
- Escalate for a changed native basename, public package dependency, provider ABI, linkage model,
  inability to derive a complete closure, or a same-name dependency with differing bytes.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline once this Draft is approved.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: shared Windows-native staging and verification behavior used by
  provider package build scripts, with OCCT consuming the same exact-closure rule already embodied
  by CGAL.
- Other real start conditions: PowerShell 7.5, the supported MSVC inspection tools, and the pinned
  provider native installations are required for full boundary proof.
- Likely touchpoints (non-binding): `Build` package-generation and verification scripts, the OCCT
  Windows project, provider-boundary checks, package-consumer fixtures, and current architecture
  documentation.
- Private implementation choices left open: helper file boundaries, manifest schema details,
  scratch naming, and whether existing CGAL logic is extracted or adapted behind a common command.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=boundary shape=integration -->
<!-- primary-proof: AC-02 purpose=boundary shape=end-to-end -->
<!-- primary-proof: AC-03 purpose=boundary shape=integration -->
<!-- primary-proof: AC-04 purpose=structural shape=integration -->
<!-- primary-proof: AC-05 purpose=regression shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | An import traversal derived independently from each packaged OCCT and CGAL binding root equals that package's DLL inventory, excluding only proved system imports | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1` |
| AC-02 | Primary | One isolated consumer restores locally built OCCT and CGAL packages, rejects differing duplicate names, and calls both providers | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1` |
| AC-03 | Primary | A differing same-name native asset fixture fails before consumer execution and reports the conflicting filename and package identities | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1 -ExerciseCollisionGuard` |
| AC-04 | Primary | Guard fixtures stop before over-budget phases; execution evidence shows one provider/compiler worker at a time; cleanup cannot cross the owned root or alter a sentinel; successful runs retain only packages and compact evidence | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1 -ExerciseDiskGuard` |
| AC-05 | Primary | Changing a fixture dependency selected by the derived closure invalidates the prior fingerprint and produces restaged dependency/package evidence | `pwsh -NoProfile -File Build/VerifyNativePackageIsolation.ps1 -ExerciseCacheInvalidation` |
| Existing provider regression | Conditional | Existing OCCT and CGAL package consumers still pass their native behavior assertions | `pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1`; `pwsh -NoProfile -File Build/VerifyCgalWindowsPackage.ps1` |
| Repository structure | Conditional | Provider dependency direction and the managed test gate remain valid | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1`; `pwsh -NoProfile -File Build/VerifyManagedTestGate.ps1` |

<!-- section: completion-criteria -->
## Completion

Complete when AC-01 through AC-05 and applicable existing-provider regressions pass on one exact
candidate, ADR-005 is reflected in current architecture documentation, no unrelated native asset
remains in the OCCT package, current provider notices remain packaged, cache fingerprints cover the
selected native inputs, compact verification evidence records package and dependency hashes,
and no required operational handoff remains. Remove this completed change record after its durable
rules and verification coverage are retained elsewhere in the repository.
