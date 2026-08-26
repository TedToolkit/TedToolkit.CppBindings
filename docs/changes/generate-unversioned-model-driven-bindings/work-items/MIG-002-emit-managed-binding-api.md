# MIG-002: Emit the exact-layout managed binding API

<!-- work-item-format: 2 -->
<!-- work-item-id: MIG-002 -->

<!-- approval-source: Explicit maintainer approval of the complete MIG-001 through MIG-005 map in the Codex task on 2026-08-26. -->

## Outcome

The completed Model emits one deterministic C# binding surface containing exact-layout structs,
inheritance interfaces, registered generic APIs, static factories, and extension methods over
values, `Handle<T>`, or `Owned<T>` under one validated configurable root namespace. Every receiver,
parameter, result, factory, copy, release, destruction, and cleanup shape follows the Model's
proved category and operation lifetime flow without emitter-side reclassification. Every required
native export has one deterministic generated table slot, and every ordinary call uses that slot
through its exact unmanaged Cdecl function-pointer signature.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: C# storage declarations from MIG-001 projections; inheritance interfaces; public and
  transport types; registered generic APIs; `in`/`ref` value extension receivers; Handle/Owned
  receivers; static factories; managed imports and invocation glue; use of Runtime's ordinary
  public owner construction and non-owning `Value` contracts; lexical `fixed` scopes and owner
  keepalive for native invocation; classification-driven parameter and result projection;
  deterministic function-table slot declarations and exact typed table dispatch;
  complete unsupported dispositions; `CSharpNamespace`; deterministic public API/source baselines.
- Non-goals: redefining physical layout, Runtime owner implementation, C declarations, C++ adapter
  bodies, manifest/fingerprint ownership, native compilation, exact-match loading, or packaging.
- Likely touchpoints (non-binding): Generator C# emitter, generation options and validation,
  managed mapping policies, source inventory, public API baselines, and Generator tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| MIG-001 complete | Canonical declarations, exact storage projections, generic registrations, ownership categories, and dispositions |
| MIG-001 operation flows | Complete receiver, parameter, result, borrowing, transfer, construction, copy, destruction, and cleanup semantics |
| Runtime owners complete | Approved and verified `Handle<T>` and `Owned<T>` public integration surfaces usable by any wrapper assembly |
| Managed target baseline | Active architecture `net8.0` Runtime and generated-package target contract |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-04 and AC-07 and supply managed public/import identities for AC-01, expected contract inputs
  for AC-05, and generated managed consumers for AC-06.
- Consume the Model's single ownership category and operation lifetime flow without reclassifying a
  type or reconstructing ownership from generated syntax. Emit no callable fragment for an
  unsupported declaration or operation.
- Emit no descriptor class, managed class inheritance, `BaseType` storage field, ownership-bearing
  struct, public raw pointer, `.Value`, public retain, public address constructor, or public common
  owner base.
- Express inheritance through generated interfaces and generic constraints. Base-declared calls use
  native-proved pointer adjustment without owner conversion or boxing.
- Emit C++ `const` value operations with `this in T`, mutating value operations with `this ref T`,
  transient operations over `Handle<T>`, and RAII operations over `Owned<T>`. Static operations and
  factories remain static.
- For every owner-derived native argument, obtain the pointer only inside a lexical `fixed` scope
  over `owner.Value`, keep the pointer inside that scope, and emit `GC.KeepAlive(owner)` immediately
  after that finalizable owner's last unmanaged use and before managed error projection. Use the
  same shape for Handle and Owned; emit no `Unsafe.AsPointer`, generated-only pointer member, static
  pointer gateway, common owner interface, callback, or invocation lease.
- Return plain structs only for proved trivial values, `Handle<T>` only for transient identities,
  and `Owned<T>` only for approved non-transient RAII identities.
- Derive every receiver, parameter, result, factory, borrow, transfer, copy, release, destruction,
  and cleanup projection from the same Model facts used by the native emitter. A missing fact makes
  the complete operation unsupported rather than selecting a default call shape.
- Derive one stable generated slot for every required operation and cleanup export. Ordinary calls
  read the process-lifetime table and cast only to the exact Model-derived unmanaged Cdecl
  signature. Factories read release or destructor slots once and pass the typed pointer to the
  corresponding Runtime owner; generated cleanup never passes a table or index to Runtime.
- Emit `IOcctRaii` on every MIG-001-approved non-transient RAII exact-layout struct, including
  eligible `TCollection_*` records, and on no trivial or `Standard_Transient` projection. Reference
  Runtime's single marker definition and constrain `Owned<T>` with `unmanaged, IOcctRaii`.
- Emit each Owned factory by creating the Runtime owner whose private `T` field is the destination,
  passing the proved native `alignof(T)` and matching destructor, placement-constructing that field
  under `fixed`, and returning the owner only after native success. Use a local success flag and
  `finally` to suppress finalization on every exit before native success is established. On native
  failure, project the error only after suppression; emit no construction-completion API or second
  storage object. Reject a type unless the pinned CLR target proves its direct field preserves the
  required alignment across GC relocation.
- Call the `GeneratedCodeOnly` Runtime constructor only from generated source. Public consumer
  factories expose the corresponding C++ construction shape and never require handwritten
  `new Owned<T>(...)`; analyzer fixtures prove direct handwritten construction reports `TTOCCT001`.
- Reference the single Runtime owner definitions and emit no Handle/Owned implementation or source
  into Runtime. The generated assembly uses only ordinary public Runtime members and receives no
  `InternalsVisibleTo`, assembly-name privilege, reflection, or private-member access.
- Consume no ABI-v1 declaration, import, fixture, generated text, or public baseline as semantic
  input or expected behavior.
- Validate `CSharpNamespace` before any output mutation: require a non-empty dot-separated C#
  identifier sequence, deterministically escape keyword segments, reject malformed input with
  `ArgumentException`, and default to `TedToolkit.Occt`.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-01 through GEN-04 and the active generated binding architecture.
- Runtime remains declaration-agnostic; concrete layouts, interfaces, type identities, imports,
  expected fingerprints, function-table slots and storage, pointer adjustments, and specialization
  registrations remain generated.
- Escalate a required routine long-lived raw-address path, boxing receiver, copied mutating
  receiver, pointer escape from `fixed`, missing owner keepalive, owner conversion, generated Runtime
  source, friend access, emitter-side ownership classification, ABI-v1 dependency, or public owner
  integration surface incompatible with `net8.0`.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-04 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-07 purpose=boundary shape=component -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-04 | Primary | Controlled and real Model inputs emit the approved value, Handle, and Owned surfaces, deterministic slots and exact typed table calls, cleanup-pointer handoff, lexical `fixed`, owner keepalive, and no Runtime table/index dependency or prohibited API; generated construction, call, copy, release, destruction, and failure paths complete exactly once against a native lifecycle fixture | Run the Generator Release managed-emission/public-surface TUnit suite and its generated managed/native lifecycle Integration fixture |
| AC-07 | Primary | Valid namespaces apply coherently, while malformed namespaces throw before cleaning or materializing output | Run the Generator Release namespace validation and output-preservation component suite |

<!-- work-item: definition-of-done -->
## Done

AC-04 and AC-07 pass; every supported object and operation consumes one Model-owned
category and lifetime flow; MIG-003 receives canonical managed operation/import/slot identities;
MIG-004 receives generated expected-contract inputs, slots, required export set, and typed call
sites; MIG-005 receives a generated managed consumer;
no emitter reclassification, partial operation, ABI-v1 input, prohibited representation, receiver,
owner, or Runtime dependency appears.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, generated source/API baselines, owned contract IDs, commands,
namespace, classification, operation-flow, fail-closed, and public-surface assertions, absence of
ABI-v1 inputs, discovered/passed/failed/skipped counts, Runtime baseline, documentation state, and
exact outputs supplied to MIG-003 through MIG-005.
