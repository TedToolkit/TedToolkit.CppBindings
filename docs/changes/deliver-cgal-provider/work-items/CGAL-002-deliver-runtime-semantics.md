# CGAL-002: Deliver CGAL Runtime semantics

<!-- work-item-format: 2 -->
<!-- work-item-id: CGAL-002 -->

<!-- approval-source: The maintainer explicitly approved this exact work-item map with “批准。” in the Codex task on 2026-09-07. -->

## Outcome

Deliver `TedToolkit.CppBindings.Cgal.Runtime` and a compiled native-boundary fixture that prove the
fixed CGAL exception taxonomy, finite optional/variant/Object result projection, and exact-once
diagnostic/container cleanup without any OCCT dependency.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: the CGAL Runtime package, its public
  exception hierarchy and native error/result projection boundary, plus compiled fixture tests.
- In scope: every parent-specified CGAL and standard failure mapping; copied native type/message/
  stack properties; malformed UTF-8 and fallback behavior; `None`, known-alternative, undeclared
  alternative, and invalid-tag behavior; operation-specific readonly result contract needed by
  generated code; exact-once clearing/destruction on success and all failure paths.
- Non-goals: discover CGAL headers, select the EPICK surface, build the final Windows package,
  bundle CGAL/GMP/MPFR, expose `std::variant`/`boost::any`, or add CGAL policy to Shared Runtime.
- Likely touchpoints (non-binding): `src/providers/cgal/TedToolkit.CppBindings.Cgal.Runtime`, a
  provider-local native fixture, Runtime TUnit tests, solution registration, and package README.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| Parent PRE-01 | Shared Runtime exposes provider-neutral ownership and native diagnostic primitives | `../establish-provider-extension-boundary/change.md` is completed with AC-01 accepted |
| Native toolchain | A C++20 Windows compiler can build the bounded failure/result fixture | Parent-pinned MSVC 19.51.36256 environment |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-02 | Owns | Public exception taxonomy, result semantics, ABI projection, and exact-once cleanup proof |
| AC-04 | Supports | Supplies the Runtime API and native failure/result contract used by the packaged real consumer |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior: exception
  names and inheritance match the approved parent contract; native diagnostics are consumed once
  in `finally`, including conversion failure; native temporaries are destroyed once after transfer
  or failure; undeclared nonempty alternatives fail with `CgalUnknownResultException`; Runtime has
  no OCCT reference and Shared remains provider-neutral.
- Private choices deliberately left to the implementer: internal tag representation, fixture export
  names, safe-handle/helper decomposition, and test harness layout, provided generated APIs can use
  the documented stable Runtime contract.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-02 | Primary | Compiled calls cover success, empty, every declared alternative, unknown tag, all fixed CGAL/standard/unknown failure categories, malformed UTF-8, and exact-once cleanup without an OCCT assembly or type | `dotnet run --project tests/TedToolkit.CppBindings.Cgal.Runtime.Tests/TedToolkit.CppBindings.Cgal.Runtime.Tests.csproj -c Release -- --report-trx` |
| Runtime package | Conditional | A clean project restores the just-packed Runtime and compiles representative generated exception/result consumers without Generator or OCCT dependencies | Run the Runtime package-consumer verifier emitted by this item |

<!-- work-item: definition-of-done -->
## Done

- The public Runtime API and compiled fixture implement every AC-02 outcome and exact-once cleanup
  invariant silently across success and all failures.
- Primary and package-consumer proof pass with zero skips and supply the verified Runtime/result ABI
  contract named by CGAL-003.
- Runtime package documentation states ownership, lifetime, failure, and unknown-result behavior and
  confirms that no OCCT semantics are inherited.

<!-- work-item: completion-evidence -->
## Verification result requirements

The implementation handoff must record the exact candidate revision, changed Runtime/fixture
artifacts, AC-02 proof purpose and integration shape, executed command, result counts for every
failure/result/cleanup partition, compiler resource prerequisites, package/documentation state, and
the verified exception/result ABI contract supplied to CGAL-003.

## Verification result

- Candidate: `489e8de77a216ee16673662723f713bdb6cc05eb`, reviewed independently as Ready.
- Evidence: `out/verification/cr-489e8de/result.json`; Runtime packaging, an isolated package
  consumer, the CGAL-linked native fixture, Runtime TUnit, and Generator compatibility TUnit all
  passed from a clean exact candidate.
- Runtime proof: 6/6 tests passed with no skips. Fourteen real CGAL, standard-library, and unknown
  failure scenarios verified exact exception/type mapping, scenario-specific copied message text,
  native stack behavior, and one diagnostic clear. Malformed UTF-8 and success cleanup paths were
  verified separately.
- Result proof: actual `std::optional<std::variant>` and `CGAL::Object` containers each passed empty,
  Point_2, Segment_2, undeclared-alternative, and invalid-tag partitions with exact create, destroy,
  and transfer counters. The compiled projection matched the current Generator output; Generator
  compatibility tests passed 7/7 with no skips.
- Locked native input: MSVC `19.51.36256.0`, recorded through project-owned CMake metadata. The
  Runtime package exposes 16 public types, references Shared Runtime only, and its isolated consumer
  restored the exact package hash recorded in the evidence.
- Supplied to CGAL-003: the verified Runtime exception taxonomy, diagnostic ownership boundary,
  operation-specific result ABI, and exact-once native temporary cleanup contract. Runtime and CGAL
  provider documentation describe the implemented ownership, failure, and result semantics.
- Integration: fast-forwarded the unchanged implementation and evidence record through
  `81dd0253fd095350845adb608ef13aabdbb99a1b` into `codex/deliver-cgal-provider`; this later status
  update does not change the independently reviewed implementation candidate or its retained proof.

## Risks and implementation notes

The dangerous path is double cleanup or loss of native diagnostics during UTF-8 conversion and
polymorphic transfer. Counters in compiled fixtures must distinguish clear, container destruction,
alternative transfer, and managed projection so a passing exception assertion cannot hide a leak.
