# UPG-002: Migrate Manifold to the shared provider contract

<!-- work-item-format: 2 -->
<!-- work-item-id: UPG-002 -->

<!-- approval-source: maintainer approved the revised delivery map and explicitly continued in the Codex task on 2026-09-09 -->

## Outcome

The locked Manifold Generator expresses its mesh buffers, owned results, status, and operations as
provider semantic inputs consumed by Shared, with its complete private binding renderer removed and
the verified UPG-005 Runtime projection preserved.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public contract: the unreleased Manifold Generator API and its use
  of the Shared semantic model.
- In scope: Manifold profile loading, declarations and provider policy, migration to
  `SemanticGenerationProvider`, removal of its private plan/output path and full C#/C++ renderer, and
  focused generation proof; and preservation of the verified Shared native-error consumption.
- Non-goals: widening Shared beyond semantics already proved by FCL and Manifold, changing Manifold
  provider-specific status/result contracts, or changing the locked public binding profile.
- Likely touchpoints (non-binding): Manifold Generator sources/resources/tests and any narrowly
  necessary Shared refinements inside the supplied UPG-001 contract.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| UPG-001 | Shared can represent and emit verified provider-neutral buffers, projected results, standard bootstrap, and plans | UPG-001 is Verified on the selected integration baseline |
| UPG-005 | Shared owns common native-error generation and Runtime projection | UPG-005 is Verified on the selected integration baseline |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-02 | Owns | Completes the Manifold migration without a second renderer authority |
| AC-04 | Supports | Supplies preserved Manifold generation, package, and native behavior |

<!-- work-item: delivery-constraints -->
## Constraints

- Preserve every current Manifold public generated type and operation, exact enum values, span and
  mesh validation, independent owned results, native-width counts, Shared common exception mapping, layouts,
  exports, inventories, dependency closure, and native basename.
- Removed unreleased Manifold Generator-only APIs receive no compatibility wrappers.
- Library-specific adapter semantics remain provider-owned inputs; generic emitted syntax and
  bootstrap mechanics remain Shared-owned.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-02 purpose=acceptance shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-02 | Primary | Manifold creates deterministic shared plans and contains no private plan/output/bootstrap or complete managed/native renderer | `dotnet run --project tests/TedToolkit.CppBindings.Manifold.Generator.Tests/TedToolkit.CppBindings.Manifold.Generator.Tests.csproj -c Release -- --report-trx` |
| Manifold boundary | Conditional | The real package retains mesh, Boolean, translation, status, Shared common exceptions, ownership, dependency, and notice behavior | `pwsh -NoProfile -File Build/VerifyManifoldWindowsPackage.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-02 and the Manifold Windows boundary proof pass.
- Manifold's former plan, writer, NativeApi/function-table/error copies, and complete binding renderer
  are removed without provider policy entering Shared.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact candidate revision, changed artifacts, AC-02 proof purpose and shape, commands,
observed counts/results, Windows prerequisites, generated-surface comparison, and the verified
Manifold migration supplied to UPG-004.

## Risks and implementation notes

The two-phase mesh count/copy operation and owner construction failure path are the highest-risk
semantic partitions; neither may be reduced to unchecked raw source snippets.
