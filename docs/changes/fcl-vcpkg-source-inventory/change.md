# Derive the FCL source inventory from vcpkg

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: bug-fix -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user approved the proposed vcpkg-backed provider corrections and explicitly requested execution on 2026-09-09 -->
<!-- candidate-binding: workspace:7c5da06dc0a806dc764ca2d3a83056933ae888ce:sha256:47b94a78a14d4957c2240850cf853cc8c366cd7c78737e4fee21430ebd1cf13b -->

<!-- section: goal-rationale -->
## Goal and rationale

Make every FCL generation run report the complete installed public-header set supplied by its selected
vcpkg root. The current three-entry handwritten inventory omits most of FCL 0.7.0 and can silently
misrepresent the source boundary used to justify the finite generated profile.

<!-- section: scope -->
## Scope and non-goals

- In scope: accept an explicit vcpkg root, enumerate `installed/<triplet>/include/fcl`, verify that set
  against the package list and installed status for the profile's exact FCL identity, classify every header by profile reachability, update
  repository callers and tests, and remove the unused embedded FCL profile JSON.
- Non-goals: expanding the admitted FCL API, changing native or managed ABI behavior, validating the
  entire compiler toolchain, or changing OCCT and CGAL generation.
- Compatibility or deliberately preserved behavior: `CreatePlan()` and
  `GenerateAsync(DirectoryInfo, CancellationToken)` remain available and resolve `VCPKG_ROOT` when
  used; new overloads with an explicit `DirectoryInfo vcpkgRoot` provide the authoritative repository
  path. Existing generated binding/native sources, profile identity, native exports, and vcpkg
  manifest/configuration package assets remain unchanged.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | FCL `source-inventory.json` | Three handwritten entries | Every installed `fcl/` public header appears once, sorted and classified as a profile root, reachable dependency, or not reachable | Finite declaration and emitted binding inventories |
| OB-02 | FCL vcpkg input validation | Generation does not inspect the selected vcpkg installation | Missing roots, absent or wrong package identity, missing or ambiguous package lists, empty header sets, and installed/package-list drift fail before output is written | FCL 0.7.0#5 `x64-windows` profile identity |

<!-- acceptance-case: AC-01 -->
### AC-01 — Complete installed source inventory

```gherkin
Scenario: Generate the FCL profile from a valid vcpkg root
  Given one vcpkg root with the locked FCL package installed for the profile triplet
  When the provider creates a generation plan
  Then source-inventory.json contains every installed fcl public header exactly once in ordinal order, with fcl/fcl.h as the root and all lexical qualified or relative includes whose normalized targets remain in the fcl tree classified by transitive reachability
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Reject unverified package state

```gherkin
Scenario: Generate the FCL profile from inconsistent vcpkg metadata
  Given the configured vcpkg root lacks the profile package identity or has missing, ambiguous, empty, or drifting fcl header metadata
  When the provider creates or writes a generation plan
  Then generation fails with an inventory validation error and writes no output
```

## Constraints and risks

- The explicit root is authoritative; generation must not hardcode `C:\vcpkg` or bundle a header
  snapshot.
- Header reachability follows real `#include` edges whose targets exist below the selected include root.
- `fcl/fcl.h` is the only profile root. Include closure reads every lexical quoted or angled include,
  resolves `fcl/...` from the triplet include root and other paths relative to the including header,
  normalizes separators and dot segments, and admits only existing targets inside the `fcl` subtree.
- Existing public methods retain their signatures and resolve `VCPKG_ROOT` at invocation; repository
  generation uses the explicit-root overloads. Missing ambient configuration fails clearly.
- A future FCL layout or package-list format change must fail closed rather than emit partial evidence.
- This correction lands before approved work item `UPG-001`; that later migration must preserve the
  corrected inventory as its input baseline rather than restore the handwritten snapshot.
- Execution ownership is serialized: this change exclusively owns FCL inventory discovery and its
  focused tests until integration; the `unify-provider-generation` coordinator keeps `UPG-001`
  unstarted and then consumes the integrated corrected inventory under its existing preservation
  contract. The central-generation coordinator alone owns `ProviderGenerators.cs` and wires the
  explicit root after this provider API is available.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: FCL Generator discovery, its package metadata, repository caller,
  and focused generator tests.
- Other real start conditions or resource prerequisites: a representative vcpkg FCL 0.7.0#5
  `x64-windows` installation for the real-boundary acceptance test; `UPG-001` remains unstarted until
  this correction is integrated.
- Likely touchpoints (non-binding): `FclGenerationProvider`, FCL Generator project resources,
  `ProviderGenerators`, and `FclGenerationProviderTests`.
- Private implementation choices left open: internal inventory/discovery types, include parser shape,
  and test fixture layout.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | A real vcpkg FCL installation produces a sorted inventory equal to its installed package-list headers and includes transitive dependencies omitted by the old snapshot | `dotnet run --project tests/TedToolkit.CppBindings.Fcl.Generator.Tests/TedToolkit.CppBindings.Fcl.Generator.Tests.csproj` |

<!-- primary-proof: AC-02 purpose=acceptance shape=unit -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-02 | Primary | Focused temporary-root scenarios independently prove missing root, absent or wrong package identity, missing/ambiguous/empty package lists, installed/list drift, and output non-publication | `dotnet run --project tests/TedToolkit.CppBindings.Fcl.Generator.Tests/TedToolkit.CppBindings.Fcl.Generator.Tests.csproj` |

| Existing entry points | Regression | Existing plan and write method signatures work with `VCPKG_ROOT`, fail clearly without it, and explicit-root overloads produce the same non-inventory artifacts | Run the focused FCL Generator tests above |
| Package/resources | Structural | The packed Generator contains the vcpkg manifest/configuration but no unused profile JSON or header snapshot | Pack the FCL Generator and inspect the archive entries |

<!-- section: completion-criteria -->
## Completion

Both acceptance cases and regression/structural checks pass, the Generator package contains no unused
FCL profile JSON or header snapshot, repository generation passes the selected vcpkg root explicitly,
and every generated artifact except `source-inventory.json` remains byte-identical.
