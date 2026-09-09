<!-- delivery-map -->
## Delivery map

This file is the only mutable work-item status source.

<!-- approval-source: maintainer approved the revised UPG-001 through UPG-004 delivery map and explicitly continued in the Codex task on 2026-09-09 with "没错，就是这样。我同意，然后开始修改！" -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| UPG-001 | Extend Shared with the reusable generation contract, common native-error protocol/projection, and FCL as the proving consumer | Owns AC-01 / Supports AC-04 / Supports AC-05 | None; supplies the verified shared provider contract, common error boundary, and buffer/composite-result primitives | Approved | `work-items/UPG-001-shared-fcl-foundation.md` |
| UPG-002 | Migrate Manifold from its private renderer and common Runtime projection to the verified shared contracts | Owns AC-02 / Supports AC-04 / Supports AC-05 | `UPG-001`: verified shared provider, native-error, buffer, and composite-result contracts | Approved | `work-items/UPG-002-manifold-migration.md` |
| UPG-003 | Remove remaining generic emission/projection copies from CGAL and OCCT and correct their error-category drift | Owns AC-03 / Owns AC-05 / Supports AC-04 | `UPG-001`: verified shared bootstrap, error, result, Runtime projection, and native-project extension points | Approved | `work-items/UPG-003-existing-provider-cleanup.md` |
| UPG-004 | Enforce and document the final one-model generation and native-error boundary across all four providers | Owns AC-04 | `UPG-001`, `UPG-002`, and `UPG-003`: all provider migrations verified on the integration baseline | Approved | `work-items/UPG-004-cross-provider-enforcement.md` |
