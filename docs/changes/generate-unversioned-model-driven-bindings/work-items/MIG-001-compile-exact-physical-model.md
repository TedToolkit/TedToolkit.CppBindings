# MIG-001: Compile one exact semantic and physical model

<!-- work-item-format: 2 -->

- Approval: None while Draft. Start requires approval of the revised parent and delivery map.

## Outcome

Pinned parsed `RecordModel` declarations compile into one canonical semantic and physical graph that
classifies every declaration, represents every supported complete-object layout with ordered typed,
padding, and opaque segments, and proves each eligible generic graph before later emitters run.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: one machine-readable native build identity; declaration coalescing and dispositions;
  size, alignment, packing, field, base, overlap, and hidden-state acquisition; sequential-layout
  compilation and CLR simulation; exact managed storage projection; generic physical graphs and
  registered closed-specialization evidence; deterministic identities and diagnostics.
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
| Approved parent and map | Revised AC-01 through AC-06 and five-item ownership boundaries |
| Pinned native build identity | OCCT version, vcpkg baseline, triplet, architecture, compiler/toolset, CRT linkage, packing, and layout-affecting build options |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-02 and AC-03; supply canonical declaration, layout, generic, ownership-category,
  and disposition inputs for AC-01 and AC-04 through AC-06.
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
  unsupported generic before source materialization.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-01 through GEN-04 and the active generated binding architecture.
- Compiler-reported native layout is authoritative, but support exists only after independent CLR
  sequential-layout simulation and emitted-probe agreement.
- Escalate a required native layout that cannot be represented exactly or a template whose layout
  cannot be described by one proved generic graph or separately generated closed projections.

<!-- work-item: proof-plan -->
## Proof

| Parent contract | Evidence purpose and shape | Observable proof |
| --- | --- | --- |
| AC-02 | Acceptance/boundary Contract plus Integration | Every emitted controlled and real object projection agrees with the pinned compiler for size, alignment, packing, and every segment; prohibited layout forms are absent |
| AC-03 | Acceptance/regression Contract | Several `NCollection_Array1<T>` and another eligible template specialization share their proved generic storage graph; unknown or mismatched `T` fails closed |
| Determinism | Structural regression | Identical declarations and pinned build identity reproduce byte-identical model/layout identities and dispositions; any layout-affecting build identity change changes the contract identity |

Run the Generator Release build and its TUnit model/layout suite, including exhaustive generated
native/managed layout probes when the pinned toolchain is present.

<!-- work-item: definition-of-done -->
## Done

AC-02 and AC-03 pass; MIG-002 and MIG-003 receive one immutable semantic/physical model with exact
layout, generic, ownership-category, and disposition identities; no second catalog or prohibited
managed layout form exists.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, pinned native build identity, model/layout artifacts, owned contract
IDs, commands, emitted/probed type and specialization counts, size/alignment/segment assertions,
unsupported dispositions, determinism result, and exact inputs supplied to MIG-002 and MIG-003.
