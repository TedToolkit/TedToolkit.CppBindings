<!-- delivery-map -->
## Delivery map

This map is the single mutable status source for the Controlled CGAL delivery. CGAL-001 and
CGAL-002 may proceed independently; CGAL-003 starts only from both verified contracts.

<!-- approval-source: The maintainer explicitly approved this exact three-item delivery map with “批准。” in the Codex task on 2026-09-07. -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| CGAL-001 | Produce the deterministic finite EPICK Generator and complete profile inventories | Owns AC-01 / Supports AC-03 | None; the parent change already supplies the completed Shared semantic engine and enforced provider topology | Verified | `work-items/CGAL-001-generate-deterministic-epick-profile.md` |
| CGAL-002 | Deliver CGAL Runtime failures and finite polymorphic-result semantics with compiled native-boundary proof | Owns AC-02 / Supports AC-04 | None; the parent change already supplies the provider-neutral Shared Runtime and native error boundary | Verified | `work-items/CGAL-002-deliver-runtime-semantics.md` |
| CGAL-003 | Pack and consume the complete admitted `win-x64` EPICK surface | Owns AC-03 / Owns AC-04 | `CGAL-001`: verified profile manifest, admitted/unsupported inventories, paired sources, and native project metadata / `CGAL-002`: verified Runtime API, exception mapping, result ABI, and cleanup contract | Approved | `work-items/CGAL-003-pack-and-consume-windows.md` |
