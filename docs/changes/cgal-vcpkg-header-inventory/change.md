# Use vcpkg as the CGAL header inventory authority

<!-- change-format: 3 -->
<!-- workflow-profile: standard -->
<!-- change-kind: behavior-change -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->

- Priority: P2
<!-- approval-source: user-approved-in-codex-task-2026-09-09 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

Make the selected vcpkg installation the sole authority for CGAL public-header discovery and
inventory validation. The Generator currently embeds 3,773 CGAL 6.2 header paths that duplicate
vcpkg's installed package list, enlarge the package, and incorrectly couple explicit profiles to
the default profile's snapshot.

<!-- section: scope -->
## Scope and non-goals

- In scope: discover CGAL headers from the selected vcpkg include tree; validate that tree against
  the corresponding vcpkg installed-package list when inventory locking is enabled; remove the
  embedded header snapshots; and align tests and provider documentation.
- Non-goals: change the finite EPICK roots or admitted declarations, change generated binding
  semantics, alter vcpkg installation, or relax the existing package-version, ABI, CMake, and MSVC
  checks.
- Compatibility: retain `RequireLockedHeaderInventory` and its default value. Its lock authority
  becomes the selected vcpkg installation's package list, so the public API remains source- and
  binary-compatible.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | A CGAL generation run resolves its source inventory | Installed headers are enumerated from vcpkg but compared with a repository-embedded CGAL 6.2 path snapshot | Installed headers and their expected package contents are both resolved from the selected vcpkg installation; no CGAL header-list snapshot is shipped | Deterministic ordering, source dispositions, missing-header failures, finite-profile discovery, and locked toolchain validation remain intact |

<!-- acceptance-case: AC-01 -->
### AC-01 — Generate from the selected vcpkg header inventory

```gherkin
Scenario: Resolve a maintained or explicit CGAL profile
  Given a vcpkg installation containing the profile's CGAL package and public headers
  When the Generator creates a plan with locked header inventory validation enabled
  Then its source inventory equals the installed CGAL header files and the installation matches vcpkg's package list without using embedded header-list resources
```

## Constraints and risks

- Keep the finite profile and toolchain locks authoritative; the vcpkg package list must be matched
  to the selected package and triplet rather than guessed from the default profile.
- Fail closed when the vcpkg package list is absent, ambiguous, malformed, or disagrees with the
  installed CGAL tree while locking is enabled.
- Escalation triggers: removing or renaming a public option, changing generated declarations or
  output semantics, or changing the supported CGAL/toolchain profile requires renewed approval.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: CGAL Generator discovery, package resources, tests, and provider
  documentation use the selected vcpkg installation as the only header inventory authority.
- Other real start conditions or resource prerequisites: the repository's configured CGAL 6.2
  vcpkg installation is available for integration proof.
- Likely touchpoints (non-binding): `CgalProfileDiscovery`, `CgalProfileResources`, the Generator
  project file and Resources directory, Generator tests, and CGAL READMEs.
- Private implementation choices left open: package-list parsing helpers and focused test layout.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Real-vcpkg generation reports exactly the installed CGAL headers, accepts explicit profiles, and the built Generator contains no embedded header-list resources | `dotnet run --project tests/TedToolkit.CppBindings.Cgal.Generator.Tests/TedToolkit.CppBindings.Cgal.Generator.Tests.csproj` |
| AC-01 | Conditional structural/package regression | The Generator package builds and remains consumable with its vcpkg profile files | `pwsh -File Build/VerifyCgalGeneratorPackage.ps1` |

<!-- section: completion-criteria -->
## Completion

The primary and package regression proofs pass, the redundant header snapshots are absent, public
compatibility and locked toolchain behavior are preserved, and current CGAL documentation describes
vcpkg as the header inventory authority. No enduring documentation beyond the updated provider
guides is required; delete this completed delivery record after merge under repository policy.

## Implementation status

- Changed artifacts: CGAL header discovery and profile resource loading, Generator package inputs,
  Generator tests, and the two CGAL provider guides. Eight embedded header snapshot files were
  removed.
- Acceptance: the real-vcpkg inventory test passed with 3,773 package-list entries, and the missing
  package-list fail-closed test passed.
- Structural proof: the affected test project built with zero warnings and errors; a Release NuGet
  package contained both vcpkg profile files and zero header snapshot files.
- Verification limitation: the full CGAL Generator suite discovered nine tests; five passed and
  four stopped at the preserved toolchain lock because the installed MSVC is `19.51.36257` while
  the approved profile requires `19.51.36256`. Updating or relaxing that profile is outside this
  change. The clean-candidate package script also cannot run in the shared dirty worktree.
- Scope deviations: None.
