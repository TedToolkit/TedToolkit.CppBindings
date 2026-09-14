<!-- delivery-map -->
## Delivery map

This file is the only mutable work-item status source.

<!-- approval-source: maintainer approved separating and continuing the unified NativeError delivery in the Codex task on 2026-09-09 with "统一，并继续。把这个玩意儿统一了，给我。" -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| UPG-005 | Make Shared the single common native-error generation and Runtime projection authority while Providers retain only local extensions | Owns AC-05 / Supports AC-04 | None; supplies the verified common error boundary to every provider migration | Verified | `work-items/UPG-005-native-error-unification.md` |
| UPG-001 | Extend Shared with the reusable non-error generation contract and FCL as the proving consumer | Owns AC-01 / Supports AC-04 | `UPG-005`: verified common native-error generation and projection boundary; supplies the remaining shared provider contract and buffer/composite-result primitives | Verified | `work-items/UPG-001-shared-fcl-foundation.md` |
| UPG-002 | Migrate Manifold from its private renderer to the verified shared generation contracts | Owns AC-02 / Supports AC-04 | `UPG-001`: verified shared provider, buffer, and composite-result contracts; `UPG-005`: verified common error boundary | Verified | `work-items/UPG-002-manifold-migration.md` |
| UPG-003 | Remove remaining non-error generic emission copies from CGAL and OCCT | Owns AC-03 / Supports AC-04 | `UPG-001`: verified shared bootstrap, result, and native-project extension points; `UPG-005`: verified common error boundary | Verified | `work-items/UPG-003-existing-provider-cleanup.md` |
| UPG-004 | Enforce and document the final one-model generation and native-error boundary across all four providers | Owns AC-04 | `UPG-005`, `UPG-001`, `UPG-002`, and `UPG-003`: error unification and all provider migrations verified on the integration baseline | Verified | `work-items/UPG-004-cross-provider-enforcement.md` |
