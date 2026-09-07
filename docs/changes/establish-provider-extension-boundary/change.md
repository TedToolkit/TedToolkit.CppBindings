# Establish a reusable provider extension boundary

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: completed -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: The maintainer approved this exact contract and explicitly authorized continuation with “批准并继续。” in the Codex task on 2026-09-05. -->
<!-- candidate-binding: workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:c0e01fbaa4557d71db3815bbcdf59aeb1aefd96b1629666d9a5c8adaf43d2472 -->
<!-- candidate-digest: workspace-bundle-v1 hashes every Git tracked or untracked non-ignored file in ordinal path order and excludes this lifecycle record. SHA-256 input starts with UTF-8 "workspace-bundle-v1\0"; each entry appends the UInt32 little-endian UTF-8 path length, UTF-8 path, a one-byte presence marker, and, when present, the UInt64 little-endian content length plus raw content. Missing tracked files use only the zero presence marker. -->

<!-- section: goal-rationale -->
## Goal and rationale

Let a native-library provider build normalized C++ models and paired managed/native bindings through
one reusable `TedToolkit.CppBindings` semantic engine, while the repository layout makes shared and
provider ownership explicit and existing OCCT consumers observe no regression. This is required now
because the current OCCT Generator physically owns reusable model, Clang transport, layout,
template, and emitter behavior that a CGAL provider must reuse rather than copy.

<!-- section: scope -->
## Scope and non-goals

- In scope: establish the provider-neutral semantic extension contract; move shared projects below
  `src/shared` and OCCT projects below `src/providers/occt`; keep provider-neutral development tools
  below `src/tools`; move reusable semantic model, compiler transport, dependency closure, layout
  admission, template normalization, and paired emitters into Shared; adapt OCCT through explicit
  provider policies; update all solution, build, test, package, and documentation paths.
- Non-goals: implement CGAL, change an OCCT public API or generated capability, add a platform or
  RID, change the function-table ABI, publish packages, or restore the separately deleted root
  README.
- Compatibility or deliberately preserved behavior: package, assembly, namespace, generated API,
  ownership, exception, native artifact identity, and supported `win-x64` behavior remain unchanged
  for OCCT. The existing public `IGenerationProvider.CreatePlanAsync`, `GenerationPlan`, generation
  options, and public pipeline modules remain source and binary compatible; the new semantic
  extension contracts are additive and feed the existing completed-plan boundary. Existing
  internal session and output types remain implementation details and are not exposed. Generated
  text may change only where paths or provider-neutral implementation details have no public or ABI
  effect.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Provider Generator package boundary | Providers can submit completed text through a narrow public plan interface, while reusable semantic construction remains inside OCCT | Shared adds provider-neutral semantic extension contracts through which a provider supplies roots and policies; Shared constructs one normalized model and emits paired outputs into the existing plan boundary without provider-name branches | Existing public `IGenerationProvider`, plan, options, and pipeline APIs; internal session/output encapsulation; deterministic single-model generation and exact-match artifacts |
| OB-02 | OCCT Generator, Runtime, and Windows consumers | All supported OCCT behavior uses projects below `src/core` | The same packages and APIs are produced from `src/providers/occt` through the shared semantic boundary | OCCT API, layout, lifetime, exceptions, native calls, and package contents |
| OB-03 | Repository dependency and source topology | Shared and OCCT projects are mixed below `src/core`, and reusable semantic code has OCCT identity | Shared projects live below `src/shared`; provider projects live below `src/providers/<provider>`; Shared has no provider dependency or provider-specific source knowledge | Provider-neutral tools remain independently packaged |

<!-- acceptance-case: AC-01 -->
### AC-01 — A provider uses the shared semantic engine

```gherkin
Scenario: Generate a complete plan through provider-neutral contracts
  Given an independently packed consumer and a deterministic fixture provider with declarations, type policies, and native build metadata
  When the consumer executes the shared generation pipeline through both the existing plan API and the additive semantic extension contract
  Then Shared produces one normalized plan and matching managed and native outputs without provider-specific names or dependencies or a compatibility break
```

<!-- acceptance-case: AC-02 -->
### AC-02 — OCCT remains usable after extraction

```gherkin
Scenario: Generate and consume the existing OCCT Windows binding
  Given the currently supported OCCT and win-x64 toolchain matrix
  When the OCCT unit, generation, package, and native consumer gates run from the new provider path
  Then the existing public API, layout, ownership, exception, native-call, and package assertions pass
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Source ownership is mechanically enforced

```gherkin
Scenario: Validate project and source dependencies
  Given the reorganized repository
  When the provider-boundary verifier inspects project references, namespaces, and provider terms
  Then Shared has no dependency on or knowledge of OCCT, CGAL, or another concrete provider and every provider depends only toward Shared
```

## Constraints and risks

- Governing constraints: follow [ADR-002](../../adr/ADR-002-cpp-bindings-platform.md),
  [ADR-003](../../adr/ADR-003-native-function-table-bootstrap.md),
  [ADR-004](../../adr/ADR-004-provider-extension-and-cgal-profile.md), and GEN-01 through GEN-05.
  Provider-neutral public contracts may describe semantic categories but may not encode an OCCT or
  CGAL declaration, namespace, library name, default artifact, or policy branch.
- Material risks and recovery: moving projects touches build and package paths broadly; keep the
  change reversible as one source move and contract extraction, do not rename public identities,
  and use the pre-change Git baseline as recovery if OCCT proof cannot be restored.
- Escalation triggers: a required shared contract embeds provider knowledge; an OCCT public or ABI
  behavior must change; a new ownership category, native ABI, platform, or package identity is
  required; or extraction cannot remain one independently provable delivery.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline and accepted ADR-004 blob
`61611f179e3e9a9b2a562f3c1ab09d4899a65c5c`.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: a provider-neutral semantic Generator under `src/shared`, an
  OCCT adapter under `src/providers/occt`, and updated repository paths with unchanged OCCT product
  behavior.
- Other real start conditions or resource prerequisites: .NET 10, PowerShell 7, CMake, MSVC,
  LLVM/Clang, and the repository's supported vcpkg OCCT installation for the real Windows gate.
- Likely touchpoints (non-binding): Shared and OCCT Generator projects, Runtime and Windows project
  locations, source generator, solution, Build scripts, tests, package fixtures, and architecture
  documentation.
- Private implementation choices left open: exact internal interfaces, namespace granularity,
  source-file split, DI registration, fixture design, and edit order, provided public provider
  contracts remain minimal and provider-neutral.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=regression shape=end-to-end -->
<!-- primary-proof: AC-03 purpose=structural shape=contract -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | An independent packed consumer compiles and runs the existing public API plus the additive semantic provider contract, producing deterministic paired outputs without provider dependencies | `pwsh -NoProfile -File Build/VerifyGeneratorPackage.ps1` |
| AC-02 | Primary | OCCT focused tests and independent Generator and Windows package consumers pass with unchanged API and real native behavior | `dotnet run --project tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj -c Release -- --report-trx`; `pwsh -NoProfile -File Build/VerifyOcctGeneratorPackage.ps1`; `pwsh -NoProfile -File Build/VerifyWindowsPackage.ps1` |
| AC-03 | Primary | The repository verifier accepts only the approved source topology and one-way dependency graph | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1` |
| AC-01 | Conditional | Focused Shared unit/component tests cover semantic normalization and paired-emitter behavior below the package contract | `dotnet run --project tests/TedToolkit.CppBindings.Generator.Tests/TedToolkit.CppBindings.Generator.Tests.csproj -c Release -- --report-trx` |
| AC-02 | Conditional | The complete repository build keeps Runtime, analyzers, native fixtures, and OCCT gates green | `dotnet run --project Build/Build.csproj -c Release` |

<!-- section: completion-criteria -->
## Completion

Complete when AC-01 through AC-03 pass on the exact candidate, the full applicable OCCT Windows and
repository regression gates pass, current architecture and package documentation describe the
implemented paths and extension boundary, and no old tracked source path remains. Completion does
not authorize record deletion; a later explicit cleanup continuation may delete it only after the
repository change-retention preconditions are independently satisfied.

## Current candidate evidence

| Area | Candidate result |
| --- | --- |
| Shared semantic ownership | Shared now owns the normalized record/member/type/transport/lifetime/layout/template model, dependency closure, admission, deterministic export slots, and actual managed/native/enum declaration emitters. Before deriving any descriptor, slot, inventory, or delayed renderer, Shared creates one graph-aware detached snapshot that preserves record/type identity and cycles. Mutable Roslyn type syntax, enum expressions, and every record/member/enum description tree are canonicalized into snapshot-owned values. OCCT supplies Clang-derived facts plus a finite emission profile and provider-specific support/build metadata. |
| AC-01 | The independent packed Shared consumer passed all 25 pipeline scenarios plus a post-plan nested-mutation regression, including representative fields, methods, parameters, bases, templates, const-reference transport, narrow rejection diagnostics, detached descriptors, mutable managed type syntax and descriptions, and Shared-produced paired bodies: `out/verification/gp-502ef3d5b144`. Focused Shared tests passed 7/7, including method export, layout, field, enum member/expression, managed/PInvoke type, and nested record/field/method/return/parameter/enum description mutations after plan creation. |
| AC-02 | Focused OCCT tests passed 143/143; the independently packed real-header provider passed two deterministic runs over 36 generated files at `out/verification/op-d28d7126da8f`; the Windows package, direct diagnostics, and real native calls passed at `out/verification/wp-e38c993af188`. The complete repository gate passed 8 modules with 1 expected skip in 52m36s, compiled and linked all 6,989 wrappers, and passed Runtime 40/40, analyzers 17/17, Shared 7/7, OCCT 143/143, and native integration 1/1. |
| AC-03 | `Build/VerifyProviderBoundaries.ps1` passed with 2 Shared projects, 4 provider projects, 1 tool project, 5 allowed project references, and 5 rejected negative fixtures. Provider identifiers are derived from `src/providers`; Shared source rejects both provider type identifiers and provider-name string branches. |
| Documentation and cleanup | Current architecture now distinguishes the delivered Shared+OCCT topology from the accepted future CGAL target. Package and model READMEs describe Shared-owned normalized semantics and paired emission. No tracked source remains under the retired `src/core` path, and no temporary debugging source is present. |
| Candidate integrity | `git diff --check` passed. The exact 461-entry workspace candidate (358 present and 103 tracked deletions) is bound above using the documented `workspace-bundle-v1` framing; this lifecycle record is excluded to avoid self-reference. |

## Final implementation review

- Conclusion: Ready. Independent review covered the exact frozen candidate
  `workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:c0e01fbaa4557d71db3815bbcdf59aeb1aefd96b1629666d9a5c8adaf43d2472`
  and found no Blocking or Important issue.
- AC-01 through AC-03 are covered by candidate-bound source, focused tests, isolated package
  consumers, the full native/managed repository gate, structural negative cases, and independently
  recomputed candidate identity.
- The semantic audit confirmed that record/type identity and cycles are preserved while Roslyn
  types, enum expressions, descriptions, templates, transport, headers, defaults, and nested
  collections are detached before delayed declaration emission.
- Two Low follow-ups remain non-blocking: canonical multiline descriptions could preserve prettier
  contextual indentation, and a few Shared documentation phrases could distinguish plan creation
  from delayed rendering more precisely. Neither changes API, ABI, semantics, or determinism.

## Closure

- Required independent review passed against the exact candidate binding, and the delivery owner
  independently recomputed the same 461-entry digest after review.
- No publication, migration, rollout, or external operational handoff is required by this
  non-publishing repository reorganization.
- Enduring topology, extension-boundary, native bootstrap, package, and ownership knowledge is
  captured in ADR-003, ADR-004, the current architecture document, and current package READMEs.
- This completed record remains active because the approved CGAL delivery cites AC-01 and AC-03 as
  explicit prerequisites; cleanup is deferred until that downstream dependency is removed.

## Latest implementation review

- Conclusion: Not ready. Independent review covered the frozen candidate
  `workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:1d047237dddb5dea5edb3af4fe95a1a1264193e60a0ddb4a8236229bd767b34a`.
- Blocking: the snapshot copied only the outer description collections. Mutable RoslynHelper
  description nodes and nested lists remained caller-owned and were consumed by delayed managed
  and enum emission after plan creation.
- Required correction: canonicalize every record, field, method, return, parameter, enum, and enum
  member description into snapshot-owned content, then add focused and packed-consumer nested
  mutation regressions.
- Verified preservation: the exact 461-entry candidate identity matched independently; fresh
  Shared 7/7, OCCT 143/143, Runtime 40/40, analyzers 17/17, neutral package 25/25, boundary
  verification, diff check, and readiness validation all passed despite the missing partition.

## Previous implementation review

- Conclusion: Not ready. Independent review covered the frozen candidate
  `workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:cb0d4fc93ea52956db02f9f1179778122891e80c470096a02d6dd933e6c779d3`.
- Blocking: `BindingSemanticGraphSnapshot` still retained caller-owned mutable RoslynHelper
  `DataType` nodes for record, field, method, parameter, and enum projections. Mutating
  `PointCounter` after plan construction changed delayed managed emission without changing native
  emission, descriptors, exports, or slots.
- Required correction: detach or canonicalize every mutable managed type projection and audit the
  remaining retained expression objects, then add focused and packed-consumer mutation regressions.
- Verified preservation: fresh Shared 7/7, OCCT 143/143, Runtime 40/40, analyzers 17/17, neutral
  package 25/25, OCCT package 2 runs/36 outputs, Windows/native package, boundary verifier, diff
  check, and exact 461-entry candidate identity all passed despite the missing test partition.

## Correction after the prior implementation review

- Shared now deep-copies the complete normalized record/member/type/template/enum graph before
  closure, validation, descriptor construction, export assignment, or delayed paired emission.
- The snapshot preserves shared record/type identity and recursive cycles while detaching every
  mutable nested collection and mutable semantic node used by either emitter. It copies Roslyn
  `DataType` state through canonical syntax, snapshots enum expressions as canonical code, and
  renders all RoslynHelper description trees once into snapshot-owned canonical content.
- Focused and independently packed regressions mutate exports, alignment, fields, enum members,
  enum expressions, managed/PInvoke type pointer depth, description nodes, and nested description
  lists after plan construction and prove that the plan inventory plus managed/native/enum bodies
  remain mutually consistent. Tracking descriptions also prove caller-owned record, field, method,
  return, parameter, enum, and enum-member nodes are consumed exactly once during snapshotting.
- The root project table and Shared package README now assign normalized semantics and paired
  emission to Shared, while OCCT owns discovery and finite provider policy.

## Earlier implementation review

- Conclusion: Not ready. Independent review covered the frozen candidate
  `workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:990f9a464db97e44a57fde0951b39d1095f75b52d691cff467ac97ee54be03ca`.
- Blocking: AC-01 requires the completed plan, export inventory, managed output, and native output to
  share one immutable normalized semantic authority. `BindingDeclaration` snapshots export strings
  but retains the caller-owned mutable `RecordModel`; the returned `GeneratedSource` delegates read
  that graph later. A post-plan mutation such as `MethodModel.NativeExportName` can therefore leave
  the function table on the old export while both generated bodies use the new export.
- Required correction: construct one graph-aware immutable/deep semantic snapshot before deriving
  descriptors, layouts, exports, slots, or deferred renderers. Preserve record identity and cycles,
  make both emitters consume only that snapshot, and add nested-mutation regressions for methods,
  exports, layout, fields/members, and enums.
- Important: correct the root project table, which still assigns semantic modeling and paired
  emission to the OCCT Generator, and narrow or substantiate the Shared README's immutable-snapshot
  verification claim.
- Candidate-bound verification passed despite the contract defect: Shared 6/6, neutral package
  consumer 25/25, OCCT 143/143, deterministic OCCT package 2 runs/36 files, full Release gate 8
  modules passed with 1 expected skip and all 6,989 wrappers linked, Windows package/native smoke,
  boundary verification, `git diff --check`, validator, and final 460-entry bundle digest.

## Previous implementation review

- Conclusion: Not ready. Independent review covered the frozen candidate
  `workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:b82f31b5d51f161f7b349448a4d8c35786ca4fd9da81df7249243a314cc62a26`.
- Blocking: AC-01 and ADR-004 require reusable normalized declaration/member/type/lifetime/layout/
  template semantics and paired emission in Shared, but OCCT still owns the real semantic graph,
  admission pipeline, and managed/native declaration renderers. Shared currently schedules
  provider-completed render delegates instead of producing representative bindings itself.
- Blocking: AC-01's focused and packed-consumer tests hand-write managed/native bodies inside the
  fixture provider, so their passing oracle cannot detect the semantic-ownership deviation. The
  neutral fixture must cover representative members, bases, transport, ownership, templates,
  rejection diagnostics, and Shared-produced paired declaration bodies.
- Blocking: AC-03's source-content guard recognizes only a narrow set of OCCT spellings and provider
  names inside namespace declarations. It accepts plain concrete-provider identifiers or string
  branches; derive identifiers from `src/providers` and add negative source-content fixtures.
- Blocking: `BindingTemplateDescriptor.Arguments` retains and exposes the caller's list while the
  public semantic model promises immutable snapshots. Defensively copy the arguments and add a
  mutation regression test.
- Important: the current architecture document presents unimplemented CGAL projects and Shared
  ownership as current state. Separate current delivered topology from the accepted target state
  and reconcile the package READMEs after the boundary is corrected.
- Verified preservation: every command passed on the exact candidate, including the full 6,989-unit
  Windows build and package consumers. An independent baseline comparison found 7,342 C# and 6,990
  C++ files on each side, with zero normalized-content differences and only three CRLF/LF-only
  differences. This proves OCCT preservation but does not override the AC-01 or AC-03 gaps.

## First implementation review

- Conclusion: Not ready. Independent review covered the frozen candidate
  `workspace:46ae69f2888c19fcaecedd4fe75eca82374672a4:sha256:8ce5adfaa36551e5af60793fbe81a1fcbd349b483627f10b9cb8e678e93d4e60`.
- Blocking: AC-01 is not implemented as approved. Shared currently dispatches type, layout,
  template, and naming policies, while each provider still owns normalized model construction,
  dependency closure, paired managed/native emission, and final `GenerationPlan` construction.
- Blocking: AC-03's verifier rejects provider knowledge in Shared and legacy paths, but does not
  reject provider-to-different-provider or provider-to-tools project references.
- Important: ADR-003 still links to the removed `src/core` directory.
- Required correction: move the provider-neutral normalized model and paired plan orchestration
  into Shared, strengthen the structural verifier with positive and negative dependency-graph
  cases, repair the durable link, and rerun candidate-bound proof and review.

## Rejected correction candidate evidence

| Area | Exact candidate result |
| --- | --- |
| Shared semantic orchestration | Shared now closes explicit provider roots over declaration dependencies, validates and normalizes admitted declarations, assigns deterministic export slots, groups managed declarations, constructs managed/native inventories, and creates the final `GenerationPlan`. The independent packed consumer passed all 25 scenarios; evidence is `out/verification/gp-17e77f90e9d6`. |
| OCCT adaptation | OCCT now supplies `BindingProviderModel` declarations, policies, renderers, supplemental sources, and native build metadata without constructing `GeneratedSource` or `GenerationPlan`. Focused OCCT tests passed 143/143, and the independent real-header package verifier passed at `out/verification/op-0dd8f0e0c339`. |
| Structural enforcement | `Build/VerifyProviderBoundaries.ps1` passed with 2 Shared projects, 4 provider projects, 1 tool project, 5 allowed project references, and 3 rejected negative fixtures covering Shared-to-provider, cross-provider, and provider-to-tools edges. ADR-003 now links to the current Shared and OCCT project locations. |
| Repository and Windows regression | `dotnet run --project Build/Build.csproj -c Release` passed 8 modules with 1 expected skip, generated and linked all 6,989 native wrappers, passed Shared 4/4, OCCT 143/143, Runtime 40/40, analyzers 17/17, and native handle integration 1/1. The independent Windows package/native consumer passed at `out/verification/wp-a71704724eb2`. |
| Candidate integrity | `git diff --check` passed. The exact 456-entry workspace candidate is bound above using `workspace-bundle-v1`; this lifecycle record is excluded from the digest to avoid self-reference. |

## Rejected candidate evidence

| Area | Exact candidate result |
| --- | --- |
| Shared extension boundary | Added ordered type projection, layout admission, template admission, and named emitter primitives under `src/shared/TedToolkit.CppBindings.Generator/Semantics`; the neutral packed consumer passed all 25 scenarios. |
| OCCT adaptation | Moved OCCT Generator, Runtime, Windows, and source-generator projects below `src/providers/occt`; the OCCT adapter supplies its policies through the shared semantic engine without changing public package identities. |
| Focused tests | Shared Generator 3/3, OCCT Generator 143/143, Runtime 40/40, analyzers 17/17, and the native handle fixture 1/1 passed. |
| Package and native proof | Neutral Generator, OCCT Generator, and Windows package verifiers passed; the real Windows build generated and linked 6,989 native wrappers and the final independent Windows consumer result is `out/verification/wp-089e878b6b80/result.json`. |
| Repository gate | `dotnet run --project Build/Build.csproj -c Release` completed with 8 modules passed and 1 expected module skipped. |
| Structural proof | `Build/VerifyProviderBoundaries.ps1` passed with 2 Shared projects, 4 provider projects, and 5 verified dependency edges; `git diff --check` passed. |
| Scope and cleanup | No public or ABI deviation was required, no temporary debugging instrumentation remains, and all known source, solution, build, test, package, and documentation paths use the approved topology. |
