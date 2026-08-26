# MIG-003: Emit the complete native binding contract

<!-- work-item-format: 2 -->
<!-- work-item-id: MIG-003 -->

<!-- approval-source: Explicit maintainer approval of the complete MIG-001 through MIG-005 map in the Codex task on 2026-08-26. -->

## Outcome

The completed Model and MIG-002's canonical managed import identity produce every supported
operation's C11 declaration, C++ adapter, manifest row, fingerprint input, and native build entry
with one canonical cross-layer identity. This item emits and verifies the native side without
re-emitting the managed import. Source-only generation finishes deterministically without invoking
a native compiler or using ABI-v1, generated text, a handwritten binding, or a handwritten
operation catalog as input or expected output.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: C-compatible pointer transports; deterministic export naming; one adapter source per
  canonical owning type plus type-independent support source; placement construction, destruction,
  intrusive reference operations, pointer adjustment, exception containment, error cleanup;
  Model-owned ownership classification and operation lifetime flow consumption; canonical
  manifest/fingerprint encoding; fixed bootstrap; generated source inventory and CMake; independent
  Model-only C/C++ emission.
- Non-goals: changing MIG-001 layouts, changing or re-emitting MIG-002 public API or managed imports,
  Runtime loading, real native compilation/linking, package coverage expansion, package creation,
  or handwritten final bindings.
- Likely touchpoints (non-binding): Generator C/C++ emitters, transport and operation mappings,
  manifest/fingerprint writers, build-description emission, source inventory, diagnostics, strict
  C syntax fixtures, and Generator tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| MIG-001 complete | Canonical semantic/physical Model, build identity, layouts, generic registrations, ownership categories, and dispositions |
| MIG-001 operation flows | Complete receiver, parameter, result, borrowing, transfer, construction, copy, destruction, and cleanup semantics |
| MIG-002 complete | Canonical managed public/import identities, ownership signatures, exception consumption, and namespace-independent native identities |
| Exception projection complete | Accepted managed/native error transport and cleanup contract |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-01; supply the canonical manifest, fingerprint, bootstrap, generated native project,
  and operation identities for AC-05 and AC-06.
- Complete parse and normalization before emission. C# and C++ emitters consume only the same Model
  and never parse headers, generated text, or each other's output.
- Consume the Model's ownership classification and operation lifetime flow exactly; do not infer a
  category or call shape from generated managed source, ABI-v1, or handwritten fixtures.
- Emit exactly one complete cross-language chain for a supported operation. If any transport,
  ownership, conversion, error, cleanup, or implementation fact is unavailable, emit an unsupported
  disposition rather than a partial layer.
- Keep the public export boundary C11-compatible: no C++ class, reference, template, STL, exception,
  overload, or compiler-specific calling convention leaks through it.
- Include every layout, registered closed generic, operation, signature, ownership, construction,
  error, cleanup, compiler, and module identity in one deterministic exact-match contract.
- Preserve source-only completion: all managed/native sources, manifests, and build descriptions are
  complete before optional compilation begins.
- Treat inactive ABI-v1 files only as migration recovery material. They cannot be a generator input,
  oracle, fallback, compatibility target, build dependency, or proof fixture for this item.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-01 through GEN-04 and the active generated binding architecture.
- The native basename remains `ted_toolkit_occt`; the managed namespace does not affect native
  identity.
- Escalate a second declaration/operation catalog, handwritten final symbol, independently
  versioned ABI, non-C11 export, incomplete cleanup path, emitter-side classification, ABI-v1
  dependency, or declaration-specific Runtime addition.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=component -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Controlled and real declarations produce one deterministic C/C++/C# identity chain, manifest, export set, and source-complete native project only from the Model, with strict C compatibility and no ABI-v1 or handwritten authority | Run the Generator Release native-emission/manifest TUnit suite, compile the generated strict C headers, and inspect deterministic clean-run inventories |

<!-- work-item: definition-of-done -->
## Done

AC-01 passes; MIG-004 receives the fixed bootstrap and complete layout-, classification-,
operation-flow-, and lifetime-complete exact-match contract; MIG-005 receives one source-complete
native/managed project; no ABI-v1 input, second authority, partial operation, handwritten final
binding, or native compilation is required for source generation.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, generated artifacts and inventories, owned contract IDs, commands,
strict-C assertions, classification and operation-flow projections, manifest mutation categories,
absence of ABI-v1 inputs, determinism results, discovered/passed/failed/skipped counts, and exact
outputs supplied to MIG-004 and MIG-005.
