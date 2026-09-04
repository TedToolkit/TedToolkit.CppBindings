<!-- delivery-map -->
## Delivery map

This is the only mutable item-status source for the approved platform migration.
The three outcomes run in dependency order; intermediate states are internal integration states,
not supported packages or compatibility promises. Each item includes its smallest proving consumers.

<!-- approval-source: Maintainer explicitly approved the complete CPB-001, CPB-002, and CPB-003 map and item set with "批准。" in this task on 2026-09-04. -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| CPB-001 | Consumable generic and OCCT Runtime contracts with standalone diagnostics | Owns AC-03 / Owns AC-04 / Supports AC-02 / Supports AC-05 | None | In progress | `work-items/CPB-001-runtime-and-diagnostics.md` |
| CPB-002 | Reusable generation platform with a functioning OCCT provider and renamed solution | Owns AC-02 / Owns AC-05 | CPB-001: verified Runtime public surfaces, lifetime behavior, and direct analyzer-package consumption | Approved | `work-items/CPB-002-generation-platform.md` |
| CPB-003 | Consistent authoritative repository identity and durable maintainer guidance | Owns AC-01 | CPB-002: verified integrated project graph, public identities, renamed solution, and real OCCT binding gates; CPB-001: verified package identities and consumption instructions | Approved | `work-items/CPB-003-repository-identity.md` |

## Integration constraints

- Runtime contracts feed generator emission, so CPB-002 starts from CPB-001's verified integration
  revision. CPB-003 consumes the resulting complete project graph and proof, not proposed paths.
- Shared build files, solution membership, generated templates, and current architecture documents
  are collision areas. Use serial integration; private edit order does not change these boundaries.
- Preserve the existing uncommitted template-projection work and keep the separate benchmark
  experiment isolated. Old verification does not cover either new template edits or this migration.
- The GitHub rename is the parent change's maintainer/coordinator operational handoff, not a fourth
  development item. CPB-003 requires its post-rename evidence before completion; an internally
  reviewed candidate alone cannot satisfy AC-01. Do not publish packages.
- `Implemented` requires item proof and review; `Verified` additionally requires the authoritative
  integration revision. Only verified prerequisite evidence unlocks dependent items.
