# CPB-002: Reusable generation platform with a functioning OCCT provider

<!-- work-item-format: 2 -->
<!-- work-item-id: CPB-002 -->
<!-- approval-source: Maintainer explicitly approved the complete CPB-001 through CPB-003 map and item set with "批准。" in this task on 2026-09-04. -->

## Outcome

A separate neutral consumer can generate through the public generic platform, and the migrated
OCCT provider produces working exact-layout bindings through the same boundary and renamed solution.

<!-- work-item: scope -->
## Scope and non-goals

- Deliver the approved Generator/provider options, public provider extension family, generic stages,
  OCCT parse/probe/registration entry points, and provider-owned header source generator.
- Include smallest external neutral consumer, OCCT generation host, generated smoke/native consumers,
  Windows package, renamed solution, and build/project graph needed to prove complete integration.
- Correct generated transient receivers to the direct owning/non-owning overloads required by AC-05;
  preserve and test the existing template-projection edits rather than replacing them with a baseline copy.
- Likely touchpoints: current Generator, header Analyzer, Console, Generator.Tests, GeneratedSmoke,
  Windows package, solution, shared build controls, and generation/package-local documentation.
- Non-goals: another production provider, new platform/ABI, public provider model graph, performance
  strategy adoption, repository remote cutover, and publication.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| CPB-001 | Verified Runtime surfaces, lifetime semantics, package identities, and direct analyzer consumption available to generated consumers | Authoritative integration revision with passing AC-03 / AC-04 and native-wrapper evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-02 | Owns | Complete one-way project graph, approved public Generator dispositions, external neutral provider proof, and renamed-solution build |
| AC-05 | Owns | Deterministic real OCCT generation and native operation/lifetime regression, including direct Handle/handle overloads |

<!-- work-item: delivery-constraints -->
## Constraints

- Implement the parent provider boundary without public OCCT models, friend access for the neutral
  consumer, or reflection-based registration. Generic stages consume one completed plan per run.
- Preserve preparation dependencies, exact shared export ordering, output containment/collision
  checks, cancellation/failure propagation, and existing render execution policy. Do not render
  before native preparation completes; failed preparation cannot produce empty successful output.
- Separate provider-specific handle/error/header/native dependency emission from shared source
  publication and function-table/loading mechanics. Keep all ownership and fail-closed invariants.
- Generate separate direct `Handle<T>` and `in handle<T>` receivers to the same NativeApi slot;
  only the owning overload checks owner liveness. No common receiver interface or Core forwarding.
- Apply the parent's approved native bitfield correction: same-named, same-typed value properties
  preserve native bit widths and signed read/write behavior without exposing an address or ref.
  Prove single and adjacent bitfields against the native compiler; retain the complete representable
  declaration set and verify generic storage without changing ownership allocation. Apply the parent's
  separately approved alignment admission disposition before paired emission, with deterministic
  diagnostics and dependency closure at the narrowest affected declaration/member boundary.
- Apply the parent's separately approved cyclic handle field correction only to storage edges
  responsible for type-loading failure. Preserve typed in-place references, constness, native
  metadata, exact storage, all loadable ordinary fields and the complete representable declaration
  set. Prove the generated cycles on net8.0, including managed-heap aliasing across relocation and
  the actual closed-generic inventory; do not change either handle type or Owned<T> storage.
- Investigate the current native-loader implementation versus ADR-001 using repository evidence;
  preserve approved behavior and supply the evidence to CPB-003. A new enduring loading decision
  requires the architecture route and any material contract change requires renewed design approval.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-02 purpose=structural shape=component -->
<!-- primary-proof: AC-05 purpose=regression shape=integration -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-02 | Primary | Generic source/metadata and dependencies contain no provider knowledge; every project/public family has its approved responsibility; a neutral package consumer registers without OCCT or privileged access | Inspect evaluated project graph, sources, metadata, and exported APIs; pack generic Generator and run a separate neutral consumer exercising preparation order, deterministic shared exports, invalid paths/collisions/duplicate exports, cancellation, and failures; run `dotnet build TedToolkit.CppBindings.slnx -c Release` |
| AC-05 | Primary | Real pinned OCCT bindings preserve exact layouts, supported operations and lifetimes, direct receivers, and pre-native rejection of unsupported declarations/RIDs | Run migrated Generator, Runtime, and analyzer TUnit projects with `dotnet run --project <test-project> -c Release --no-build -- --report-trx` after Release builds; run the generation/native-build/GeneratedSmoke pipeline and native integration/CTest on the pinned win-x64 matrix |
| Deterministic output | Conditional | Repeated identical prepared input produces identical relative file/content manifests and native export order on both sides | Repeat generation in isolated output roots and compare normalized manifests/export inventories; no timestamp or throughput claim substitutes for content proof |
| Template and packaging regression | Conditional | Preserved dirty template projection compiles and behaves correctly; packaged OCCT generator discovers its header source generator and generated consumers resolve the split Runtime/analyzer assets | Run targeted template tests plus representative native calls; inspect and consume packed OCCT Generator/Windows assets without publishing |

<!-- work-item: definition-of-done -->
## Done

Both owned cases and conditional gates pass on one integrated candidate. Supply CPB-003 the verified
solution/project graph, package/public inventory, real native proof, loader/ADR evidence, and accurate
consumer/build instructions. No old partial identity is presented as a supported public release.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record exact candidate/integration revision and included dirty-template delta, changed artifacts,
AC IDs, purposes/shapes, concrete commands, counts/skips/failures, output manifests and logs, native
toolchain/dependency pins, packed-asset identities, documentation state, and supplied CPB-003 inputs.
Keep mutable progress/results outside this stable contract and status only in the map.
