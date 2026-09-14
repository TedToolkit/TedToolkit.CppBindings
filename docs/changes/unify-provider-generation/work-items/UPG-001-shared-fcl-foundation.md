# UPG-001: Establish the shared provider contract through FCL

<!-- work-item-format: 2 -->
<!-- work-item-id: UPG-001 -->

<!-- approval-source: maintainer approved the revised delivery map and explicitly continued in the Codex task on 2026-09-09 -->

## Outcome

Shared gains the provider-neutral transport, projected-result, bootstrap, and plan capabilities
required by FCL. The locked FCL Generator becomes their first complete proving consumer without
retaining a parallel generation pipeline and consumes the native-error boundary supplied by UPG-005.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public contract: Shared semantic/generation contracts plus the
  unreleased FCL Generator APIs.
- In scope: only reusable buffer and composite/owned-result semantics evidenced by current providers;
  standard function-table, NativeApi, native-error, native-project, plan, inventory, and output helpers
  where provider-neutral; FCL semantic/profile inputs; and removal of FCL's private plan, writer, and
  complete managed/native renderer.
- Non-goals: Manifold migration, CGAL/OCCT cleanup, a provider-name branch in Shared, or a changed FCL
  generated consumer surface.
- Likely touchpoints (non-binding): Shared semantic models and emitters, FCL Generator sources and tests,
  and provider-boundary verification.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| UPG-005 | Shared common native-error generation and Runtime projection are verified | UPG-005 is Verified on the selected integration baseline |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-01 | Owns | Produces the common contract and complete FCL migration |
| AC-04 | Supports | Supplies one behavior-preserving migrated provider and the reusable semantic inputs required downstream |

<!-- work-item: delivery-constraints -->
## Constraints

- FCL public generated operation types, validation, status values, owned lifetime, collision results,
  exports, inventories, native basename, locked toolchain profile, and verified Shared native-error
  consumption remain unchanged.
- Removed unreleased FCL Generator-only APIs receive no compatibility wrappers.
- Private schema, type names, source grouping, and concrete emitter decomposition remain open.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | FCL creates two identical shared plans, derives paired sources/slots from one model, and contains no private plan/output/bootstrap engine | `dotnet run --project tests/TedToolkit.CppBindings.Fcl.Generator.Tests/TedToolkit.CppBindings.Fcl.Generator.Tests.csproj -c Release -- --report-trx` |
| FCL boundary | Conditional | The real Windows package retains native calls, ownership, status, common exception semantics, layout, dependency, and notice behavior | `pwsh -NoProfile -File Build/VerifyFclWindowsPackage.ps1` |
| Shared regression | Conditional | Existing shared semantic contracts remain green | `dotnet run --project tests/TedToolkit.CppBindings.Generator.Tests/TedToolkit.CppBindings.Generator.Tests.csproj -c Release -- --report-trx` |

<!-- work-item: definition-of-done -->
## Done

- AC-01 and its conditional FCL/shared proof pass.
- FCL uses the shared provider contract, duplicate generation infrastructure is removed, and the
  verified Shared capabilities are available to dependent items without provider-specific branches.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact candidate revision, changed artifacts, AC-01 proof purpose and shape, commands,
observed test counts/results, Windows toolchain prerequisites, compatibility limits, and the verified
Shared inputs supplied to UPG-002 and UPG-003.

## Risks and implementation notes

Bulk pointer/length correlation, conditional owned-result construction, and slot order must be
represented explicitly in the semantic model rather than embedded as FCL source templates.
