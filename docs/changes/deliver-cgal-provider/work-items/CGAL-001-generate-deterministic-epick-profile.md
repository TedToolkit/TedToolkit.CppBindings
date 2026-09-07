# CGAL-001: Generate a deterministic finite EPICK profile

<!-- work-item-format: 2 -->
<!-- work-item-id: CGAL-001 -->

<!-- approval-source: The maintainer explicitly approved this exact work-item map with “批准。” in the Codex task on 2026-09-07. -->

## Outcome

Deliver `TedToolkit.CppBindings.Cgal.Generator` with a configurable finite-profile contract and the
default `epick-windows-v1` manifest, so two real CGAL 6.2 runs produce identical candidate,
admitted, unsupported, managed, native, and source inventories through the Shared semantic engine.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: the CGAL Generator package and embedded
  `epick-windows-v1` profile/inventory schema below `src/providers/cgal`.
- In scope: vcpkg CGAL discovery; installed public-header inventory; explicit kernel alias and
  closed-template signatures; recursive declaration closure; CGAL type, layout, lifetime,
  dependency, naming, admission, exception-support, and native-project policies; stable source,
  candidate, admitted, and unsupported dispositions; paired Shared-generated sources; package and
  deterministic real-header tests.
- Non-goals: implement the managed Runtime taxonomy, compile the final Windows native artifact,
  bundle dependencies, publish packages, support another kernel/RID, or claim open-template
  completeness.
- Likely touchpoints (non-binding): `src/providers/cgal/TedToolkit.CppBindings.Cgal.Generator`, the
  provider profile manifest, solution/build registration, Generator fixtures, and package verifier.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| Parent PRE-01 | Shared owns normalized semantics and paired declaration emission | `../establish-provider-extension-boundary/change.md` is completed with AC-01 accepted |
| Parent PRE-02 | Provider source topology and one-way dependencies are mechanically enforced | `../establish-provider-extension-boundary/change.md` is completed with AC-03 accepted |
| Locked discovery matrix | CGAL 6.2 is installed for `x64-windows` from the parent-pinned vcpkg baseline | `C:/vcpkg/installed/x64-windows` and the provider-local manifest/configuration |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-01 | Owns | Deterministic finite profile, complete partitioned inventories, and matching managed/native plan |
| AC-03 | Supports | Supplies the exact admitted surface and native-project metadata compiled and packed by CGAL-003 |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior: Shared
  remains free of CGAL knowledge; `epick-windows-v1` is finite and versioned; every reachable
  declaration receives exactly one admitted or narrow unsupported disposition; unproved layout,
  transport, lifetime, invocation, or template semantics fail closed; output ordering and hashes
  are stable; existing OCCT generation remains unchanged.
- Private choices deliberately left to the implementer: Clang traversal services, internal model
  decomposition, profile serialization implementation, diagnostic codes, and generator file split,
  provided the public profile and inventory behavior remains deterministic and complete.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Two real-header runs over the pinned CGAL installation produce byte-identical source/candidate/admitted/unsupported/managed/native inventories; admitted and unsupported entries exactly partition the finite candidate set | `dotnet run --project tests/TedToolkit.CppBindings.Cgal.Generator.Tests/TedToolkit.CppBindings.Cgal.Generator.Tests.csproj -c Release -- --report-trx`; then `pwsh -NoProfile -File Build/VerifyCgalGeneratorPackage.ps1` |
| AC-03 | Conditional | Every admitted declaration has paired generated artifacts and native build metadata consumable by the Windows item | The package verifier validates inventory-to-artifact identity and emits a machine-readable result for CGAL-003 |
| OCCT regression | Conditional | Adding the provider introduces no Shared provider-name branch and does not change OCCT focused generation | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1`; run the OCCT Generator TUnit project |

<!-- work-item: definition-of-done -->
## Done

- The Generator package, finite default profile, source/candidate/admitted/unsupported inventories,
  paired generated sources, and native project metadata implement AC-01.
- Primary proof passes twice against the locked CGAL 6.2 installation with no skipped profile
  partition and produces the verified inputs named by CGAL-003.
- Shared/provider boundaries and focused OCCT generation remain green, and any public Generator or
  profile documentation is current.

<!-- work-item: completion-evidence -->
## Verification result requirements

The implementation handoff must record the exact candidate revision, changed Generator/profile
artifacts, AC-01 proof purpose and integration shape, both commands, deterministic run counts and
inventory hashes/counts, resolved CGAL/vcpkg inputs, documentation state, and the verified profile,
inventory, paired-source, and native-project outputs supplied to CGAL-003.

## Verification result

- Candidate: `397ff49e406ac954dcb9fb55fe0c2b0179ae5ba4`, reviewed independently as Ready.
- Evidence: `out/verification/cg-397ff49/result.json`; package consumer, generated managed build,
  generated native CMake/MSVC build, CGAL Generator TUnit, Shared Generator TUnit, OCCT Generator
  TUnit, and provider-boundary checks all passed from a clean exact candidate.
- Deterministic inventory: 3,773 installed headers; 19,627 compiler-observed source declarations;
  3,698 finite candidates partitioned into 19 admitted and 3,679 unsupported declarations; 17
  exact native exports. Repeated managed and native inventories matched their recorded SHA-256
  hashes.
- Locked inputs: CGAL 6.2, GMP 6.3.0#5, MPFR 4.2.2#1, `x64-windows`, CMake 4.4.3, and MSVC
  19.51.36256 with the recorded vcpkg ABI identities.
- Supplied to CGAL-003: the embedded `epick-windows-v1` manifest, source/candidate/admitted/
  unsupported inventories, paired generated managed/native sources, exact export table, and CMake
  project metadata. Current provider and Generator READMEs describe the implemented finite-profile
  accounting and borrowed-reference lifetime contract.
- Integration: fast-forwarded unchanged implementation candidate `397ff49e406ac954dcb9fb55fe0c2b0179ae5ba4`
  into `codex/deliver-cgal-provider`; the only later item-branch commit recorded this evidence and
  status, so the independently reviewed code and retained exact-candidate proof are unchanged.

## Risks and implementation notes

CGAL header volume and template recursion may make unrestricted discovery impractical. The finite
manifest is the authority: reject unsupported declarations narrowly and keep every exclusion
visible rather than silently shrinking the candidate set.
