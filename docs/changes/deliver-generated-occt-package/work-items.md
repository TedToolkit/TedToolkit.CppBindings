<!-- delivery-map -->
## Delivery map

- Parent change: Revised Windows package Draft governed by the repository principles and active
  generated binding architecture.
- Planning status: Draft. The prior map approval is suspended because the public package and managed
  assembly identity changed to `TedToolkit.Occt.Windows`. Implementation is not authorized until
  the revised parent and map are explicitly approved.

This file is the only mutable work-item status source. Independent rows may run only when their
verified prerequisites are available.

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Primary proof | Status | Document |
| --- | --- | --- | --- | --- | --- | --- |
| PKG-001 | Every pinned OCCT public header and declaration has one order-independent generated, unsupported, or excluded disposition | Owns AC-01 / Supports AC-05 | Approved revised parent; complete machine-readable native build identity and pinned OCCT header universe | Component coverage proof over real headers plus fail-closed fixtures | Draft | `work-items/PKG-001-inventory-complete-occt-headers.md` |
| PKG-002 | The completed Generator and Runtime outputs materialize one full `TedToolkit.Occt.Windows` managed/native candidate whose shipped declarations, layouts, APIs, ownership, and exact-match contract agree | Owns AC-02 / Owns AC-03 / Supports AC-04 / Supports AC-05 | Completed `net8.0` Runtime-defined Handle and Owned public contracts; completed generator migration; PKG-001 inventory | Exhaustive layout/API Contract validation plus representative real Integration proof, including ordinary public Runtime use and absence of friend access, without modifying Generator or Runtime contracts | Draft | `work-items/PKG-002-deliver-callable-generated-bindings.md` |
| PKG-003 | A self-contained truthful `TedToolkit.Occt.Windows` package for `win-x64` passes isolated consumption and closure proof | Owns AC-04 / Owns AC-05 | PKG-001 coverage report; PKG-002 Windows assembly, native candidate, manifest, API baseline, and boundary evidence | End-to-end `net8.0` consumer plus package/assembly/native/license Contract inspection and unsupported-RID proof | Draft | `work-items/PKG-003-package-and-prove-windows-consumer.md` |

## Plan constraints

- PKG-001 supplies the authoritative declaration universe and dispositions; PKG-002 does not create
  another coverage model.
- PKG-002 consumes the completed Generator and Runtime contracts to materialize the full release
  candidate; it does not own emitter, loader, owner, or exception behavior. A defect that changes
  those contracts returns to the owning change. PKG-003 may add only packaging, RID resolution,
  support diagnostics, and consumer documentation.
- PKG-002 names the public assembly `TedToolkit.Occt.Windows`, uses `TedToolkit.Occt` as its C#
  namespace default, and keeps all declaration-specific imports, identities, layouts,
  specializations, and fingerprint data in generated output. Runtime owns only the necessary shared
  declaration-agnostic mechanisms. Windows references the single Runtime owner definitions through
  ordinary public API and receives no `InternalsVisibleTo` or assembly-name privilege.
- Every PKG-002 shipped object and registered closed generic receives exhaustive native/managed
  layout proof. Representative real operations prove behavior and lifecycle categories only.
- PKG-003 alone owns package contents, native dependency closure, license notices, and public
  support claims. Remote publication remains outside the parent change.
