# CPB-001: Consumable Runtime contracts and standalone diagnostics

<!-- work-item-format: 2 -->
<!-- work-item-id: CPB-001 -->
<!-- approval-source: Maintainer explicitly approved the complete CPB-001 through CPB-003 map and item set with "批准。" in this task on 2026-09-04. -->

## Outcome

Consumers can use the approved generic and OCCT ownership contracts and explicitly opt into one
provider-neutral analyzer package, with independently proven public surfaces and lifetime behavior.

<!-- work-item: scope -->
## Scope and non-goals

- Deliver the parent identity table's generic Runtime, OCCT Runtime, and analyzer families, including
  `ICppOwner<T>`, minimal lowercase `handle<T>`, and `TTCB001` / `TTCB002`.
- Split package responsibilities, remove embedded analyzer delivery, and migrate the smallest managed
  and native wrapper consumers, contract tests, and package-local usage/suppression documentation.
- Likely touchpoints: current Runtime and Runtime.Analyzers projects, Runtime.Tests, analyzer tests,
  FirstWrapper, SecondWrapper, Runtime.NativeIntegration, and their build references.
- Non-goals: generator extraction or generated receiver changes, full-solution migration, remote
  rename, compatibility aliases, package publication, and benchmark strategy adoption.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | Approved parent contract and recoverable baseline; preserve unrelated dirty generator/template work | Parent start conditions and a recorded revision plus dirty-file inventory before edits |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | Complete migrated Runtime public surfaces and executable ownership/lifetime contracts |
| AC-04 | Owns | Packed standalone analyzer and isolated direct-consumer diagnostic matrix |
| AC-02 | Supports | Verified provider-neutral Runtime/analyzer dependency graph and public identities supplied to CPB-002 |
| AC-05 | Supports | Verified managed and native wrapper lifetime/cleanup behavior supplied to CPB-002 |

<!-- work-item: delivery-constraints -->
## Constraints

- Follow the parent identity disposition and AGENTS native semantics. Keep generic Runtime free of
  OCCT knowledge; retain OCCT intrusive ownership and exception projection only in OCCT Runtime.
- Lowercase `handle<T>` has only the prescribed private pointer and public non-owning `ref T Value`;
  it is not an owner interface implementation. Do not widen public APIs to share private cleanup.
- Analyzer assets are consumed directly with `PrivateAssets="all"`, with no analyzer runtime
  dependency or assumed transitive delivery. Derive rules from generic contracts/metadata.
- Intermediate integration is unpublished; deferred generator references do not constitute a
  full-solution pass. Private helper allocation, project/test organization, and file moves remain local choices.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-04 purpose=boundary shape=integration -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | Approved public identities, unmanaged value/storage layout, exactly-once cleanup, borrowing, finalization, copy semantics, and fail-closed behavior are preserved | Build the migrated Runtime test projects in Release, then `dotnet run --project <runtime-test-project> -c Release --no-build -- --report-trx`; inspect exported API contracts in both assemblies |
| AC-04 | Primary | Isolated generic and OCCT consumers receive the correct valid/invalid, borrowed/disposed, generated-only, and suppression results without an analyzer runtime reference | `dotnet pack <analyzer-project> -c Release -o <isolated-feed>`; restore/build isolated consumers using that packed asset and inspect diagnostics, nupkg assets, and consumer dependency metadata |
| Native ownership | Conditional | Cross-wrapper cleanup uses the matching native artifact after the assembly split | Build and run the existing handle-fixture CTest and migrated Runtime.NativeIntegration against the pinned Windows native dependencies |
| Core neutrality | Conditional | Runtime and analyzer dependency/source/metadata inventory has no provider-specific rule or dependency | Inspect evaluated project references, packed assets, and provider-name/classification occurrences; account for every allowed documentation/example string |

<!-- work-item: definition-of-done -->
## Done

Both owned cases and required boundary proofs pass; package-local documentation describes direct
consumption and caller lifetime responsibility. Supply verified Runtime surfaces, package identities,
diagnostic expectations, and native-wrapper evidence to CPB-002 without claiming a generated-binding pass.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact candidate/integration revision, changed artifacts, AC IDs, proof purpose/shape,
actual commands replacing placeholders, test counts/skips, diagnostics, package and log paths,
native resource prerequisites, documentation state, and inputs supplied to CPB-002. Report failures
explicitly; keep mutable progress/results outside this stable contract and status only in the map.
