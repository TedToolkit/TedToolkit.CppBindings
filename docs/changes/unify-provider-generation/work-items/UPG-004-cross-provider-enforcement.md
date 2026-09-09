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

## Risks and implementation notes

Full pipeline proof is resource intensive and must preserve the repository's serial native-build,
disk-boundary, and task-owned cleanup rules.
