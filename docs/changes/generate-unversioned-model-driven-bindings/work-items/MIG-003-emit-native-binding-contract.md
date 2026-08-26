# MIG-003: Emit the complete native binding contract

<!-- work-item-format: 2 -->

- Approval: None while Draft. Start requires the verified MIG-001 and MIG-002 outputs.

## Outcome

The completed Model emits every supported operation's C11 declaration, C++ adapter, managed import,
manifest row, fingerprint input, and native build entry with one canonical identity. Source-only
generation finishes deterministically without invoking a native compiler or using a handwritten
operation catalog.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: C-compatible pointer transports; deterministic export naming; one adapter source per
  canonical owning type plus type-independent support source; placement construction, destruction,
  intrusive reference operations, pointer adjustment, exception containment, error cleanup;
  canonical manifest/fingerprint encoding; fixed bootstrap; generated source inventory and CMake;
  independent Model-only C/C++ emission.
- Non-goals: changing MIG-001 layouts, changing MIG-002 public API, Runtime loading, real native
  compilation/linking, package coverage expansion, package creation, or handwritten final bindings.
- Likely touchpoints (non-binding): Generator C/C++ emitters, transport and operation mappings,
  manifest/fingerprint writers, build-description emission, source inventory, diagnostics, strict
  C syntax fixtures, and Generator tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| MIG-001 complete | Canonical semantic/physical Model, build identity, layouts, generic registrations, ownership categories, and dispositions |
| MIG-002 complete | Canonical managed public/import identities, ownership signatures, exception consumption, and namespace-independent native identities |
| Exception projection complete | Accepted managed/native error transport and cleanup contract |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-01; supply the canonical manifest, fingerprint, bootstrap, generated native project,
  and operation identities for AC-05 and AC-06.
- Complete parse and normalization before emission. C# and C++ emitters consume only the same Model
  and never parse headers, generated text, or each other's output.
- Emit exactly one complete cross-language chain for a supported operation. If any transport,
  ownership, conversion, error, cleanup, or implementation fact is unavailable, emit an unsupported
  disposition rather than a partial layer.
- Keep the public export boundary C11-compatible: no C++ class, reference, template, STL, exception,
  overload, or compiler-specific calling convention leaks through it.
- Include every layout, registered closed generic, operation, signature, ownership, construction,
  error, cleanup, compiler, and module identity in one deterministic exact-match contract.
- Preserve source-only completion: all managed/native sources, manifests, and build descriptions are
  complete before optional compilation begins.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-01 through GEN-04 and the active generated binding architecture.
- The native basename remains `ted_toolkit_occt`; the managed namespace does not affect native
  identity.
- Escalate a second declaration/operation catalog, handwritten final symbol, independently
  versioned ABI, non-C11 export, incomplete cleanup path, or declaration-specific Runtime addition.

<!-- work-item: proof-plan -->
## Proof

| Parent contract | Evidence purpose and shape | Observable proof |
| --- | --- | --- |
| AC-01 | Acceptance/structural Component plus Contract | Controlled and real declarations produce one complete deterministic C/C++/C# identity chain; both emitters consume only the completed Model; strict C declarations compile; no handwritten catalog or partial operation exists |
| Fingerprint input | Boundary/regression Contract | Mutating each layout, generic, operation, ownership, error, cleanup, compiler, or module identity changes canonical manifest bytes and digest input |
| Source-only path | Structural regression | Generation materializes complete C#, C, C++, manifest, and build outputs without starting the compiler; identical inputs reproduce byte-identical native artifacts |

Run the Generator Release build and TUnit native-emission/manifest suite, compile strict C headers
against controlled output, and inspect deterministic clean-run inventories.

<!-- work-item: definition-of-done -->
## Done

AC-01 passes; MIG-004 receives the fixed bootstrap and complete canonical exact-match contract;
MIG-005 receives one source-complete native/managed project; no second authority, partial operation,
handwritten final binding, or native compilation is required for source generation.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, generated artifacts and inventories, owned contract IDs, commands,
strict-C assertions, manifest mutation categories, determinism results, discovered/passed/failed/
skipped counts, and exact outputs supplied to MIG-004 and MIG-005.
