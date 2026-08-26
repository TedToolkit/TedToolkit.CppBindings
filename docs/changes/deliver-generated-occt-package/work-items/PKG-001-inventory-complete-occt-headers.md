# PKG-001: Inventory every pinned OCCT public header safely

<!-- work-item-format: 2 -->

- Approval: None while Draft. The prior map approval is suspended by the revised
  `TedToolkit.Occt.Windows` parent contract.

## Outcome

Release generation produces one deterministic inventory in which every in-boundary OCCT header and
canonical declaration has exactly one generated, unsupported, or excluded disposition, including
the precise layout, generic, ownership, or operation reason for every unsupported declaration.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: pinned header discovery, canonical declaration identity and deduplication, layout and
  ownership eligibility inputs, complete dispositions, deterministic machine-readable and
  human-reviewable coverage, and mixed supported/unsupported behavior.
- Non-goals: generated public/native implementations, approving a new layout or ownership category,
  package composition, or claiming a coverage percentage beyond complete disposition.
- Likely touchpoints (non-binding): vcpkg environment and generation options, parser and record
  discovery, eligibility diagnostics, coverage models/writers, and Generator tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| Approved parent | Revised package scope, pinned matrix, and coverage boundary |
| Pinned OCCT input | Machine-readable build identity resolving OCCT 8.0.1 and recording vcpkg baseline, `x64-windows`, MSVC toolset, CRT, packing, and layout-affecting options |
| Accepted generation architecture | GEN-01 through GEN-04 and the active generated binding architecture |

<!-- work-item: contract-coverage -->
## Contract responsibility

- Own parent AC-01 and supply the coverage and limitation input for AC-05.
- Every selected canonical declaration appears once regardless of header order, AST order,
  redeclarations, aliases, or dependency discovery.
- A generated disposition requires complete physical layout, generic specialization, ownership,
  construction/destruction, and callable-operation eligibility under the accepted architecture.
- Unsupported declarations name the missing rule and do not recursively turn STL, compiler
  internals, iterators, or arbitrary templates into supported wrapper targets.
- The inventory consumes the canonical declaration model; it does not become a second operation or
  layout authority.

<!-- work-item: delivery-constraints -->
## Constraints

- Governed by GEN-01 through GEN-04 and the active generated binding architecture. This item cannot
  approve or weaken a layout, generic, ownership, Runtime, or public API category.
- Private fixture organization and coverage serialization remain implementation choices when they
  preserve deterministic identities and the supplied PKG-002 input.

<!-- work-item: proof-plan -->
## Proof

Component proof over the pinned real headers demonstrates complete one-to-one dispositions,
order-independent output, and stable diagnostics. Controlled fixtures prove duplicates coalesce,
unsupported siblings do not suppress supported declarations, and missing layout/ownership rules
fail closed.

<!-- work-item: definition-of-done -->
## Done

PKG-002 receives the authoritative eligible declaration set and PKG-003 receives truthful
coverage/support input; parent AC-01 passes; no callable or package artifact is implemented here.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record candidate revision, inventory artifacts, AC-01 purpose and execution shape, commands,
declaration/header counts by disposition, determinism and fail-closed assertions, pinned OCCT
inputs, documentation state, and the exact identities supplied to PKG-002 and PKG-003.
