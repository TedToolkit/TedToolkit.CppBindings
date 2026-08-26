# MIG-005: Replace and prove the exact-layout native boundary

<!-- work-item-format: 2 -->

- Approval: None while Draft. Start requires the verified MIG-001 through MIG-004 inputs.

## Outcome

When optional native compilation is selected, the generated unversioned project builds against the
pinned OCCT closure and passes strict C11 and generated managed consumers. The passing replacement
becomes the only active source, build, fixture, output, and current-documentation path.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: generated-project materialization; native compile/link/runtime integration; build and
  module identity; strict C11 export consumption; exhaustive layout probe execution for every
  emitted object and registered closed generic; representative trivial, inherited transient,
  generic, non-transient RAII, mutation, exception, and cleanup calls; same-library release;
  removal of ABI-v1 and obsolete descriptor/explicit-offset active paths; verified package input.
- In scope: preserving source-only generation while proving that selected compilation consumes the
  exact emitted project without reparsing or changing Model semantics.
- Non-goals: changing contracts owned by MIG-001 through MIG-004, increasing declaration coverage,
  packing or publishing NuGet, or adding another platform.
- Likely touchpoints (non-binding): generated native build materialization, native presets, strict C
  consumer, generated managed integration fixtures, current READMEs/guides, and obsolete ABI-v1
  source/build/tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required verified input |
| --- | --- |
| MIG-001 complete | Canonical Model, build identity, every layout/generic proof input, and dispositions |
| MIG-002 complete | Generated managed API, owners, namespace, imports, and public baseline |
| MIG-003 complete | Complete deterministic C/C++ project, manifest/fingerprint, bootstrap, and build description |
| MIG-004 complete | Matching/missing/different fingerprint behavior and generated managed initialization |
| Pinned environment | Exact machine-readable Windows x64 OCCT/compiler/build identity and required native dependencies |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-06 and supply real-boundary evidence supporting AC-01 through AC-05.
- Prove source-only generation does not invoke the compiler and selected compilation begins only
  after complete C# and C++ source materialization.
- Execute the exhaustive native/managed size, alignment, packing, and physical-segment matrix for
  every emitted object and registered closed generic; no representative sample substitutes for this
  layout contract.
- Use representative real calls to prove construction, value mutation, interface pointer
  adjustment, transient lifetime, RAII placement/clone/destruction, errors, active-call lifetime,
  and same-library cleanup.
- Remove active ABI-major/version scaffolding, descriptor assumptions, explicit-offset projections,
  handwritten adapters/imports, and current guides describing them only after replacement proof
  passes.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Active code and current documentation describe only the generated replacement architecture after
  replacement.
- Strict C11 proof validates export syntax and pointer transports; it does not claim that C
  consumers can interpret C++ object layouts outside the pinned contract.
- Escalate any layout mismatch, dependency loaded outside the candidate closure, cleanup through
  another module, public contract change, or requirement to retain ABI-v1 compatibility.

<!-- work-item: proof-plan -->
## Proof

| Parent contract | Evidence purpose and shape | Observable proof |
| --- | --- | --- |
| AC-06 | Acceptance/boundary Integration | Generated native build and strict C11 and managed consumers pass; active legacy paths are absent |
| AC-02 / AC-03 | Boundary/compatibility Integration | Exhaustive native/managed probes agree for every emitted layout and registered generic specialization; unsupported `T` fails before native access |
| AC-04 | Acceptance/regression Integration | Representative value, Handle, Owned, inheritance-extension, clone, dispose, finalizer, error, and cleanup journeys match OCCT and release once |
| AC-05 | Boundary regression | Matching artifact initializes; changed layout/lifetime and missing-bootstrap fixtures fail before operation resolution |

Build the generated native project in Release, run its CTest strict-C consumer, run exhaustive layout
probes, then run Generator and Runtime TUnit projects in Release with TRX output. Run structural
searches proving obsolete active identifiers and projections are absent outside historical records.

<!-- work-item: definition-of-done -->
## Done

The replacement passes on the pinned candidate; every emitted layout and registered generic is
proved; the exact generated boundary is the only active path; every required dependency and cleanup
origin is recorded; the Draft `TedToolkit.Occt.Windows` package receives the verified candidate;
and no NuGet publication or additional platform work occurs.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the integrated candidate revision, generated/native artifacts, commands, exhaustive layout
counts/assertions, strict-C and managed results, representative lifecycle/error journeys, module and
cleanup origins, removed active paths, documentation state, and verified package inputs.
