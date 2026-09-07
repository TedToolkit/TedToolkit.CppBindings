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
