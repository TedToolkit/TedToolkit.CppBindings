# MIG-004: Enforce exact managed/native contract matching

<!-- work-item-format: 2 -->
<!-- work-item-id: MIG-004 -->

<!-- approval-source: Explicit maintainer approval of the complete MIG-001 through MIG-005 map in the Codex task on 2026-08-26. -->

## Outcome

Generated managed initialization resolves and calls only the immutable fingerprint bootstrap until
the native SHA-256 digest exactly matches the generated managed digest, including every ownership
classification and operation lifetime flow. Only then does it resolve every required operation and
cleanup export into a private managed `IntPtr[]`, validate completeness, and publish the table once.
Missing or different contracts and missing required exports fail deterministically as
`BadImageFormatException` before publication or operation invocation.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: generated Win32 loader imports and initialization mechanisms; generated expected
  contract data; native bootstrap invocation; digest comparison; generated operation export
  resolution; static managed table construction and atomic publication; classification- and
  lifetime-flow-complete digest inputs; one-time/thread-safe initialization; mismatch diagnostics;
  instrumentation seams and native fixtures proving pre-match and partial-failure behavior.
- Non-goals: defining the canonical manifest/fingerprint domain; defining operation, layout,
  closed-generic, or ownership identities; compiling the real OCCT adapter; changing owner or
  exception semantics; adding module loading, generated-set identities, imports, expected digests,
  function-table storage, or Windows-specific behavior to Runtime; package/RID asset resolution.
- Likely touchpoints (non-binding): generated managed loader and invocation glue, Generator tests,
  and bounded native loader fixtures.

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
- After equality, every required export must resolve before the static table is published. A missing
  required export throws `BadImageFormatException`; no generated call can observe a null, stale, or
  partially initialized slot.
- A category change, ambiguous-category disposition, or operation borrowing, transfer,
  construction, copy, destruction, or cleanup change alters the canonical digest.
- No generated operation export resolves or invokes before equality. The generated wrapper owns the
  concrete Win32 loader boundary, expected constants, comparison, synchronization, module handle,
  and process-lifetime registration; Runtime does not become a loader, manifest parser, or
  generated-set registry.
- The generated wrapper owns the managed table and deterministic slots. Ordinary calls use exact
  typed unmanaged Cdecl pointers from the table. Owner factories copy cleanup pointers into
  `Handle<T>` and `Owned<T>`; Runtime owners never retain the table or an index.

<!-- work-item: delivery-constraints -->
## Constraints and escalation

- Governed by GEN-04 and the active generated binding architecture.
- Concrete loader imports, bootstrap binding, expected digest, operation imports, contract data,
  table storage, synchronization, and process-lifetime module state remain generated output.
  Runtime supplies only its existing declaration-agnostic owner and exception contracts through
  ordinary public API.
- Canonical managed and native digest inputs come only from MIG-003 Model-derived output. ABI-v1 and
  handwritten fixtures do not supply expected identities or matching behavior.
- A successfully matched generated native module remains loaded until process termination. Neither
  it nor its published table is replaced or released. Neither Runtime owners nor generated
  operations acquire per-owner leases or expose module unloading.
- Escalate any pre-match call whose signature depends on generated contract data, any need to put a
  generated identity in Runtime, or any ABI-v1 or handwritten expected-contract dependency.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-05 purpose=boundary shape=integration -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-05 | Primary | A matching fixture publishes one complete table and invokes exact typed slots; digest mismatch, missing bootstrap, and missing required export fail with `BadImageFormatException` before partial publication or operation invocation | Run the Generator manifest mutation contract suite and generated-wrapper loader integration fixtures with export-resolution and invocation instrumentation |

<!-- work-item: definition-of-done -->
## Done

AC-05 passes for matching, differing, missing-bootstrap, and missing-required-export artifacts;
MIG-005 receives a managed boundary that cannot resolve an operation before equality, cannot
publish a partial table, and cannot invoke an operation before complete table publication.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, fixture contracts, owned contract IDs, commands, mutation categories
including ownership-category and operation-flow mutations, resolution/invocation assertions,
absence of ABI-v1 expected identities, results/counts, platform prerequisites, and verified loader
output supplied to MIG-005.
