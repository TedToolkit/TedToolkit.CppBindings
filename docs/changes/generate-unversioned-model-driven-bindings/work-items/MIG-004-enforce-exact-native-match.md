# MIG-004: Enforce exact managed/native contract matching

<!-- work-item-format: 2 -->

- Approval: None while Draft. Start requires MIG-002's managed boundary and MIG-003's verified
  exact-match inputs.

## Outcome

Generated managed initialization resolves and calls only the immutable fingerprint bootstrap until
the native SHA-256 digest exactly matches the generated managed digest; only then may generated
operation exports resolve. Missing or different contracts fail deterministically as
`BadImageFormatException`.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: declaration-agnostic Runtime loading and initialization mechanisms; generated expected
  contract data; native bootstrap invocation; digest comparison; generated operation export
  resolution; one-time/thread-safe initialization; mismatch diagnostics; instrumentation seams and
  native fixtures proving pre-match behavior.
- Non-goals: defining the canonical manifest/fingerprint domain; defining operation, layout,
  closed-generic, or ownership identities; compiling the real OCCT adapter; changing owner or
  exception semantics; storing generated-set identities, imports, or expected digests in Runtime;
  package/RID asset resolution.
- Likely touchpoints (non-binding): Runtime loading boundary, generated managed invocation glue,
  Runtime and Generator tests, and bounded native loader fixtures.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| MIG-002 complete | Generated managed operation boundary, public Runtime owner integration, and expected-contract consumption point |
| MIG-003 complete | Canonical manifest encoding and SHA-256 digest; fixed bootstrap source/signature; complete operation, layout, generic, ownership, error, cleanup, compiler, and import identities |

<!-- work-item: contract-coverage -->
## Contract responsibilities

- Own parent AC-05 and supply the verified initialization and resolution gate for AC-06.
- Before equality, native knowledge is limited to exactly
  `ted_toolkit_occt_contract_fingerprint`, cdecl, and one non-null 32-byte caller-owned output
  buffer.
- Missing bootstrap and digest mismatch throw `BadImageFormatException`; neither surfaces later as
  an operation `EntryPointNotFoundException`.
- No generated operation export resolves or invokes before equality. Generated wrapper code passes
  the expected constants through Runtime's public declaration-agnostic loading contract; Runtime
  does not become a manifest parser or generated-set registry.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-04 and the active generated binding architecture.
- Concrete bootstrap binding, expected digest, operation imports, and contract data remain generated
  output; Runtime owns only public declaration-agnostic loading, synchronization, comparison, and
  module-lifetime mechanisms usable by any wrapper assembly without friend access.
- Escalate any pre-match call whose signature depends on generated contract data or any need to put
  a generated identity in Runtime.

<!-- work-item: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Observable proof |
| --- | --- | --- | --- |
| AC-05 digest domain | Acceptance/regression | Contract | Mutating one field at a time across build/layout identity, closed generics, operations, signatures, ownership, errors, lifetime, and cleanup changes the 32-byte digest |
| AC-05 load boundary | Acceptance/boundary | Integration | A matching fixture initializes and resolves a representative operation; differing and missing-bootstrap fixtures throw `BadImageFormatException` after resolving at most the bootstrap and never invoke an operation |

Run the Generator canonical-manifest mutation matrix and Runtime loader fixtures with real
export-resolution and invocation instrumentation.

<!-- work-item: definition-of-done -->
## Done

AC-05 passes for matching, differing, and missing-bootstrap artifacts; MIG-005 receives a managed
boundary that cannot resolve or invoke an OCCT operation before exact contract equality.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, fixture contracts, owned contract IDs, mutation categories, commands,
resolution/invocation assertions, results/counts, platform prerequisites, and verified loader output
supplied to MIG-005.
