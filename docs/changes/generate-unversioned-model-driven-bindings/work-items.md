<!-- delivery-map -->
## Delivery map

- Parent change: Revised Draft governed by GEN-01 through GEN-04 and the active generated binding
  architecture.
- Planning status: Draft. This revised five-item migration map requires explicit approval before
  implementation.

This file is the only mutable work-item status source. Prerequisites name verified inputs rather
than preferred scheduling.

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Primary proof | Status | Document |
| --- | --- | --- | --- | --- | --- | --- |
| MIG-001 | One pinned native build identity and one RecordModel-centered semantic/physical model produce exact sequential managed storage projections and proved generic layout graphs | Owns AC-02 / Owns AC-03 / Supports AC-01 / Supports AC-04 through AC-06 | Approved revised parent/map; machine-readable pinned OCCT/compiler/build identity | Component plus native/managed Contract proof for model completeness, every emitted layout, generic closure, determinism, and fail-closed eligibility | Draft | `work-items/MIG-001-compile-exact-physical-model.md` |
| MIG-002 | Generated managed source exposes exact structs, inheritance interfaces, validated namespaces, `in`/`ref` value extensions, and separate Handle/Owned ownership APIs | Owns AC-04 / Supports AC-01 / Supports AC-05 / Supports AC-06 | MIG-001 model and layout projections; completed approved `net8.0` Handle and Owned Runtime public integration contracts | Public API Contract plus controlled Component proof for namespace, receiver, interface, factory, ownership, independent wrapper use, and prohibited-surface rules | Draft | `work-items/MIG-002-emit-managed-binding-api.md` |
| MIG-003 | The same completed Model emits the C11 declarations, C++ adapters, managed imports, manifest, fingerprint input, and native build description for every supported operation | Owns AC-01 / Supports AC-05 / Supports AC-06 | MIG-001 canonical model/layout identities; MIG-002 managed public/import identities; completed exception projection | Cross-layer Component/Contract proof for identity completeness, source-only generation, C11 syntax, deterministic artifacts, and no second catalog | Draft | `work-items/MIG-003-emit-native-binding-contract.md` |
| MIG-004 | Generated initialization accepts only a native artifact with the exact layout- and lifetime-complete fingerprint while Runtime remains declaration-agnostic | Owns AC-05 / Supports AC-06 | MIG-002 generated managed boundary; MIG-003 canonical manifest, fingerprint, bootstrap, imports, and operation identities | Contract/Integration proof with matching, differing, and missing-bootstrap fixtures plus export-resolution instrumentation | Draft | `work-items/MIG-004-enforce-exact-native-match.md` |
| MIG-005 | The optional native-build stage compiles the generated unversioned project, replaces ABI-v1, and passes strict C11 and managed real-OCCT proof | Owns AC-06 / Supports AC-01 through AC-05 | MIG-001 through MIG-004 complete; pinned native build identity and dependencies available | Native/managed Integration proof plus structural replacement inspection | Draft | `work-items/MIG-005-replace-and-prove-native-boundary.md` |

## Plan constraints

- MIG-001 is the only declaration and physical-layout authority. Later items consume its identities
  and dispositions rather than rebuilding or filtering the model.
- MIG-002 owns managed public compatibility, including namespace validation and owner/receiver
  signatures. MIG-003 cannot change those signatures while completing the cross-language chain.
- MIG-003 is the only source of the canonical manifest, fingerprint domain, native symbols, C11
  declarations, C++ adapters, and generated build description. MIG-004 only consumes the resulting
  fixed bootstrap and expected digest.
- C# and C++ source generation completes without MIG-005. MIG-005 consumes completed emitted source
  and never reparses headers or changes Model semantics during compilation.
- Every emitted object and registered closed generic receives exhaustive layout proof before the
  migration can complete. Representative real calls supplement that exhaustive contract proof;
  they do not replace it.
- ABI-v1 proof remains available only until MIG-005 supplies a passing replacement. MIG-005 then
  removes active legacy source, build, fixtures, generated output, and current documentation.
- The Draft `TedToolkit.Occt.Windows` package change consumes the verified MIG-005 candidate and
  cannot redefine Generator or Runtime contracts.
