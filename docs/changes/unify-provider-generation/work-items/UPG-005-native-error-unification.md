# UPG-005: Unify native-error generation and projection

<!-- work-item-format: 2 -->
<!-- work-item-id: UPG-005 -->

<!-- approval-source: maintainer approved separating and continuing this delivery in the Codex task on 2026-09-09 with "统一，并继续。把这个玩意儿统一了，给我。" -->

## Outcome

Shared is the single implementation authority for standard C++ catch generation, common native-error
diagnostic consumption, and common managed exception projection. FCL and Manifold define no local
exception mappings; OCCT and CGAL retain only their native-library-specific extensions.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public contract: Shared Generator and Runtime native-error contracts,
  plus the four Provider Generator and Runtime consumption boundaries.
- In scope: the fixed Shared kinds 0 through 8 and 255; provider-local kinds 9 through 254 without
  cross-Provider uniqueness; standard catch ordering; exact-once originating-module cleanup; strict
  diagnostic copying and fallback; Shared common exception types; OCCT `Standard_Failure=9` and
  `std::exception=8`; CGAL overflow=7 and underflow=3; and removal of provider-prefixed common
  exception implementations.
- Non-goals: migrating a Provider's complete generation pipeline, changing a locked native profile or
  dependency version, changing generated operation APIs, or consolidating provider-specific result and
  native-project semantics.
- Likely touchpoints (non-binding): Shared native error emitters and Runtime projection, Provider error
  profiles/facades, native fixtures, public-surface tests, and current error-boundary documentation.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | Approved parent AC-05 contract and ADR-006 | Parent change and repository baseline |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-05 | Owns | Supplies the complete common error authority and local Provider extension boundary |
| AC-04 | Supports | Supplies exception behavior and cleanup invariants consumed by final integration |

<!-- work-item: delivery-constraints -->
## Constraints

- Shared must not branch on a Provider name or depend on a Provider assembly.
- Provider typed catches precede Shared catches, but a Provider cannot override a Shared C++ type or
  discriminator. The same local number may have unrelated meanings in different Provider Runtimes.
- The producing native module remains responsible for clearing its carrier exactly once.
- Common native failures intentionally migrate from unreleased provider-prefixed types to Shared
  `Native*Exception` types; Provider-specific failures retain Provider exception types.
- Locked provider profiles, discovery results, generated operation surfaces, ownership, native
  artifacts, and dependency versions remain unchanged.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-05 purpose=acceptance shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-05 | Primary | Shared and Provider-focused Generator/Runtime tests prove catch order, all common kinds, overlapping local extensions, corrected OCCT/CGAL mappings, strict fallback, and exact-once cleanup | Run the Shared Generator and Runtime, FCL and Manifold Generator and Runtime, OCCT Generator, and CGAL Runtime TUnit projects with `dotnet run -c Release -- --report-trx` |
| CGAL emission | Conditional | A toolchain-unlocked focused CGAL Generator test proves its semantic profile contributes only local kinds 10 through 16 while Shared emits standard kinds | Run the focused CGAL native-error emission test with `dotnet run` and a TUnit tree-node filter |
| Locked-provider regression | Conditional | Full locked Generator/package gates remain unchanged and run on their exact configured MSVC/native dependency identities | Deferred to AC-04 integration proof when the matching locked toolchain is available |

<!-- work-item: definition-of-done -->
## Done

- AC-05 primary proof and the focused CGAL emission proof pass on one exact candidate.
- Standard catch/projection copies and provider-prefixed common exception implementations are absent;
  Provider Runtimes contain only thin Shared facades and local extension factories.
- Current architecture, ADR, and Provider Runtime documentation describe the resulting boundary.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact candidate revision, changed artifacts, AC-05 proof purpose and shape, every executed
test count/result, the unavailable locked-toolchain identity when applicable, public-surface changes,
and the verified error contract supplied to UPG-001 through UPG-004.

## Risks and implementation notes

Catch order is semantic because Provider-specific subtypes may also derive from standard C++
exceptions. A diagnostic-copy or Provider-extension failure must never suppress carrier cleanup or
turn a nonzero native failure into success.
