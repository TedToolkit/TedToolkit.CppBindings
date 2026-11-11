# Publish provider packages for Linux x64 and macOS Arm64

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: in-progress -->
<!-- delivery-shape: multi-item -->
<!-- approval-source: The maintainer approved the Linux x64 and Apple Silicon release contract and requested implementation in the 2026-11-10 conversation. -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

Let .NET 8 consumers install and run each of the five native providers on Linux x64 and Apple
Silicon from the regular NuGet release. The current release projects and native verification
pipeline support Windows x64. Shared header discovery covers some Linux x64 cases, while the
provider build host, locked profiles, native dependency staging, and package checks target Windows.

<!-- section: scope -->
## Scope and compatibility

- Add a `.Linux.csproj` and a `.MacOS.csproj` alongside each existing Windows package project
  for OCCT, CGAL, Manifold, FCL, and SMLib. Publish distinct platform package IDs using the
  existing provider naming convention, managed namespaces, and `net8.0` target.
- Target `linux-x64` with glibc on Ubuntu 24.04 and `osx-arm64` on macOS 15 as the first
  verified environments. Build natively with the .NET 10 SDK and run the package consumers on
  .NET 8. Keep the existing package identities for the generator, analyzer, and Runtime.
- Preserve existing Windows package IDs, native profiles, consumers, and verification. Keep
  native ownership and reference semantics across platforms. Port each existing finite profile.
  For OCCT, derive the admitted surface from the target's headers, symbols, and layout proof.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Restore and run a native provider from NuGet | Windows x64 provider packages | All five providers also package, restore, load, and run on Linux x64 and macOS Arm64 | Existing Windows packages and native ownership contracts |
| OB-02 | Release the platform package set | The Windows pipeline can publish on `main` before the final Windows generation check | A single main-branch publisher receives the verified package set from all three platforms | Existing feed, Windows package IDs, and single publication of shared packages |

<!-- acceptance-case: AC-01 -->
### AC-01 — Restore and run all providers on each target

```gherkin
Scenario: Consume verified native bindings from the local NuGet feed
  Given a clean .NET 8 consumer on a target Linux or macOS host
  When it restores each provider separately and then all five together from CI artifacts
  Then the restored modules load and their representative operations and cleanup succeed in both modes
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Assemble the release from verified packages

```gherkin
Scenario: Validate the release inventory for one release version
  Given the Windows verification and both new platform jobs have succeeded
  When the release workflow collects their NuGet artifacts
  Then the single publisher receives exactly one verified package per expected ID for the same commit and release version
```

## Constraints and risks

- Generate C# and C++ together on each target with the matching headers and compiler ABI.
  Validate sizes, alignment, calling conventions, and exports. The OCCT generator has
  MSVC-specific layout rules, including bitfield allocation units. Require target evidence for
  those rules. Document and check any OCCT projection omitted for a proven target limitation.
  Require every existing finite-profile operation on both new targets. Review any change to
  that scope before shipping it. [Binding architecture](../../architecture/generated-binding-system.md)
- Reuse the pinned vcpkg versions and the pinned SMLib source where they build on the target.
  NuGet offers matching `libclang.runtime.osx-arm64` and `libClangSharp.runtime.osx-arm64`
  versions for the macOS generator. Establish supported compiler and deployment baselines in
  CI before making broader distribution claims.
- Preserve independent installation and co-installation. Pack the target's module and its
  recursive non-system closure under `runtimes/<rid>/native/`, including required notices.
  Verify ELF dependency names and search paths, Mach-O install names and rpaths, and
  package-relative loading. Reject same-name assets with differing bytes.
  [Native package isolation](../../adr/ADR-005-provider-native-package-isolation.md)
- Platform jobs build, verify, and upload CI artifacts without feed credentials. Authorize one
  publisher on `main` after all platform checks pass. Require an exact expected package ID set
  with one copy of each shared ID. Bind verified artifacts to the candidate commit, release
  version, and package digests. Publish the verified bytes. Any missing or invalid input blocks
  the entire feed push. If the feed accepts only part of the set, retry the same verified
  artifacts and version, using the existing duplicate handling.
- Apply the platform package identities, Unix loader and dependency-closure rules, and single
  publication gate in [ADR-007](../../adr/ADR-007-platform-package-release.md). Revisit the scope if a
  provider needs a different dependency version, public profile, or licensing exception.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

Use the current `development` baseline and native GitHub-hosted runners for both target RIDs.
The existing Windows verification is the compatibility baseline. Verify that the pinned SMLib
source builds and that its native closure and notices can be distributed on both targets before
admitting its packages to the release inventory.

<!-- section: delivery-brief -->
## Delivery

Plan independently verifiable work items for the shared native build and packaging path, provider
ports, and integrated release verification after the contract is approved. Use the existing
generation host, provider projects, and Windows clean-consumer tests as integration points.
Keep private MSBuild factoring, native tooling, and cache layout open to implementation.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Each target can restore, load, and exercise each provider alone and all five in one process without build-host paths or external provider installations. | On each target, compile the generated ABI/layout checks, inspect exports and dependency closure, then restore and run fresh .NET 8 consumers on a dependency-clean target host. |
| AC-02 | Primary | The publisher receives exactly the verified packages for the selected commit and version. Invalid input prevents publisher invocation. | Dry-run release collection on a full matrix. Repeat with missing, failed, duplicate, wrong-commit, wrong-version, and digest-mismatched inputs and check that each prevents publication. |
| AC-01, AC-02 | Conditional | Existing Windows generation, package isolation, and consumer checks continue to pass. | Run the existing Windows build and package verification workflow against the same candidate. |

<!-- section: completion-criteria -->
## Completion

Record passing evidence for all three platforms. Document the supported OS and architecture
matrix, package IDs, native dependency requirements, and release entry point in the lasting
documentation. Use a dry run for the PR proof and keep production feed publication on the
approved `main` release path.
