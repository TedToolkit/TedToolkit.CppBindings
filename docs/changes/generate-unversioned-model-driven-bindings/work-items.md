<!-- delivery-map -->
## Delivery map

- Parent change: `change.md` at `6237a3291aad4fe423b392c31126c9add14d2dc6`.
- Planning status: Approved by the user in the current Codex task on 2026-08-25 for Draft map
  SHA-256 `6950FD108EB0D7CB95600C4724BB395FA2A9AFB7D57688B525D4CF2719BF8C27`.

This file is the only mutable work-item status source. Prerequisites name concrete verified outputs,
not preferred scheduling.

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Primary proof | Status | Document |
| --- | --- | --- | --- | --- | --- | --- |
| MIG-001 | Configured parsed declarations produce one complete, deterministic, frozen catalog and every replacement binding artifact from that catalog | Owns AC-01 / Owns AC-04 / Owns AC-06 / Owns AC-08 / Supports AC-02 / Supports AC-03 / Supports AC-05 / Supports AC-07 | Approved parent at `6237a3291aad4fe423b392c31126c9add14d2dc6` | Acceptance and regression Component/Contract proof over controlled parsed fixtures and byte comparisons | Approved | `work-items/MIG-001-generate-complete-binding-set.md` |
| MIG-002 | Generated managed initialization accepts only the native artifact with the exact generated fingerprint | Owns AC-03 / Supports AC-05 | MIG-001: canonical manifest encoding, SHA-256 fingerprint, invariant native bootstrap, generated managed expected digest, and complete generated operation identities | Acceptance and boundary Contract/Integration proof with matching, differing, and missing-bootstrap native fixtures plus export-resolution instrumentation | Approved | `work-items/MIG-002-enforce-exact-native-match.md` |
| MIG-003 | The generated unversioned project replaces the ABI-v1 scaffold and passes real C11 and managed OCCT boundary proof | Owns AC-02 / Owns AC-05 / Owns AC-07 | MIG-001: complete deterministic generated source/build set, catalog, and fixture-propagation proof; MIG-002: verified exact-match initialization and operation-resolution gate | Acceptance and boundary Integration proof by building the generated native library and running strict C11 and generated managed consumers, plus structural replacement inspection | Approved | `work-items/MIG-003-replace-and-prove-native-boundary.md` |

## Plan constraints

- MIG-001 owns operation semantics and emitted artifact contracts. MIG-002 may add loading and
  resolution orchestration but cannot define a second manifest, fingerprint, import list, or
  operation identity.
- MIG-003 consumes the emitters and loader proved by MIG-001 and MIG-002. Integration-driven fixes
  may remain within their approved contracts; a changed generated contract returns to its owning
  item.
- The ABI-v1 proof remains available until MIG-003 has a passing replacement boundary, then MIG-003
  removes active versioned source, build, fixtures, generated output, and current documentation.
- After MIG-003 supplies reconciled Draft package records, explicit package-change/map reapproval is
  still required before this migration can be marked complete or package delivery can resume.
