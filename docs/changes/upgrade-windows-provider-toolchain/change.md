# Upgrade locked Windows provider toolchains

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: maintainer approved the final reviewed contract and explicitly continued in the Codex task on 2026-09-09 with "统一，继续！直到解决了为止。" -->
<!-- candidate-binding: workspace:c44a72aa98d44b03440a359ce4bb4bc2de080f49:sha256:8c3e8ba1b29f3b0bff807ef24ddb2bcf6a3c495376e9ee6dfd5e50bced580503 -->

<!-- section: goal-rationale -->
## Goal and rationale

Move the maintained CGAL, FCL, and Manifold Windows profiles from MSVC `19.51.36256` to the
installed and supported `19.51.36257` toolchain so their reproducible generation, native build,
package, and consumer gates can run without rolling Visual Studio back. The compiler servicing
update currently makes every locked provider build fail even though its declared native dependency
versions and generated binding surface are unchanged.

<!-- section: scope -->
## Scope and non-goals

- In scope: versioned `v2` identities for the three maintained finite profiles; an MSVC lock of
  `19.51.36257`; rebuilding the affected `x64-windows` vcpkg dependency closures at the existing
  builtin baseline; profile, verifier, test, contributor-documentation, and generated-output
  baseline updates required by that identity migration.
- Non-goals: changing the vcpkg builtin baseline, native dependency versions, triplet, CMake
  version, package IDs, generated namespaces or public APIs, native exports, ownership semantics,
  output layout, or weakening exact toolchain validation.
- Compatibility: consumer packages and generated APIs remain source- and binary-compatible. The
  bundled defaults move to `v2`; no `v1` identifier aliases the new compiler. CGAL callers that
  retain an explicit old `v1` manifest retain its `19.51.36256` lock and receive the existing
  toolchain-mismatch failure on MSVC `19.51.36257`. FCL and Manifold expose only their package's
  default profile, so their `v1` defaults remain available from the earlier Generator package and
  are not selectable aliases in the migrated package.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | CGAL, FCL, and Manifold locked Windows generation | The installed MSVC `19.51.36257` is rejected by profiles locked to `19.51.36256` | Each provider selects a distinct `v2` profile locked to `19.51.36257`, accepts only that exact compiler, and completes generation and native packaging against rebuilt vcpkg inputs | Native dependency versions, CMake `4.4.3`, `x64-windows`, package identities, generated API/source fingerprints, exports, closure, notices, and consumer behavior remain unchanged |

<!-- acceptance-case: AC-01 -->
### AC-01 — Generate and package all migrated profiles

```gherkin
Scenario: Build the maintained profiles with the serviced compiler
  Given MSVC 19.51.36257 and the existing pinned vcpkg baseline and dependency versions
  When the CGAL, FCL, and Manifold Windows packages are generated, rebuilt, and independently verified
  Then each reports its v2 profile and exact compiler identity and all package, dependency-closure, notice, runtime, and isolated-consumer checks pass
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Preserve the generated binding contract

```gherkin
Scenario: Compare the migrated outputs with the approved pre-migration baseline
  Given the approved managed-source counts and byte fingerprints for all maintained providers
  When generation runs with the v2 profiles
  Then CGAL, FCL, Manifold, and unaffected OCCT retain the same generated managed-source counts and byte fingerprints and the migrated providers retain the same native export inventories
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Keep old profile identities unambiguous

```gherkin
Scenario: Select a profile after the migration
  Given a migrated Generator package and MSVC 19.51.36257
  When a caller uses its bundled default or supplies an explicit CGAL v1 manifest
  Then the bundled default reports the provider's v2 identity while the explicit v1 manifest retains its 19.51.36256 lock and is rejected rather than aliased
```

## Constraints and risks

- The profile ID changes together with the compiler lock: `epick-windows-v2`,
  `fcl-0.7.0-obbrss-double-windows-v2`, and `manifold-3.5.2-windows-v2`. Reusing a `v1` identifier
  would make one stable identity describe two toolchains.
- `C:\vcpkg` stays at builtin baseline `30ef65cad98f08e7197c9a1656fbd871bcb72f2d`; the relevant
  installed dependency closures may be rebuilt, but port sources and declared versions must not
  change.
- Rebuilding the shared vcpkg installation is an external, reversible developer-machine
  operation. The delivery owner must use one fresh ignored evidence directory and, before mutation,
  capture `vcpkg list`, the complete dependency graph for `cgal`, `fcl`, and `manifold`, the
  `installed/vcpkg/status` hash, and a restorable copy of `C:\vcpkg\installed`. Bootstrap the CLI
  with `bootstrap-vcpkg.bat -disableMetrics` when `vcpkg.exe` is absent. With
  `VCPKG_BINARY_SOURCES=clear`, install `cgal:x64-windows`, `fcl:x64-windows`, and
  `manifold:x64-windows` using `--recurse`; this compiler-driven ABI change must rebuild stale
  packages from source rather than reuse binary cache entries. Capture the post-install list,
  dependency graph, status hash, ABI values, and logs. Stop if the checkout leaves the pinned
  commit, its tracked files change, or any package version, feature, or triplet changes.
- Recovery never rolls Visual Studio back. On a failed or partial vcpkg operation, restore the
  captured `installed` tree atomically, keep MSVC `19.51.36257`, correct the environmental cause,
  and rerun the same source-only install. Retain the backup until candidate-bound package
  verification succeeds; remove it after the external handoff closes.
- The material risk is an ABI, generated-source, export, dependency-closure, or runtime change
  hidden by a one-patch compiler servicing update. Any such difference is a stop condition that
  requires renewed design approval.
- Escalation triggers: any native dependency/baseline/CMake/triplet change, generated or public API
  difference, package/output-layout change, relaxed exact-version check, or inability to recover
  the prior vcpkg inventory.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Implement this change in an isolated clean worktree at the approved Git baseline, independent
of the in-progress central-generation workspace. Before editing a profile, create this change's own
repository-contained pre-migration baseline from source-only `v1` generation plans without building
an old native binary. The CGAL capture may disable installed-toolchain matching only for this
source-derived snapshot; package and acceptance builds may not. Record every invariant managed and
native generated-source hash, ABI/layout and declaration inventory, deterministic function-table
slot/order and call shape, public export inventory, package identity/content boundary, and native
dependency version. Record profile/toolchain fields separately so the verifier requires exactly two
approved deltas—profile ID and MSVC version—and exact equality for every other field. The installed
compiler is `19.51.36257`, while the repository and vcpkg checkout both retain the approved vcpkg
baseline. Historical native-binary byte equality is neither required nor claimed; the rebuilt
binary is proved by package/runtime/closure verification.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: migrate the three profile identities and exact compiler locks,
  rebuild their existing vcpkg closures, refresh matching verification expectations and current
  documentation, and prove unchanged generated/package behavior.
- Other real start conditions or resource prerequisites: .NET 10, PowerShell 7.5, CMake 4.4.3,
  Visual Studio MSVC 19.51.36257, sufficient native-build disk space, and writable `C:\vcpkg`.
- Likely touchpoints (non-binding): the three provider profile definitions/resources, their
  Generator tests and package verifiers, provider READMEs, repository profile documentation, and a
  dedicated toolchain-migration baseline and verifier owned by this change.
- Private implementation choices left open: evidence-directory name, verifier decomposition, and
  edit order. The vcpkg mutation and recovery boundaries are fixed above.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | The source-rebuilt closures retain their package versions/features/triplet, and the three providers report their v2 identity and `MSVC 19.51.36257.0`; package, runtime, closure, notice, and isolated-consumer checks pass | Execute the bounded vcpkg capture/rebuild procedure above, then run `Build/VerifyCgalWindowsPackage.ps1`, `Build/VerifyFclWindowsPackage.ps1`, and `Build/VerifyManifoldWindowsPackage.ps1` with fresh report directories |
| AC-02 | Primary | Invariant managed/native source hashes, ABI/layout/declaration inventories, function-table slot/order/call shape, public exports, package IDs/content boundaries, and dependency versions match the source-derived baseline; only profile ID and MSVC fields change to their approved values | Run the dedicated toolchain-migration verifier against this change's repository-contained baseline, all four Windows provider builds, all four package verifiers, and the affected Generator tests |
| AC-03 | Primary | Every bundled default is v2; an explicit CGAL v1 manifest remains locked to `19.51.36256` and fails on the new compiler; no old identifier aliases v2 | Run focused profile-selection and toolchain-mismatch contract tests for all three Generator packages |
| Toolchain rejection | Conditional | A compiler identity other than the profile lock is still rejected | Run focused profile/discovery and Windows generation cache tests |
| Repository regression | Conditional | The complete repository pipeline remains green | `dotnet run --project Build/Build.csproj -c Release` |

<!-- section: completion-criteria -->
## Completion

All three acceptance cases pass on one exact candidate, the vcpkg rebuild inventory and compiler identity
are retained as candidate-bound evidence, current profile documentation describes the v2 identities,
no temporary rebuild artifacts remain in the repository, and independent implementation review
finds no unresolved compatibility or verification issue. After merge and reference release, delete
this completed delivery record; Git history retains it.

## Integrated candidate evidence

- The final integrated bundle is `out/candidate/centralized-windows-toolchain.patch`, based on
  `c44a72aa98d44b03440a359ce4bb4bc2de080f49`, with SHA-256
  `8c3e8ba1b29f3b0bff807ef24ddb2bcf6a3c495376e9ee6dfd5e50bced580503` across 72 paths. It includes
  the separately approved centralized Windows generation host, excludes both lifecycle records,
  applies cleanly to the baseline, and reverse-applies cleanly to the verified workspace.
- `Build/VerifyWindowsProviderToolchainMigration.ps1` passed under
  `out/verification/centralized-toolchain-final2-20260910`. CGAL, FCL, and Manifold reported their
  exact v2 identities and MSVC `19.51.36257`; OCCT retained 7,342 managed and 6,990 native artifacts
  with fingerprints `21185f594a6c8a20d4fed2eb97f4bf45818a3c7c907085a5874809ce54540425` and
  `82cf23e4e43bb92a3540e8cb38620a4b6aba092b69870810b3078d2316e8e78e`.
- Fresh package verification passed for all four providers under
  `out/verification/centralized-final-*-package-20260910`; the complete repository pipeline passed
  all eight enabled modules in 44 minutes 23 seconds with 298/298 managed tests and zero native or
  managed build warnings/errors.
- The vcpkg checkout remains at `30ef65cad98f08e7197c9a1656fbd871bcb72f2d`; the rebuilt installed
  status hash remains `D613F62014CB8C097653964078AAB4A7B62CE821A45D11C7680B0B3B4A582F5F` with the approved versions,
  features, triplet, and refreshed compiler ABI. The exact recovery backup remains retained until
  this integrated candidate receives independent review and the external handoff closes.

## Integrated independent implementation review

- Conclusion: `Ready to merge` for exactly
  `workspace:c44a72aa98d44b03440a359ce4bb4bc2de080f49:sha256:8c3e8ba1b29f3b0bff807ef24ddb2bcf6a3c495376e9ee6dfd5e50bced580503`.
  Independence was established by a fresh read-only reviewer that did not implement the candidate
  or perform the external migration.
- Code correctness, test adequacy, and candidate-bound verification all passed with no Blocking or
  Important findings, suggestions, design deviations, or prohibited profile/dependency/package/API
  changes. The reviewer independently verified bundle completeness and current vcpkg revision,
  tracked cleanliness, installed-status hash, rebuild evidence, versions, features, triplet, and ABI
  transition.
- Fresh review execution passed the shared-host suite 22/22, FCL and Manifold Generator suites 6/6
  each, and the exact CGAL legacy-profile contract 1/1. The retained complete candidate pipeline and
  CGAL suite remained 298/298 and 11/11 respectively.
- Documentation extraction is complete. The external recovery backup is technically safe to remove
  only after the delivery owner closes the lifecycle and external handoff; the active change record
  is deleted only after merge and reference release.

## Superseded isolated candidate evidence

The following evidence remains valid for the isolated pre-centralization implementation, but its
binding is superseded by the maintainer-authorized integration into the shared Windows generation
tool and must not be used as the final merge candidate.

- Baseline revision: `c44a72aa98d44b03440a359ce4bb4bc2de080f49`. The frozen implementation
  bundle is `out/candidate/windows-provider-toolchain-review-fix.patch`; its SHA-256 is
  `dcd6a7f375c1962e1f2acc41cae5fd1ef7545d33b87a0f7ee535107b407a1602`. The lifecycle record is
  coordinator state and is not part of that implementation bundle.
- `C:\vcpkg` remained at builtin baseline `30ef65cad98f08e7197c9a1656fbd871bcb72f2d`.
  A source-only rebuild with binary caches disabled preserved all 11 relevant package
  versions, features, and the `x64-windows` triplet while refreshing their compiler-dependent ABI
  values. Pre/post inventory, graph, status, ABI, and install logs are retained under
  `out/vcpkg-toolchain-migration-20260909-1334`.
- `Build/VerifyGenerationCache.ps1` passed with more than 25 GiB free. It covers `/bigobj`, process
  environment restoration, successful OCCT object-intermediate reclamation, and deterministic FCL
  and Manifold wrong-compiler failures that publish no output or success stamp.
- The final OCCT, CGAL, FCL, and Manifold Windows package verifiers passed under
  `out/final-packages/candidate-final-occt`, `candidate-final-cgal-short`,
  `candidate-final-fcl`, and `candidate-final-manifold`. The cross-provider migration verifier
  passed under `out/verification/candidate-final-migration`, including exact v2/MSVC identity
  checks and unchanged aggregate fingerprints for 7,342 OCCT managed and 6,990 native files.
- `dotnet run --project Build/Build.csproj -c Release` passed all eight enabled pipeline targets.
  The test totals were 33 shared Generator, 11 CGAL Generator, 6 CGAL Runtime, 6 FCL Generator,
  4 FCL Runtime, 6 Manifold Generator, 7 Manifold Runtime, 144 OCCT Generator, 42 Runtime, and
  17 Analyzer tests, all with zero failures. The OCCT native build compiled and linked all 690
  translation units; CGAL, FCL, and Manifold then built with MSVC `19.51.36257.0`, each with zero
  warnings and errors. The complete pipeline finished in 38 minutes 2 seconds.
- Implementation-context proof exposed three build-only defects inside the approved boundary:
  generated Windows native builds now preserve `/EHsc` while adding `/bigobj` for the complete OCCT
  build, and one analyzer fixture now fully qualifies the shared `NativeErrorProjection` type after
  the solution-wide namespace set made the short name ambiguous. Successful OCCT generation also
  removes its target object directory after validating the DLL, manifest, and dependency closure,
  then publishes the success stamp last, so later provider builds retain the disk safety margin and
  a cleanup failure cannot leave a false success marker. None changes generated or consumer API
  behavior.
- The temporary drive mapping and failed `C:\ttv2` report directory were removed. The external
  `C:\vcpkg\.tedtoolkit-backups\windows-toolchain-migration-c44a72a-20260909-1334` recovery copy is
  intentionally retained until candidate-bound review and integration handoff close.

## Superseded isolated implementation review

- Conclusion: `Ready to merge` for exactly
  `workspace:c44a72aa98d44b03440a359ce4bb4bc2de080f49:sha256:dcd6a7f375c1962e1f2acc41cae5fd1ef7545d33b87a0f7ee535107b407a1602`.
  Independence was established by a fresh read-only reviewer that did not implement the candidate.
- Code correctness, test adequacy, and candidate-bound verification all passed with no Blocking or
  Important findings. The reviewer revalidated all 29 bundle paths and post-images, ran 40 focused
  TUnit tests with zero failures or skips, and freshly passed the migration and generation-cache
  verifiers. Retained package, full-pipeline, and vcpkg evidence was checked from raw artifacts.
- No design deviation or unrelated candidate path was found. Remote CI and release publication were
  unavailable, and the full native pipeline was not repeated during review because its retained
  post-fix evidence was exact and review activity reduced free space below the native-build guard.
- Durable profile, migration-invariant, and cache semantics are captured in current documentation
  and versioned verifiers. After merge, reference release, and integration/package handoff closure,
  remove the retained vcpkg recovery backup and delete this completed active change record.
