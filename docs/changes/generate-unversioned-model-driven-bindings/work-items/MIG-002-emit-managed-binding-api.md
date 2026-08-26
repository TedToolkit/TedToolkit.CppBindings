# MIG-002: Emit the exact-layout managed binding API

<!-- work-item-format: 2 -->

- Approval: None while Draft. Start requires MIG-001 and the approved Runtime ownership baselines.

## Outcome

The completed Model emits one deterministic C# binding surface containing exact-layout structs,
inheritance interfaces, registered generic APIs, static factories, and extension methods over
values, `Handle<T>`, or `Owned<T>` under one validated configurable root namespace.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: C# storage declarations from MIG-001 projections; inheritance interfaces; public and
  transport types; registered generic APIs; `in`/`ref` value extension receivers; Handle/Owned
  receivers; static factories; managed imports and invocation glue; use of Runtime's ordinary
  public owner construction and scoped-invocation contracts; `CSharpNamespace`; deterministic
  public API/source baselines.
- Non-goals: redefining physical layout, Runtime owner implementation, C declarations, C++ adapter
  bodies, manifest/fingerprint ownership, native compilation, exact-match loading, or packaging.
- Likely touchpoints (non-binding): Generator C# emitter, generation options and validation,
  managed mapping policies, source inventory, public API baselines, and Generator tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| MIG-001 complete | Canonical declarations, exact storage projections, generic registrations, ownership categories, and dispositions |
| Runtime owners complete | Approved and verified `Handle<T>` and `Owned<T>` public integration surfaces usable by any wrapper assembly |
| Managed target baseline | Active architecture `net8.0` Runtime and generated-package target contract |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-04 and supply managed public/import identities for AC-01, expected contract inputs
  for AC-05, and generated managed consumers for AC-06.
- Emit no descriptor class, managed class inheritance, `BaseType` storage field, ownership-bearing
  struct, public raw pointer, `.Value`, public retain, public address constructor, or public common
  owner base.
- Express inheritance through generated interfaces and generic constraints. Base-declared calls use
  native-proved pointer adjustment without owner conversion or boxing.
- Emit C++ `const` value operations with `this in T`, mutating value operations with `this ref T`,
  transient operations over `Handle<T>`, and RAII operations over `Owned<T>`. Static operations and
  factories remain static.
- Return plain structs only for proved trivial values, `Handle<T>` only for transient identities,
  and `Owned<T>` only for approved non-transient RAII identities.
- Reference the single Runtime owner definitions and emit no Handle/Owned implementation or source
  into Runtime. The generated assembly uses only ordinary public Runtime members and receives no
  `InternalsVisibleTo`, assembly-name privilege, reflection, or private-member access.
- Validate `CSharpNamespace` before any output mutation: require a non-empty dot-separated C#
  identifier sequence, deterministically escape keyword segments, reject malformed input with
  `ArgumentException`, and default to `TedToolkit.Occt`.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-01 through GEN-04 and the active generated binding architecture.
- Runtime remains declaration-agnostic; concrete layouts, interfaces, type identities, imports,
  expected fingerprints, pointer adjustments, and specialization registrations remain generated.
- Escalate a required routine long-lived raw-address path, boxing receiver, copied mutating
  receiver, owner conversion, generated Runtime source, friend access, or public owner integration
  surface incompatible with `net8.0`.

<!-- work-item: proof-plan -->
## Proof

| Parent contract | Evidence purpose and shape | Observable proof |
| --- | --- | --- |
| AC-04 | Acceptance/compatibility Public API Contract plus Component | Public source and two independently named compile-time wrapper fixtures prove exact structs, interfaces, registered generics, `in`/`ref` receivers, factories, separate Runtime-defined Handle/Owned surfaces, no friend access, and absence of prohibited APIs |
| Namespace contract | Acceptance/regression Component | Valid namespace variants apply to every generated C# artifact; keyword segments escape deterministically; malformed values fail with `ArgumentException` before output mutation |
| Determinism | Structural regression | Identical Model and namespace reproduce byte-identical C# source and public API baseline; changing only the namespace changes only managed identity and dependent C# text |

Run the Generator Release build and its TUnit managed-emission/public-surface suite against
controlled and real MIG-001 model inputs.

<!-- work-item: definition-of-done -->
## Done

AC-04 and namespace proof pass; MIG-003 receives canonical managed operation/import identities;
MIG-004 receives generated expected-contract inputs; MIG-005 receives a generated managed consumer;
no prohibited representation, receiver, owner, or Runtime dependency appears.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, generated source/API baselines, owned contract IDs, commands,
namespace and public-surface assertions, discovered/passed/failed/skipped counts, Runtime baseline,
documentation state, and exact outputs supplied to MIG-003 through MIG-005.
