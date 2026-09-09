# UPG-003: Finish consolidation in CGAL and OCCT

<!-- work-item-format: 2 -->
<!-- work-item-id: UPG-003 -->

<!-- approval-source: maintainer approved the revised delivery map and explicitly continued in the Codex task on 2026-09-09 -->

## Outcome

CGAL and OCCT retain only provider-specific discovery, profiles, policy, and native-library semantic
inputs; their remaining non-error generic result, native-project, and legacy emitter implementations
use the verified Shared extension points. Their native-error boundary is already supplied by UPG-005.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public contract: the existing CGAL and OCCT semantic-provider
  implementations and their internal support emitters.
- In scope: CGAL projected-result and native-project consolidation where generic; OCCT native-project
  consolidation; removal of OCCT legacy emitter test adapters; and migration of tests to the actual
  Shared emitters and plan boundary.
- Non-goals: changing CGAL header discovery/profile closure, OCCT Clang parsing/compiler probes,
  provider-specific failure meaning, or provider-specific adapter semantics that Shared cannot express
  without violating the approved boundary.
- Likely touchpoints (non-binding): CGAL and OCCT provider classes/renderers/generators/tests and
  Shared support abstractions established by UPG-001.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| UPG-001 | Shared bootstrap, result, native-project, and semantic extension points are verified through a real provider | UPG-001 is Verified on the selected integration baseline |
| UPG-005 | Shared common native-error generation and Runtime projection are verified across providers | UPG-005 is Verified on the selected integration baseline |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | Removes residual generic-emission authorities from providers already using Shared semantics |
| AC-04 | Supports | Supplies preserved CGAL and OCCT generation/package behavior |

<!-- work-item: delivery-constraints -->
## Constraints

- Preserve OCCT compiler-derived layout, Handle/handle semantics, reference returns, source grouping,
  generated API, and package behavior.
- Preserve CGAL profile-relative completeness, vcpkg-owned header discovery, result alternatives,
  provider-specific exception diagnostics, generated API, and package behavior. Common native failures
  intentionally migrate to Shared exceptions.
- The active CGAL header-inventory edits must be integrated without overwriting or reverting them.
- Private helper and test organization remain open.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-03 purpose=structural shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | OCCT and CGAL use Shared for every provider-neutral emitter concern and structural verification rejects reintroduced copies | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1` |
| OCCT regression | Conditional | OCCT Generator and real Windows package behavior remain green | Run the OCCT Generator tests, then `pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1` |
| CGAL regression | Conditional | CGAL Generator and real Windows package behavior remain green on the resolved vcpkg profile | Run the CGAL Generator tests, then `pwsh -NoProfile -File Build/VerifyCgalWindowsPackage.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-03 and both provider regression boundaries pass.
- Generic helpers are removed or moved to Shared, provider-specific responsibilities are documented,
  and current CGAL inventory work is preserved.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact candidate revision, changed artifacts, AC-03 proof purpose and shape, all commands,
observed counts/results, Windows/vcpkg prerequisites, preserved concurrent CGAL work, and the verified
OCCT/CGAL inputs supplied to UPG-004.

## Risks and implementation notes

Do not mistake CGAL's template/library adapter semantics or OCCT's compiler extraction for generic
boilerplate. Move only mechanisms whose inputs can stay provider-neutral.
