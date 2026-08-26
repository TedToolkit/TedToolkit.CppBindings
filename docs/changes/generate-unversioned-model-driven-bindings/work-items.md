<!-- delivery-map -->
## Delivery map

The approved Controlled migration requires five independently verifiable deliveries. This file is
the only mutable work-item status source. Prerequisites name supplied outcomes rather than preferred
scheduling.

<!-- approval-source: Explicit maintainer approval in the Codex task on 2026-08-26. -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| MIG-001 | One pinned build identity and one `RecordModel`-centered semantic/physical model provide exact layouts, generic graphs, ownership categories, and operation lifetime flows | Owns AC-02 / Owns AC-03 / Owns AC-08 / Supports AC-01 / Supports AC-04 / Supports AC-05 / Supports AC-06 | Approved parent contract; complete pinned OCCT/compiler/build identity | Approved | `work-items/MIG-001-compile-exact-physical-model.md` |
| MIG-002 | Generated managed source exposes the exact public API, deterministic function-table slots, exact typed dispatch, and Handle/Owned integration exclusively from the completed Model | Owns AC-04 / Owns AC-07 / Supports AC-01 / Supports AC-05 / Supports AC-06 | MIG-001: verified Model, layouts, classifications, operation flows, and dispositions; verified Runtime owner and exception baselines | Approved | `work-items/MIG-002-emit-managed-binding-api.md` |
| MIG-003 | The completed Model emits one canonical C11/C++ boundary, manifest, fingerprint domain, required export set, and native build description without ABI-v1 input | Owns AC-01 / Supports AC-05 / Supports AC-06 | MIG-001: canonical model and physical identities; MIG-002: canonical managed operation, import, and slot identities; verified exception projection | Approved | `work-items/MIG-003-emit-native-binding-contract.md` |
| MIG-004 | Generated initialization verifies exact match, then validates and publishes one complete process-lifetime static managed function table | Owns AC-05 / Supports AC-06 | MIG-002: generated slots and typed calls; MIG-003: fingerprint bootstrap, expected digest, and required export set | Approved | `work-items/MIG-004-enforce-exact-native-match.md` |
| MIG-005 | The emitted project compiles and passes the real pinned OCCT boundary while every active ABI-v1 path is removed | Owns AC-06 / Supports AC-01 / Supports AC-02 / Supports AC-03 / Supports AC-04 / Supports AC-05 / Supports AC-08 | MIG-001: verified Model and layouts; MIG-002: generated managed API; MIG-003: source-complete native contract; MIG-004: verified initialization and function table; pinned native dependencies | Approved | `work-items/MIG-005-replace-and-prove-native-boundary.md` |

## Plan constraints

- MIG-001 is the only declaration, physical-layout, ownership-classification, and operation-flow
  authority. Later items consume its identities without rebuilding or reclassifying them.
- MIG-002 owns managed public compatibility, deterministic function-table slots, exact typed call
  sites, and cleanup-pointer handoff to Runtime owners. It does not own loading or publication.
- MIG-003 owns native symbols, the canonical manifest and fingerprint domain, the fixed bootstrap,
  required export set, and generated build description. It does not publish the managed table.
- MIG-004 owns the generated Win32 loader, exact-match ordering, complete table validation, atomic
  publication, and process-lifetime module/table state. Runtime remains declaration-agnostic.
- MIG-005 compiles and integrates completed outputs. It cannot change Model, API, ABI, loader,
  owner, or table contracts while removing the active legacy boundary.
- ABI-v1 may remain only as inactive recovery material until MIG-005 proves the replacement. No item
  may consume it as input, oracle, fallback, compatibility target, or proof fixture.
- The `TedToolkit.Occt.Windows` package change consumes the verified MIG-005 result and cannot
  redefine Generator or Runtime contracts.
