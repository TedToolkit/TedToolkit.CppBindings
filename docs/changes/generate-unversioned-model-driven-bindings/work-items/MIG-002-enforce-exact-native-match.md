# MIG-002: Enforce exact managed/native contract matching

<!-- work-item-format: 2 -->

- Approval: User approval in the current Codex task on 2026-08-25 for Draft content SHA-256
  `2B5921EC1E275E469B39D8304DF7A1F7C136BBE39A7744AB38DE52AD94A865F6`.

## Outcome

Generated managed initialization resolves and calls only the immutable fingerprint bootstrap until
the native SHA-256 digest exactly matches the generated managed digest; only then may generated
operation exports resolve. Missing or different contracts fail deterministically as
`BadImageFormatException`.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: Runtime/native-library initialization,
  bootstrap invocation, digest comparison, generated operation export resolution, and mismatch
  diagnostics.
- In scope: one-time/thread-safe initialization; fixed bootstrap resolution and invocation;
  managed expected-digest consumption; exact comparison; missing/different diagnostics;
  instrumentation seams and native fixtures proving pre-match resolution behavior.
- Non-goals: changing the canonical manifest or fingerprint domain; defining operation identities
  or imports; compiling the real OCCT adapter; changing public Handle or exception semantics;
  package/RID asset resolution.
- Likely touchpoints (non-binding): Runtime loading boundary, generated managed invocation glue,
  Runtime and Generator tests, and small native loader fixtures.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| MIG-001 complete | Canonical manifest encoding and SHA-256 digest; fixed bootstrap source/signature; generated managed expected digest; complete operation identities/import metadata | MIG-001 completion evidence and passing AC-01/AC-06 component proof |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-03 | Owns | Prove matching artifacts initialize, each fingerprint-domain mutation changes the digest, and missing/different artifacts fail before any operation export resolution. |
| AC-05 | Supports | Supply the verified initialization and resolution gate used by MIG-003's generated managed consumer. |

<!-- work-item: delivery-constraints -->
## Constraints

- Pre-match native knowledge is limited to exactly
  `ted_toolkit_occt_contract_fingerprint`, cdecl, and
  `void(uint8_t* out_fingerprint)` with one non-null 32-byte caller-owned buffer.
- Missing bootstrap and digest mismatch throw `BadImageFormatException`; neither may surface later
  as an operation `EntryPointNotFoundException`. No generated OCCT operation export is resolved or
  invoked before equality is established.
- Initialization must not create a second operation catalog or manifest parser; it consumes
  MIG-001 generated constants/metadata.
- Loader lifetime, synchronization mechanism, generated source organization, and diagnostic prose
  remain private choices unless they change the observable exception contract.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-03 digest domain | Acceptance and regression | Contract | Mutating one field at a time across identifiers, calling convention, ordered signatures, direction/nullability/ownership, layouts/constants, error, lifetime, and cleanup changes the 32-byte SHA-256 digest while the bootstrap contract remains byte-for-byte fixed. | Run the Generator TUnit project's canonical-manifest mutation matrix with `dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx`. |
| AC-03 load boundary | Acceptance and boundary | Integration | A matching fixture initializes and resolves a representative operation; different and missing-bootstrap fixtures throw `BadImageFormatException` after resolving at most the bootstrap export and never invoke an operation. | Build the bounded native fixtures, then run `dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release -- --report-trx` with export-resolution/invocation instrumentation. |

<!-- work-item: definition-of-done -->
## Done

- AC-03 has passing digest-domain and loader-boundary proof for matching, differing, and missing
  bootstrap cases.
- MIG-003 receives a generated managed boundary that cannot resolve or invoke an OCCT operation
  until exact native contract equality is established.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

The implementation handoff records candidate revision, changed artifacts, AC-03 evidence purposes
and shapes, exact fixture contracts, mutation categories, commands, assertions, results/counts,
platform prerequisites, and the verified loader output supplied to MIG-003. Mutable status remains
in `work-items.md`.

## Risks and implementation notes

- A bootstrap signature derived from the manifest would recreate the unsafe pre-match call this
  item exists to prevent; the fixed ADR contract is not customizable generation policy.
- Resolution instrumentation must observe the real loader seam rather than infer call order from
  later exceptions.
