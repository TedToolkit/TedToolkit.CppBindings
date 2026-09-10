# Centralize Windows binding generation in one C# tool

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-preserving-refactor -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P2
<!-- approval-source: maintainer approved the centralized implementation and explicitly selected src/tools/TedToolkit.CppBindings.Windows.Generation.Tool in the Codex task on 2026-09-09; independent design review found the behavioral contract ready -->
<!-- candidate-binding: workspace:c44a72aa98d44b03440a359ce4bb4bc2de080f49:sha256:8c3e8ba1b29f3b0bff807ef24ddb2bcf6a3c495376e9ee6dfd5e50bced580503 -->

<!-- section: goal-rationale -->
## Goal and rationale

Give maintainers one typed, discoverable Windows generation host that directly composes every
provider Generator while preserving the independently buildable provider packages and their exact
generated artifact, cache, native-build, and dependency-staging behavior. The current four large
provider-specific generation scripts and four provider-specific executable hosts duplicate
orchestration and obscure the boundary between generation and verification.

<!-- section: scope -->
## Scope and non-goals

- In scope: one non-packable C# generation tool under
  `src/tools/TedToolkit.CppBindings.Windows.Generation.Tool`; direct project references to
  the OCCT, CGAL, Manifold, and FCL Generator projects; migration of Windows source generation,
  native compilation, cache, dependency closure, and notice staging; provider `.Windows` build
  integration; removal of the four `Generate*.ps1` scripts, three redundant provider Generator Tool
  projects, and the superseded OCCT Console generation host; a benchmark-only OCCT host that keeps
  raw generation measurements outside the canonical build workflow; solution, architecture,
  contributor documentation, and structural verification updates.
- Non-goals: changing generated public APIs, provider declaration profiles, native dependency
  versions, package identities, output locations, CI vcpkg cache policy, or converting independent
  package and test verification scripts to C#.
- Compatibility or deliberately preserved behavior: each provider `.Windows.csproj` remains an
  independently buildable and packable entry point; existing verification commands and generated
  package consumers retain their inputs and observable results.
- Candidate composition: the frozen workspace bundle also contains the separately approved
  `upgrade-windows-provider-toolchain` migration. Its v2 profile identities and compiler locks are
  owned and proved by that companion record; they are not effects of the generation-host refactor.

<!-- section: invariants -->
## Preserved invariants

<!-- preserved-invariant: INV-01 -->
- INV-01: Building any OCCT, CGAL, Manifold, or FCL Windows provider independently still produces
  the same managed `.cs` file count and byte-content fingerprint recorded before the refactor, plus
  its existing generation inventories and output paths. Native DLL byte equality is deliberately
  excluded because compiler output is not the semantic authority; each package must instead pass
  its independent native behavior, dependency-closure, notice, and isolated-consumer verification.

<!-- preserved-invariant: INV-02 -->
- INV-02: For every provider descriptor, generation still rejects output roots outside
  repository-owned output/evidence trees, serializes writers per output root, invalidates changed
  or deleted inputs and missing, empty, or modified managed/native/dependency/notice outputs,
  enforces the disk guard before generation and native compilation, publishes no success stamp on
  failure, restores imported compiler environment, and reuses only an authenticated complete hit.

<!-- preserved-invariant: INV-03 -->
- INV-03: Generator libraries remain the authorities for provider semantics, the new host and all
  superseded hosts remain outside consumer package graphs, and verification remains independent of
  the canonical generation implementation.

## Constraints and risks

- Governed by
  `docs/architecture/generated-binding-system.md@0b369144c6f9c69962c5e97eaba027afe1370875`:
  one non-packable build-time application composes providers; provider projects reference only its
  executable output; `Build.csproj` remains the top-level pipeline; source emission authority stays
  in provider Generators.
- No public or persisted contract, migration, security boundary, release operation, or external
  deployment changes.
- The material risk is a false cache hit or incomplete native runtime closure after translation
  from PowerShell. Recovery is to restore the former generation scripts and project references as
  one repository revision; no persisted consumer data requires migration.
- Concurrent FCL and Manifold vcpkg source-inventory changes are independent worktree collisions,
  not logical prerequisites. Their edits must be preserved and the implementation must consume the
  final Generator APIs and fingerprint their actual header and package-list inputs.
- Escalation triggers: a generated/public API change, output or package layout change, weakened
  cache/closure/toolchain validation, a new platform or RID, a dependency-direction exception, or
  a need for more than one independently releasable delivery boundary.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline. Windows verification requires the configured `VCPKG_ROOT`
and installed provider dependencies.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: one canonical Windows generation tool under `src/tools/`,
  four provider build integrations, and removal of the superseded generation entry points,
  including the OCCT Console and three provider Generator Tool projects.
- Other real start conditions or resource prerequisites: .NET 10, PowerShell 7.5 for retained
  verification, CMake, Visual Studio C++ tools, and the existing `C:\vcpkg` installation.
- Likely touchpoints (non-binding): `src/tools/`, `Build/`, the four provider `.Windows.csproj`
  files, the three provider Generator Tool directories, `benchmarks/`,
  `TedToolkit.CppBindings.slnx`, architecture and README files, and provider-boundary verification.
- Private implementation choices left open: internal type decomposition, process wrapper details,
  command-line parsing, provider descriptor representation, and focused test organization.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: INV-01 purpose=boundary shape=integration -->
<!-- primary-proof: INV-02 purpose=regression shape=component -->
<!-- primary-proof: INV-03 purpose=structural shape=component -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| INV-01 | Primary | All four standalone Windows builds match the approved pre-refactor managed-source count/fingerprint and pass their independent package behavior, closure, notice, and consumer assertions | Build each provider `.Windows.csproj` in Release; run `pwsh -NoProfile -File Build/VerifyWindowsGenerationOutputs.ps1 -Configuration Release -BaselinePath docs/changes/centralize-windows-generation/evidence/windows-generation-baseline.json`; then run `Build/VerifyWindowsPackage.ps1`, `Build/VerifyCgalWindowsPackage.ps1`, `Build/VerifyManifoldWindowsPackage.ps1`, and `Build/VerifyFclWindowsPackage.ps1` with fresh report directories |
| INV-02 | Primary | The shared host's focused scenarios cover all provider descriptors, bounded-root rejection, same-root serialization, both disk-guard phases, complete hits, every named invalidation input/output, failure stamp suppression, and environment restoration | `pwsh -NoProfile -File Build/VerifyGenerationCache.ps1` |
| INV-03 | Primary | Exactly one build-time generation host references all four Generators, no provider package consumes it, no `Build/Generate*.ps1` or redundant provider Generator Tool remains, and the benchmark-only OCCT host remains buildable without becoming a generation entry point | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1`; `dotnet build benchmarks/TedToolkit.CppBindings.Occt.BenchmarkHost/TedToolkit.CppBindings.Occt.BenchmarkHost.csproj -c Release` |
| Affected regression | Conditional | Generator and Runtime tests plus the repository build pipeline remain green | `dotnet run --project Build/Build.csproj -c Release` |

## Candidate evidence

- Baseline revision: `c44a72aa98d44b03440a359ce4bb4bc2de080f49`. The frozen combined bundle is
  `out/candidate/centralized-windows-toolchain.patch`; its SHA-256 is
  `8c3e8ba1b29f3b0bff807ef24ddb2bcf6a3c495376e9ee6dfd5e50bced580503` across 72 paths. Both lifecycle
  records are excluded. Forward application to the baseline, reverse application to the verified
  workspace, and the clean real Git index were checked.
- Focused Generator, analyzer, and shared-host tests passed 62/62. The generation-cache verifier
  passed 22/22 shared-host scenarios, and the provider-boundary verifier passed all structural,
  reference, packaging, CI, and negative cases.
- `Build/VerifyWindowsGenerationOutputs.ps1` passed strict managed-source byte comparison for all
  four providers. Its evidence baseline is bound to the actual implementation baseline `c44a72a`;
  the Manifold fingerprint is independently corroborated by the toolchain migration's source-only
  per-file baseline after the intervening approved native-error unification.
- Fresh OCCT, CGAL, Manifold, and FCL package/closure/notice/runtime/isolated-consumer verification
  passed under `out/verification/centralized-final-*-package-20260910`. The benchmark-only OCCT host
  also built independently with zero warnings and errors.
- The complete repository pipeline passed all eight enabled modules in 44 minutes 23 seconds. Its
  managed gate passed 298 tests with zero failures or skips, the full OCCT native build compiled
  and linked all 690 Unity translation units, and all four Windows projects built with zero warnings
  and errors.

## Independent implementation review

- Conclusion: `Ready to merge` for exactly
  `workspace:c44a72aa98d44b03440a359ce4bb4bc2de080f49:sha256:8c3e8ba1b29f3b0bff807ef24ddb2bcf6a3c495376e9ee6dfd5e50bced580503`.
  Independence was established by a fresh read-only reviewer that did not implement the candidate.
- Code correctness, test adequacy, and candidate-bound verification all passed. The reviewer found
  no Blocking or Important findings, suggestions, unrelated paths, design deviations, or missing
  durable documentation.
- The reviewer independently matched all 72 patch entries and 74 pre/post path endpoints to the
  baseline and workspace, reran the 22 shared-host tests and provider-boundary verifier, and built
  the benchmark host with zero warnings and errors. Retained package, migration, pipeline TRX, and
  native-build evidence was checked from primary artifacts.
- Durable extraction is complete in current architecture, ADR, repository, provider, and benchmark
  documentation. The active delivery record is eligible for deletion only after merge and reference
  release under repository retention policy.

<!-- section: completion-criteria -->
## Completion

All three invariants pass on one exact candidate, affected package verification remains green,
architecture and contributor documentation describe the current C# generation boundary, no
temporary artifacts remain, and the implementation receives candidate-bound review. After merge
and release of all references, delete this active delivery record under the repository retention
policy; Git history retains it.
