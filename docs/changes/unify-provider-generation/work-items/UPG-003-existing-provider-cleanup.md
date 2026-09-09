# UPG-003: Finish consolidation in CGAL and OCCT

<!-- work-item-format: 2 -->
<!-- work-item-id: UPG-003 -->

<!-- approval-source: maintainer approved the revised delivery map and explicitly continued in the Codex task on 2026-09-09 -->

## Outcome

CGAL and OCCT retain only provider-specific discovery, profiles, policy, and native-library semantic
inputs; their remaining generic result, error, native-project, managed projection, and legacy emitter
implementations use the verified Shared extension points. OCCT and CGAL retain only their local
native-error extensions.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public contract: the existing CGAL and OCCT semantic-provider
  implementations and their internal support emitters.
- In scope: CGAL projected-result and standard error/native-project consolidation where generic;
  OCCT standard error/native-project consolidation; removal of OCCT legacy emitter test adapters;
  migration of tests to the actual Shared emitters and plan boundary; removal of provider-prefixed
  common native exception types; OCCT `StandardException=8` and local `OcctFailure=9`; and correction
  of CGAL overflow kind 7 versus underflow kind 3.
- Non-goals: changing CGAL header discovery/profile closure, OCCT Clang parsing/compiler probes,
  provider-specific failure meaning, or provider-specific adapter semantics that Shared cannot express
  without violating the approved boundary.
- Likely touchpoints (non-binding): CGAL and OCCT provider classes/renderers/generators/tests and
  Shared support abstractions established by UPG-001.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| UPG-001 | Shared bootstrap, error, result, native-project, and semantic extension points are verified through a real provider | UPG-001 is Verified on the selected integration baseline |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | Removes residual generic-emission authorities from providers already using Shared semantics |
| AC-04 | Supports | Supplies preserved CGAL and OCCT generation/package behavior |
| AC-05 | Owns | Proves the corrected common codes and independent OCCT/CGAL local extension mappings through their matching Runtimes |

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
<!-- primary-proof: AC-05 purpose=acceptance shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | OCCT and CGAL use Shared for every provider-neutral emitter concern and structural verification rejects reintroduced copies | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1` |
| AC-05 | Primary | Shared and Provider Generator emission tests plus Runtime tests prove the complete Shared catch order, `StandardException=8`, OCCT-local `OcctFailure=9`, CGAL overflow=7, underflow=3, and local extension-number independence | Run the Shared, OCCT, and CGAL Generator and Runtime TUnit projects |
| OCCT regression | Conditional | OCCT Generator and real Windows package behavior remain green | Run the OCCT Generator tests, then `pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1` |
| CGAL regression | Conditional | CGAL Generator and real Windows package behavior remain green on the resolved vcpkg profile | Run the CGAL Generator tests, then `pwsh -NoProfile -File Build/VerifyCgalWindowsPackage.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-03, AC-05, and both provider regression boundaries pass.
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
