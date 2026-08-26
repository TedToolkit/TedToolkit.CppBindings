# MIG-001: Compile one exact semantic and physical model

<!-- work-item-format: 2 -->
<!-- work-item-id: MIG-001 -->

<!-- approval-source: Explicit maintainer approval of the complete MIG-001 through MIG-005 map in the Codex task on 2026-08-26. -->

## Outcome

Pinned parsed `RecordModel` declarations compile into one canonical semantic and physical graph that
classifies every declaration and supported object, represents every complete-object layout with
ordered typed, padding, and opaque segments, records every operation lifetime flow, and proves each
eligible generic graph before later emitters run.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: one machine-readable native build identity; declaration coalescing and dispositions;
  size, alignment, packing, field, base, overlap, and hidden-state acquisition; sequential-layout
  compilation and CLR simulation; exact managed storage projection; generic physical graphs and
  registered closed-specialization evidence; compiler-backed value/Handle/Owned eligibility;
  receiver, parameter, result, borrowing, transfer, construction, copy, destruction, and cleanup
  semantics; deterministic identities and diagnostics.
- Non-goals: inheritance/extension/factory public API design, Runtime ownership implementation,
  C/C++ adapters, exact-match loading, real native compilation, package coverage expansion, or
  package creation.
- Likely touchpoints (non-binding): generation options, Clang input/configuration, declaration/type
  models, layout services, eligibility policies, physical projection emitter, diagnostics, and
  Generator tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| Approved parent and map | Revised AC-01 through AC-08 and five-item ownership boundaries |
| Pinned native build identity | OCCT version, vcpkg baseline, triplet, architecture, compiler/toolset, CRT linkage, packing, and layout-affecting build options |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-02, AC-03, and AC-08; supply canonical declaration, layout, generic, ownership-category,
  operation-flow, and disposition inputs for AC-01 and AC-04 through AC-06.
- Assign exactly one supported category before emission: a proved `Standard_Transient` descendant
  with complete intrusive-reference semantics uses Handle; a proved safely copyable non-transient
  object with no required cleanup is a value; and another non-transient object uses Owned only when
  construction, copy, destruction, alignment, and same-library cleanup are complete. Reject every
  ambiguous or incomplete classification.
- Classify eligible `TCollection_*` and other supported non-transient RAII records for generated
  `IOcctRaii` implementation; trivial and `Standard_Transient` records never receive that marker.
- Record complete receiver, parameter, result, borrowing, transfer, construction, and cleanup
  semantics for each supported operation. Reject the whole operation when its lifetime flow is
  incomplete; no emitter may infer or override those facts.
- Preserve `RecordModel` and attached type/method models as the only declaration graph. Physical and
  generic projections reference originating identities rather than copying them into a catalog.
- Represent each supported complete C++ object as an unmanaged sequential struct with no
  `FieldOffset`, managed `BaseType`, class inheritance, universal `Pack = 1`, or overlapping managed
  fields.
- Prove native and managed total size, alignment, packing, and every physical segment for every
  emitted object, including registered closed generic specializations.
- Emit one generic storage graph only when every registered closed specialization is represented by
  that graph. Otherwise select separately proved closed projection or unsupported disposition.
- Reject an incomplete native build identity, unrepresentable alignment, ambiguous overlap, or
  unsupported generic, ownership category, or operation lifetime flow before source materialization.
- Consume no ABI-v1 source, fixture, emitted text, or handwritten binding as model input, expected
  result, fallback, or compatibility target.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-01 through GEN-04 and the active generated binding architecture.
- Compiler-reported native layout is authoritative, but support exists only after independent CLR
  sequential-layout simulation and emitted-probe agreement.
- Escalate a required native layout that cannot be represented exactly or a template whose layout
  cannot be described by one proved generic graph or separately generated closed projections; a
  fourth ownership category, emitter-side reclassification, or ABI-v1 dependency also escalates.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-02 purpose=boundary shape=integration -->
<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-08 purpose=boundary shape=component -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-02 | Primary | Every emitted controlled and real object agrees with the pinned compiler for size, alignment, packing, and every physical segment, with prohibited layout forms absent | Run the Generator Release model/layout suite and exhaustive generated native/managed layout probes |
| AC-03 | Primary | Proved template specializations share only a valid generic physical graph, while unknown or mismatched closed types fail before native access | Run the Generator Release generic contract suite against controlled and real specializations |
| AC-08 | Primary | Removing or mutating any required build-identity field fails before layout acquisition or output mutation and preserves existing output | Run the Generator Release build-identity mutation component suite |

<!-- work-item: definition-of-done -->
## Done

AC-02, AC-03, and AC-08 pass; MIG-002 and MIG-003 receive one immutable semantic/physical model with exact
layout, generic, ownership-category, operation-flow, and disposition identities; every supported
object has exactly one proved category; no ABI-v1 input, second catalog, emitter reclassification,
or prohibited managed layout form exists.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, pinned native build identity, model/layout artifacts, owned contract
IDs, commands, emitted/probed type and specialization counts, size/alignment/segment assertions,
value/Handle/Owned and rejected classification counts, operation-flow assertions, unsupported
dispositions, absence of ABI-v1 inputs, determinism result, and exact inputs supplied to MIG-002 and
MIG-003.
