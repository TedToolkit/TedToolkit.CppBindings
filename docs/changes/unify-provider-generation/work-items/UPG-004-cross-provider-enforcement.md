# UPG-004: Enforce the final one-model provider boundary

<!-- work-item-format: 2 -->
<!-- work-item-id: UPG-004 -->

<!-- approval-source: maintainer approved the revised delivery map and explicitly continued in the Codex task on 2026-09-09 -->

## Outcome

Repository verification and current documentation make the completed four-provider boundary
explicit and prevent a provider from reintroducing a private plan, complete binding renderer,
NativeApi/function table, provider-neutral bootstrap implementation, or common Runtime projection.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public contract: cross-provider structural verification, package
  consumption, architecture, and contributor/provider documentation.
- In scope: enforce the shared dependency and semantic-authority boundary across all providers;
  update references to the common Generator and Runtime APIs; prove four-provider generation/package
  behavior, native-error projection, and coexistence; align the downstream central-host design with
  the uniform provider contract.
- Non-goals: implementing the separate Windows generation host, changing provider semantics, or
  adding another platform/profile.
- Likely touchpoints (non-binding): `Build/VerifyProviderBoundaries.ps1`, managed gate/build pipeline,
  solution/project references, architecture/provider READMEs, and central-host change constraints.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| UPG-001 | FCL and the reusable shared generation contract are verified | UPG-001 is Verified on the selected integration baseline |
| UPG-002 | Manifold is verified on the shared generation contract | UPG-002 is Verified on the selected integration baseline |
| UPG-003 | CGAL and OCCT residual generic emitters are consolidated | UPG-003 is Verified on the selected integration baseline |
| UPG-005 | The common native-error boundary and provider-local extensions are verified | UPG-005 is Verified on the selected integration baseline |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-04 | Owns | Makes the integrated generated/package behavior and one-model boundary observable and durable |

<!-- work-item: delivery-constraints -->
## Constraints

- Verification must inspect source/project structure independently of the generation implementation;
  it must not call an implementation helper to decide whether the implementation is compliant.
- All four provider Windows projects remain independently buildable and packable, and provider
  Generator packages remain isolated from consumer package graphs.
- Verification must allow the same extension number to have different meanings in different Provider
  Runtimes and must reject a Provider override of kinds 0 through 8 or 255.
- The central-host change remains a separate outcome and consumes this verified contract.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-04 | Primary | The repository pipeline builds and exercises all four locked providers with the approved Shared/common and Provider-specific behavior, artifacts, package boundaries, and coexistence | `dotnet run --project Build/Build.csproj -c Release` |
| Structural regression | Conditional | Independent inspection rejects duplicate provider engines and invalid dependency direction | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-04 and structural regression proof pass on the integrated provider migrations.
- Current architecture, provider documentation, solution/build integration, and the downstream
  central-host contract describe and consume the uniform provider boundary.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact integrated candidate revision, changed artifacts, AC-04 proof purpose and shape,
commands, observed counts/results, required Windows resources, provider-by-provider package outcomes,
coexistence result, documentation disposition, and remaining operational limitations.

## Verification result

- Candidate: `0f6c2defc48e391d75a1073465832adb13e2f5fe`.
- Changed artifacts: the provider-boundary gate, native integration/package-isolation gate, Shared
  Generator and Runtime boundary tests, platform architecture, and FCL/Manifold provider guidance.
- AC-04 boundary/integration proof: `dotnet run --project Build/Build.csproj -c Release` completed in
  36 minutes 44 seconds with eight modules passed and the configured formatting module skipped. All four
  Windows projects built with zero warnings and errors; the native Handle fixture passed 1/1; and
  the eleven managed test projects passed 312/312.
- Package evidence: `out/verification/bg-500f78ee/result.json` is bound to the candidate and records
  successful isolated OCCT, CGAL, Manifold, and FCL package verification, ten produced packages,
  exact native closures of 60, 5, 5, and 5 files respectively, and successful real package consumers.
- Coexistence: the combined consumer loaded and called all four packages and observed the approved
  OCCT, CGAL, Manifold, and FCL results.
- Structural proof: `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1` passed for 16 projects,
  21 project references, seven structural negative fixtures, four packaging rules, and four providers.
  Shared Generator tests reject every reserved Provider kind from 0 through 8 and 255, while Shared
  Runtime tests prove an extension is never invoked for those kinds and still allow independent reuse
  of local kind 9.
- Required resources: Windows x64, the configured `VCPKG_ROOT`, CMake, Visual Studio MSVC/LLVM and
  Ninja toolchains, and the locked OCCT, CGAL, Manifold, and FCL dependencies.
- Documentation: the durable platform architecture and provider READMEs now identify Shared as the
  plan/emission and common diagnostic authority; no additional ADR is required.
- Independent candidate review: the implementation and verification lanes found the previous
  native-error transport authority blocker resolved, with no remaining code, test, or design finding.
- Operational limitation: full proof is Windows-only and resource intensive; the immutable local
  evidence directory is intentionally ignored by source control.

## Risks and implementation notes

Full pipeline proof is resource intensive and must preserve the repository's serial native-build,
disk-boundary, and task-owned cleanup rules.
