# CGAL-003: Pack and consume the Windows EPICK binding

<!-- work-item-format: 2 -->
<!-- work-item-id: CGAL-003 -->

<!-- approval-source: The maintainer explicitly approved this exact work-item map with “批准。” in the Codex task on 2026-09-07. -->

## Outcome

Deliver `TedToolkit.CppBindings.Cgal.Windows` as a self-contained `win-x64` package for the complete
admitted `epick-windows-v1` surface, and prove a clean consumer performs the parent-specified real
2D/3D operations and observes CGAL precondition diagnostics.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: the CGAL Windows package, locked native
  build, recursive DLL/notices bundle, package verifier, real consumer, and current consumer docs.
- In scope: provider-local vcpkg manifest/configuration and ABI identity recording; generation from
  verified CG profile; native wrapper compilation with `CGAL_DEBUG`; generated managed assembly;
  exact managed/native inventory agreement; recursive imported non-system DLL closure; CGAL/GMP/
  MPFR and dependency notices; isolated package restore; real Point_2, Point_3, squared-distance,
  Segment_2 intersection/none, and invalid-index calls; complete OCCT regression gate.
- Non-goals: publish packages, support another RID/kernel/compiler matrix, install dependencies on a
  consumer machine, ship demos/Qt, or widen a rejected Generator declaration during packaging.
- Likely touchpoints (non-binding): `src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows`,
  provider-local native CMake/vcpkg inputs, build modules/verifiers, Windows consumer fixture,
  solution/package metadata, root/architecture/provider docs, and notices.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| CGAL-001 verified | Stable profile identity, complete admitted/unsupported partition, paired sources, and native build metadata | CGAL-001 is `Verified` in `../work-items.md` with passing item proof |
| CGAL-002 verified | Stable Runtime exception/result API and compiled native cleanup ABI | CGAL-002 is `Verified` in `../work-items.md` with passing item proof |
| Locked Windows matrix | CGAL 6.2, GMP 6.3.0#5, MPFR 4.2.2#1, `x64-windows`, MSVC 19.51.36256, CMake 4.4.3 | Provider-local manifest/configuration plus verifier-recorded resolved package/compiler identities |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | Compiled and packed complete admitted surface, recursive native dependency closure, notices, and matrix identity |
| AC-04 | Owns | Clean packed-package consumer with exact real 2D/3D values, intersection alternatives, none, and precondition exception |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior: package
  identity is `TedToolkit.CppBindings.Cgal.Windows`; generated API namespace is profile-controlled,
  not package-suffix-controlled; consumers need no CGAL/vcpkg/GMP/MPFR installation; imported
  non-system DLLs and applicable notices are complete; managed/native hashes and inventories match;
  exact locked matrix is enforced; OCCT Generator/Runtime/Windows remain usable and unchanged.
- Private choices deliberately left to the implementer: dependency-scanner implementation, native
  build directory layout, package staging mechanics, and consumer harness decomposition, provided
  the verifier starts clean and proves the approved package boundary.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-03 purpose=boundary shape=integration -->
<!-- primary-proof: AC-04 purpose=journey shape=end-to-end -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | The locked native build compiles every admitted declaration; the just-built package contains the matching managed/native artifacts, recursive non-system DLL closure (including imported `gmp-10.dll` and `mpfr-6.dll`), notices, and recorded matrix/ABI identities | `pwsh -NoProfile -File Build/VerifyCgalWindowsPackage.ps1` |
| AC-04 | Primary | An isolated project restores only the just-packed packages and obtains 25, 9, Point_2(1,0), no intersection, and `CgalPreconditionException` with nonempty native diagnostics from real calls | Execute the clean consumer prepared by `Build/VerifyCgalWindowsPackage.ps1` and validate its machine-readable result |
| AC-01–AC-04 | Conditional | Integrated packages retain verified Generator/Runtime behavior and all existing OCCT tests, package consumers, native integration, and full repository gates remain green | Run focused CGAL projects, OCCT package verifiers, and `dotnet run --project Build/Build.csproj -c Release` |

<!-- work-item: definition-of-done -->
## Done

- The Windows package contains exactly the complete admitted profile surface, matched native DLL,
  recursive dependency closure, and required third-party notices for the locked matrix.
- Primary package and real-consumer proof pass from fresh evidence directories with no installed
  CGAL/vcpkg dependency assumed by the consumer.
- The integrated exact candidate passes all CGAL item proofs, full OCCT/repository regression gates,
  independent implementation review, and current architecture/package/consumer documentation.

<!-- work-item: completion-evidence -->
## Verification result requirements

The implementation handoff must record the integrated candidate revision, changed Windows/build/
consumer/docs artifacts, AC-03 and AC-04 proof purposes and shapes, package and full-gate commands,
admitted/native/package/dependency/notices counts and hashes, exact toolchain/vcpkg ABI identities,
real consumer values and exception diagnostics, OCCT regression results, and final documentation
and dependent-output disposition.

## Risks and implementation notes

Dependency copying must follow actual PE imports recursively and distinguish Windows system DLLs
from app-local dependencies. Packaging must fail if the tested native binary, admitted inventory,
packed binary, and restored consumer asset hashes do not identify the same build.

## Implementation handoff

- Implementation candidate: `a253ccbaadcf6f33b717a05a59c9478ffcc27307`.
- Delivery artifacts: the CGAL Generator tool host, Windows package project, locked CMake compiler
  identity probe, generation/build/cache script, package verifier, isolated package consumer,
  solution/build-gate wiring, and current root/provider/architecture documentation.
- AC-03 and AC-04 evidence: `out/verification/cw-a253ccb/result.json`. A fresh evidence-local build
  admitted and emitted 19 matching managed/native entries, populated 17 contiguous function-table
  slots behind the sole public `NativeApi_GetFunctionTable` export, staged five imported app-local
  DLLs and three notices, and authenticated the complete 38-file output manifest. Deliberate managed
  output corruption invalidated the cache and the next build restored the original hash.
- Locked build identity: CGAL 6.2, GMP 6.3.0#5, MPFR 4.2.2#1, CMake 4.4.3, and MSVC
  19.51.36256.0 from toolset 14.51.36231. The verifier recorded the resolved CGAL/GMP/MPFR vcpkg
  ABI identities and derived the redistributable closure from CMake's selected compiler.
- CGAL package identity: Windows package SHA-256
  `805B853137151C3B6B8C24A3C26B60861FC86F0A5078E6B030E26CC88D3B0E80`; managed assembly
  SHA-256 `B2E893DA99658C5503E1AE6C4190F772C4E1DE58B775A59C57B123BF03973209`; native wrapper
  SHA-256 `33C18E70B5A055CC5D3406E16C8ABF85BC8E29B39239ED27C2ADECB8571FD3D0`.
- Real CGAL consumer result: squared distances 25 and 9, a Point intersection at `(1, 0)`, a
  disjoint `None`, and `CgalPreconditionException` with copied nonempty native type, message, and
  stack text. Its restored package graph contained only Shared Runtime, CGAL Runtime, and CGAL
  Windows.
- Exact-candidate repository gate: `dotnet run --project Build/Build.csproj -c Release --no-build
  --no-restore` completed successfully in 57m04s. It generated and linked all 6,989 OCCT wrappers;
  passed Shared Generator 9/9, CGAL Generator 7/7, CGAL Runtime 6/6, OCCT Generator 143/143,
  Shared Runtime 40/40, and Analyzers 17/17; and passed the solution build, native integration,
  managed test gate, and final assertion modules.
- OCCT preservation evidence: `out/verification/wp-a253ccb/result.json` and its isolated consumer
  passed direct diagnostics and real native calls. The packed OCCT native wrapper SHA-256 is
  `17B8AC2E692F43E376B89CE38A3202F27980D836AA4F07376C92A788FC132122`; the consumer checked 6,792
  closed layouts, 6,986 prepared native layouts, 194 closed generic specializations, cyclic handle
  behavior, and generated value/Owned/Handle/borrowed/inheritance/error paths.
- Independent implementation review: Ready to merge. Cache authentication, actual compiler and
  redistributable provenance, function-slot/export proof, package consumption, and full OCCT/
  repository regression have no remaining blocker or important finding.
- Dependent-output disposition: exact CGAL-001, CGAL-002, CGAL-003, current OCCT package, and
  documentation-referenced historical evidence remain. Thirty-seven unreferenced superseded
  verifier directories were deleted after path-boundary validation, reclaiming 9.11 GiB; they are
  generated and recoverable from their source revisions.
