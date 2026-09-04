# Make repository builds reproducible and safely verified

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: maintenance -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user-approved-in-thread-2026-09-02-and-abi-v1-retirement-2026-09-04 -->
<!-- candidate-binding: workspace:ce9b864918c5dc3d52b6e18c913ba4956f1026d1:sha256:d105ac7a354c636bb859725cd7824be8418ea4cf5758a1533e4bdd4e835de10b -->

<!-- section: goal-rationale -->
## Goal and rationale

Maintainers can build and verify the repository through one reproducible path without nested-build
races, unexpected restore traffic, missing managed test suites, manual native-fixture setup, incorrect
package links, or unnecessarily broad CI trust. The current solution build races two compilations of
`TedToolkit.Occt.Console`, and the build pipeline does not execute all existing verification projects.

<!-- section: scope -->
## Scope and non-goals

- In scope: solution/MSBuild generation orchestration, repository build-pipeline test coverage,
  automated native Handle fixture construction and execution, package project URL, and GitHub Actions
  permissions and reusable-workflow pinning; retirement of the broken historical ABI-v1 fixture,
  its root CMake entry points, retained native source, managed boundary test, and obsolete guide.
- Non-goals: the complete C11/DLL binding implementation, publishing a package or release, changing
  generated binding semantics, and the `TedToolkit.CppBindings` migration.
- Compatibility: existing generator, Runtime, analyzer, generated binding, and native Handle lifetime
  behavior remain unchanged. The historical ABI-v1 verification contract is explicitly retired;
  its materializer was already removed by the current exact-match binding architecture. No current
  binding test or supported API is removed. Existing uncommitted template-generation work is preserved.

<!-- section: structural-contract -->
## Structural outcomes

<!-- structural-outcome: STR-01 -->
- STR-01: A default parallel solution build performs generation once without competing for a project
  output, and `--no-restore` causes no nested restore attempt.

<!-- structural-outcome: STR-02 -->
- STR-02: The standard repository pipeline runs the Generator, Runtime, and Runtime Analyzer TUnit
  projects and fails when any suite fails.

<!-- structural-outcome: STR-03 -->
- STR-03: The standard verification path builds both native Handle fixture libraries, supplies their
  resolved paths to the native integration executable, and fails unless both exact-library release
  calls occur once.

<!-- structural-outcome: STR-04 -->
- STR-04: Produced package metadata uses
  `https://github.com/TedToolkit/TedToolkit.Occt` as its project URL rather than
  `https://github.com/TedToolkit/TedToolkit.Assertions`.

<!-- structural-outcome: STR-05 -->
- STR-05: GitHub Actions references the delegated build workflow at immutable commit
  `b9152e16ef9e227a2540b86b90af5391374cfd90`; its existing write permissions and inherited secrets
  remain explicit because that exact upstream workflow contains conditional release/PR modules and
  consumes the AI, NuGet, and GitHub credentials.

<!-- structural-outcome: STR-06 -->
- STR-06: No active build, test, or current documentation entry point requires the retired ABI-v1
  materializer. The current exact-match generated boundary and native Handle tests remain active.

## Constraints and risks

- The build remains Windows `win-x64` and uses the existing CMake/vcpkg prerequisites; this change
  does not claim another supported platform.
- Do not hide generation by disabling normal solution projects or serialize the entire solution as
  the permanent fix. Generation must have one explicit owner in the build graph.
- Native fixture paths must be derived from build outputs rather than machine-specific absolute paths.
- The selected upstream commit declares `workflow_call`, requests `contents: write` and
  `pull-requests: write`, and passes AI, NuGet, and GitHub credentials into the shared build project.
  Reducing those grants requires a separate upstream workflow contract; this delivery removes the
  mutable-reference risk without silently breaking its release behavior.
- Recovery is a normal revert of build/configuration changes; no external CI, repository, or package
  publication operation is authorized.
- ABI-v1 retirement removes only tracked historical fixture artifacts, recoverable from Git at
  baseline ce9b864918c5dc3d52b6e18c913ba4956f1026d1. Restoring fixture files alone would also restore
  their known missing-materializer failure, not a working compatibility layer.
- Escalate if implementation changes public managed/native APIs, binding semantics, supported
  platforms, external repository settings, or the active product migration.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: repository build orchestration, `Build`, native test fixtures,
  shared package properties, and the GitHub Actions wrapper.
- Resource prerequisites: installed .NET SDK and the existing Windows C++/CMake toolchain; package
  assets must already be restored for the no-restore proof.
- Likely touchpoints (non-binding): `TedToolkit.Occt.slnx`,
  `src/core/TedToolkit.Occt.Windows/TedToolkit.Occt.Windows.csproj`, `Build/Program.cs`,
  `tests/native/handle-fixtures`, `tests/TedToolkit.Occt.Runtime.NativeIntegration`,
  `Directory.Build.props`, and `.github/workflows/build.yml`.
- Private implementation choices left open: the exact MSBuild target/project representation and how
  the build pipeline composes fixture build arguments.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: STR-01 purpose=structural shape=integration -->
<!-- primary-proof: STR-02 purpose=structural shape=integration -->
<!-- primary-proof: STR-03 purpose=boundary shape=integration -->
<!-- primary-proof: STR-04 purpose=structural shape=contract -->
<!-- primary-proof: STR-05 purpose=structural shape=contract -->
<!-- primary-proof: STR-06 purpose=structural shape=contract -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| STR-01 | Primary | A normal parallel no-restore solution build succeeds without duplicate Console compilation, and the evaluated generation target contains no nested `dotnet run`/restore path | Run `dotnet build TedToolkit.Occt.slnx --no-restore`; inspect a preprocessed/evaluated Windows project or focused structural assertion for the generation target |
| STR-02 | Primary | All three managed TUnit projects execute successfully through the repository pipeline | Run the repository `Build` project and inspect its test invocations/results |
| STR-03 | Primary | Both fixture libraries are built and the native integration executable exits zero using their resolved paths | Run the repository `Build` project on Windows with the native toolchain available |
| STR-04 | Primary | Evaluated package metadata contains the authoritative repository URL | Inspect evaluated MSBuild/package metadata after build |
| STR-05 | Primary | The reusable-workflow reference equals the recorded commit while the grants required by that upstream contract remain visible | Review `.github/workflows/build.yml` and validate YAML/workflow syntax against upstream commit `b9152e16ef9e227a2540b86b90af5391374cfd90` |
| STR-06 | Primary | Retired fixture paths and active materializer/ABI-v1 references are absent, while current binding verification is retained | Inspect the exact deleted-path inventory, scan tracked current sources/docs/build entry points for ABI-v1 references, and run the retained managed/native gates |
| Existing behavior | Conditional | Existing Generator, Runtime, and Runtime Analyzer suites remain green | Run the three documented `dotnet run --project ... --no-build -- --report-trx` commands |
| Cache recovery | Conditional | Missing/empty outputs and changed/deleted inputs cannot bypass generation; failed native work does not publish success | Run `pwsh -NoProfile -File Build/VerifyGenerationCache.ps1` |
| Gate failure | Conditional | Empty, failing, skipped or malformed test results, nonzero process exits, and cancellation fail closed | Build the Release Build project, then run `pwsh -NoProfile -File Build/VerifyManagedTestGate.ps1` on PowerShell with .NET 10 |

<!-- section: completion-criteria -->
## Completion

Complete when all six structural outcomes have candidate-bound evidence, the retained managed
regression suites pass, an independent implementation review accepts the CI security and native
boundary coverage, and any enduring developer command changes are reflected in current README
documentation. Terminal status does not itself authorize cleanup; delete this temporary record only
after it is committed on the authoritative branch, no tracked artifact references it, durable truth
is retained elsewhere, and the user explicitly continues cleanup.

## Current verification

- Candidate: isolated `C:\Users\11239\AppData\Local\Temp\occt-r` worktree at baseline
  `ce9b864918c5dc3d52b6e18c913ba4956f1026d1`; all 24 owned changed/deleted/untracked paths
  reproduce SHA-256 `d105ac7a354c636bb859725cd7824be8418ea4cf5758a1533e4bdd4e835de10b`.
  The shared submodule is clean. Unrelated Binding/template work remains unchanged in the main
  workspace and is excluded from this delivery's verification claim.
- Primary integration: `dotnet run --project Build/Build.csproj -c Release --no-build --no-restore`
  passed on 2026-09-04 in 59m40s. Windows bindings completed all 6,992 native build/link steps;
  managed solution compilation, native integration, managed gates, and final assertions passed.
  The only skipped module was optional formatting. No release/publish modules ran.
- No-restore/incremental: `dotnet build TedToolkit.Occt.slnx -c Release --no-restore` passed in
  6.19 seconds with zero warnings/errors, one Console output, and a generation cache hit.
- Managed regression: Generator 87/87, Runtime 38/38, Runtime Analyzer 16/16; all 141 individual
  results passed with zero skips. Native Handle integration passed CTest 1/1 using both actual DLLs.
- Conditional proof: `Build/VerifyGenerationCache.ps1` passed on both sandbox and ordinary host;
  it covers cache reuse/invalidation, missing/empty output, changed/deleted input, native failure,
  retry, case-insensitive duplicate environment imports, last-value semantics, original PATH
  restoration, removal of newly introduced variables, and the real Visual Studio environment.
  The restoration defect was reproduced before repair. `Build/VerifyManagedTestGate.ps1` passed
  seven TRX cases, nonzero child exit, and cancellation.
- Standalone documentation proof: cleaned the Release native-integration project, confirmed its
  runner DLL absent, then rebuilt it with zero warnings/errors and ran both native fixtures
  successfully. Runtime README now includes that prerequisite, Release configuration for
  single-config generators, and no unnecessary vcpkg app-local copying.
- Structural proof: produced package metadata contains the authoritative project URL; the Actions
  wrapper uses the approved immutable upstream commit and explicit required grants. All eight
  historical ABI-v1 artifacts are retired without removing current binding or native lifetime tests.
- Raw evidence remains in the isolated worktree: `out/reliability-final-pipeline.log`,
  `out/reliability-final-incremental.log`, `out/reliability-final-incremental.binlog`,
  `output/test/*.trx`, and `output/tests/handle-fixtures/Testing/Temporary/LastTest.log`.
  The initial harness log-location failure is separately retained under `out`; the successful
  retry did not alter code or forge cache stamps. These timings are verification, not benchmarks.
- Independent review: fresh read-only `review_reliability_final` concluded `Ready to merge`,
  all STR-01 through STR-06 covered, no Blocking or Important findings. It independently checked
  primary artifacts and unchanged candidate hashes before/after verification. Reviewed contract
  SHA-256: `C916B51A9A1D0400BE661C8CE1E999E567638906C8A470115846710431F49354`.
  Only lifecycle/completion evidence is updated after that review; the approved contract is unchanged.
- Durable extraction is captured in the root, Generator, and Runtime READMEs and current architecture;
  no operational handoff remains inside the approved scope. The user's instruction to continue until
  completion authorizes closure without another phase pause. Performance benchmarking remains a
  separate draft; unrelated migration, external Actions, merges, and publishing are excluded.
- Cleanup disposition: retain the exact `docs/changes/reliable-build-verification` directory until
  the record is committed on the authoritative branch and the cleanup helper confirms eligibility.
  The initial eligibility check returned `change record is not tracked`. The user explicitly
  authorized a scoped local commit and subsequent record cleanup on 2026-09-04; that authority is
  separate from the implementation review and does not authorize pushing or publishing.
- Digest reproduction: sort the unique union of `git diff --name-only HEAD` and
  `git ls-files --others --exclude-standard` with `Sort-Object -Unique`; prepend `HEAD <full-sha>`,
  append one `<relative-path>|<uppercase-file-SHA256-or-DELETED>` row per path, and SHA-256 the UTF-8
  rows joined with LF and no trailing LF. Workflow records are outside the implementation bundle.
