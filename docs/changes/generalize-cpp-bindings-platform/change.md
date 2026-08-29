# Generalize the OCCT repository into the TedToolkit.CppBindings platform

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: draft -->
<!-- delivery-shape: multi-item -->

- Priority: P1
<!-- approval-source: none -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

The repository becomes `TedToolkit.CppBindings`: a reusable .NET platform for generating and
shipping object-oriented C++ library bindings. Declaration-agnostic generation, runtime ownership,
and analyzer policy are separated from the OCCT provider, while the completed OCCT behavior remains
available through `TedToolkit.CppBindings.Occt`. The migration removes the current product-identity
coupling before a CGAL provider is introduced and includes the coordinated GitHub repository rename.

<!-- section: scope -->
## Scope and non-goals

- In scope: repository, solution, project, assembly, package, root namespace, documentation, CI,
  badge, source-link, and package-metadata migration from `TedToolkit.Occt` to
  `TedToolkit.CppBindings`; the GitHub repository rename from `TedToolkit/TedToolkit.Occt` to
  `TedToolkit/TedToolkit.CppBindings`; and local `origin` migration after the remote rename.
- In scope: declaration-agnostic `TedToolkit.CppBindings.Generator`,
  `TedToolkit.CppBindings.Runtime`, and separately published
  `TedToolkit.CppBindings.Analyzers` packages; provider-specific
  `TedToolkit.CppBindings.Occt.Generator`, `TedToolkit.CppBindings.Occt.SourceGenerators`,
  `TedToolkit.CppBindings.Occt.Runtime`, and the existing planned
  `TedToolkit.CppBindings.Occt.Windows` identity.
- In scope: a dependency direction in which OCCT projects depend on the generic core and the core
  has no OCCT, CGAL, native-library, or generated-declaration knowledge; mirrored tests and current
  architecture, principles, and maintainer documentation.
- In scope: a built-in default Windows generation profile for the currently proved MSVC, cdecl,
  `win-x64`, native-build, and packaging conventions. A generic Windows package is created only if
  it later gains an independently useful responsibility.
- Non-goals: implementing CGAL bindings; creating empty CGAL or generic Windows projects; adding
  Linux, another ABI, RID, or architecture; publishing packages or releases; renaming the GitHub
  organization; or retaining compatibility aliases for unreleased `TedToolkit.Occt.*` identities.
- Non-goals: a public `Borrowed<T>` type, borrowed wrapper class, lease, or callback lifetime
  abstraction. Borrowing remains an operation-level fact represented by generated signatures,
  direct `ref T`, or an approved pointer; callers own use-after-free discipline and analyzers offer
  suppressible best-effort guidance.
- Compatibility: no supported public binding package has been released. The repository therefore
  performs one coordinated identity migration without type-forwarder assemblies, alias packages,
  or dual namespaces. OCCT native layout, ownership, cleanup, exception, and fail-closed behavior
  remain unchanged.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Product and repository identity | Repository and product family are named `TedToolkit.Occt` | Repository, solution, metadata, documentation, and GitHub remote use `TedToolkit.CppBindings` | Git history, default branch, issues, pull requests, permissions, and required repository settings remain available |
| OB-02 | Dependency boundaries | Shared generation and runtime mechanisms are coupled to OCCT project identities | Generic Generator, Runtime, and Analyzer packages contain no provider knowledge; OCCT projects depend inward on them | One semantic model remains the authority for each generated binding chain |
| OB-03 | Managed API identity | Shared and OCCT-specific APIs coexist under `TedToolkit.Occt` | Shared APIs use `TedToolkit.CppBindings`; OCCT APIs use `TedToolkit.CppBindings.Occt`; package and assembly names follow the same hierarchy | Platform suffixes such as `.Windows` do not enter generated API namespaces |
| OB-04 | Ownership model | `Owned<T>`, `Handle<T>`, value layouts, and analyzer rules are all housed under OCCT identities | Generic Runtime owns declaration-agnostic `Owned<T>` and its eligibility contract; OCCT Runtime owns intrusive `Handle<T>` and OCCT exceptions; values remain structs with no owner wrapper | OCCT retains Value, Owned, and Handle semantics, exactly-once cleanup, direct native-style borrowing, and fail-closed unsupported types |
| OB-05 | Analyzer delivery | Runtime embeds an internal OCCT-named analyzer assembly | `TedToolkit.CppBindings.Analyzers` is a separately packed, provider-neutral analyzer package referenced directly by consumers with `PrivateAssets="all"` | Diagnostics are best-effort and suppressible; Runtime remains the executable authority for checks it can perform |
| OB-06 | Windows defaults and provider growth | Windows assumptions are mixed with the OCCT-specific repository | Generic Generator supplies the proved Windows default profile; OCCT Windows remains the concrete ready-package identity; another provider can reuse the core without an OCCT dependency | Only the proved `win-x64` matrix is claimed, and new providers or platforms require separate changes |

<!-- acceptance-case: AC-01 -->
### AC-01 — Repository identity migrates as one recoverable operation

```gherkin
Scenario: Open and consume the renamed repository
  Given the internal migration candidate is verified
  When the maintainer renames the GitHub repository to TedToolkit.CppBindings
  Then the new repository URL is authoritative
  And the local origin, solution, package metadata, source links, documentation, badges, and CI references use the new identity
  And the default branch, history, issues, pull requests, permissions, and required settings are verified after the rename
  And no tracked current artifact refers to the old repository as authoritative
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Generic core and OCCT provider have one-way dependencies

```gherkin
Scenario: Inspect and build the migrated project graph
  Given the renamed solution and project tree
  When project and package dependencies are evaluated
  Then generic Generator, Runtime, and Analyzers contain no OCCT, CGAL, or generated-declaration dependency
  And OCCT Generator and Runtime depend only on the generic contracts they consume
  And provider-specific source generation remains separate from consumer diagnostics
  And every production or package project introduced by this migration has one documented responsibility and no forbidden dependency edge
  And every current public Generator, source-generator, and analyzer API family matches the approved identity disposition table
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Public names and ownership categories match their responsibility

```gherkin
Scenario: Inspect representative generic and OCCT public APIs
  Given the migrated Runtime and OCCT provider assemblies
  When their public surfaces are inspected and representative lifetimes execute
  Then generic APIs use TedToolkit.CppBindings
  And OCCT APIs use TedToolkit.CppBindings.Occt
  And generic Runtime supplies Owned<T> with a provider-neutral RAII eligibility contract
  And OCCT Runtime supplies Handle<T> for Standard_Transient ownership and OCCT exception projection
  And every migrated public contract matches the approved identity disposition table
  And generated value layouts remain unmanaged structs without a Value, Owned, Handle, or Borrowed wrapper layer
  And disposal, finalization, copying, borrowing, and unsupported-type behavior preserve the approved OCCT contracts
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Consumers opt into one provider-neutral analyzer package

```gherkin
Scenario: Consume the packed analyzer with generic and OCCT code
  Given an isolated project references TedToolkit.CppBindings.Analyzers directly with PrivateAssets all
  When representative valid, invalid, borrowed, disposed, and generated-only usages compile
  Then provider-neutral diagnostics are delivered from analyzer assets without a runtime assembly reference
  And rules derive from generic contracts or metadata instead of hard-coded OCCT type identities
  And diagnostic IDs and suppression documentation match the approved identity disposition table
  And valid generated OCCT use remains diagnostic-free
  And each diagnostic can be suppressed under the documented caller-responsibility model
```

<!-- acceptance-case: AC-05 -->
### AC-05 — The migrated platform preserves OCCT and exposes a reusable Windows default

```gherkin
Scenario: Generate and run the representative OCCT binding after migration
  Given the completed model-driven OCCT generator and the built-in Windows default profile
  When the Release generator, runtime, analyzer, and native integration gates run
  Then the same supported OCCT declarations produce deterministic exact-layout bindings
  And representative Value, Handle, and Owned operations call and clean up through the matching native artifact
  And unsupported declarations and unsupported RIDs still fail before native access
  And no generic core artifact depends on OCCT
  And no CGAL or generic Windows package is required for the proof
```

## Constraints and risks

- [ADR-002](../../adr/ADR-002-cpp-bindings-platform.md) and the
  [C++ bindings platform architecture](../../architecture/cpp-bindings-platform.md) govern product
  identity, package allocation, and generic/provider dependency direction. The repository design
  principles, generated binding architecture, Runtime/analyzer boundary, and ADR-001 continue to
  govern exact generation, ownership, native loading, and failure behavior. Before completion their
  current terminology must match the delivered platform without claiming CGAL support.
- `TedToolkit.CppBindings.Runtime` owns only declaration-agnostic mechanisms. OCCT-specific
  intrusive reference counting, exception types, classifications, symbols, layouts, and generated
  sets stay in `TedToolkit.CppBindings.Occt.*`.
- `TedToolkit.CppBindings.Analyzers` is a separate NuGet package and a direct consumer dependency;
  it is not embedded in Runtime and is not assumed to flow transitively. The OCCT header source
  generator remains a distinct non-packable build component distributed with the OCCT Generator.
- Repository, project, assembly, package, namespace, and URL changes form one migration boundary.
  Partial aliases are not a supported intermediate public state. Existing active change records
  must be reconciled to the new identity before their later approval or continuation.
- The breaking migration assumes no `TedToolkit.Occt.*` package or assembly has been distributed as
  a supported external compatibility baseline. Discovery of such a consumer before implementation
  pauses the change and requires a compatibility decision rather than silently adding aliases.
- Migration risk is high because public symbols, package identities, project paths, generated text,
  tests, documentation, and external repository state change together. Preserve unrelated working-
  tree changes and perform the rename only from an exact reviewed candidate.
- Escalate if implementation requires a public Borrowed wrapper, a generic core dependency on a
  provider, dual public identities, compatibility packages, another provider or platform, remote
  publication, or a different GitHub organization.

### Public contract identity disposition

| Current contract | Target owner and identity | Disposition |
| --- | --- | --- |
| `TedToolkit.Occt.Owned<T>` | `TedToolkit.CppBindings.Owned<T>` in `TedToolkit.CppBindings.Runtime` | Rename and move; preserve direct-storage RAII behavior |
| `TedToolkit.Occt.IOcctRaii` | `TedToolkit.CppBindings.ICppRaii` in `TedToolkit.CppBindings.Runtime` | Replace with provider-neutral eligibility marker |
| `TedToolkit.Occt.Attributes.NativeTypeNameAttribute` | `TedToolkit.CppBindings.NativeTypeNameAttribute` in `TedToolkit.CppBindings.Runtime` | Rename namespace and generalize documentation |
| `TedToolkit.Occt.Runtime.GeneratedCodeOnlyAttribute` | `TedToolkit.CppBindings.GeneratedCodeOnlyAttribute` in `TedToolkit.CppBindings.Runtime` | Rename namespace and generalize documentation |
| `TedToolkit.Occt.NativeError` | `TedToolkit.CppBindings.NativeError` in `TedToolkit.CppBindings.Runtime` | Move the provider-neutral ABI carrier |
| `TedToolkit.Occt.Handle<T>` and `IStandard_Transient` | Same type names under `TedToolkit.CppBindings.Occt` in `TedToolkit.CppBindings.Occt.Runtime` | Rename namespace; preserve intrusive ownership |
| `TedToolkit.Occt.NativeErrorProjection` and `Occt*Exception` / `IOcctException` | Same type names under `TedToolkit.CppBindings.Occt` in `TedToolkit.CppBindings.Occt.Runtime` | Rename namespace; preserve OCCT error semantics |
| `TTOCCT001` generated-only usage diagnostic | `TTCB001` in `TedToolkit.CppBindings.Analyzers` | Rename ID and update suppression/documentation references |
| `TTOCCT002` non-owning reference lifetime diagnostic | `TTCB002` in `TedToolkit.CppBindings.Analyzers` | Rename ID and update suppression/documentation references |
| Generated value category | Unmanaged structs under `TedToolkit.CppBindings.Occt` | Preserve; do not introduce a `Value<T>` or `Borrowed<T>` wrapper |
| `GenerationOptions` | Generic `GenerationOptions` in `TedToolkit.CppBindings.Generator` plus OCCT-specific `OcctGenerationOptions` in `TedToolkit.CppBindings.Occt.Generator` | Split output, namespace, artifact, and target-profile settings from OCCT header, vcpkg, declaration-selection, and classification settings |
| `DeclOptions` | `OcctDeclarationOptions` in `TedToolkit.CppBindings.Occt.Generator` | Rename and keep OCCT declaration selection provider-owned |
| `CleanGenerationOutputModule`, `GenerateCppModule`, and `GenerateCSharpModule` | Same type names under `TedToolkit.CppBindings.Generator` | Move the provider-neutral pipeline stages; provider emitters enter through generic contracts |
| `ParseModule` | `OcctParseModule` under `TedToolkit.CppBindings.Occt.Generator` | Rename and keep OCCT/vcpkg header acquisition and classification provider-owned |
| `PipelineBuilderExtension.AddOcctGenerators` | `OcctPipelineBuilderExtensions.AddOcctGenerators` under `TedToolkit.CppBindings.Occt.Generator` | Rename the extension container and preserve the OCCT registration entry point |
| Current internal Generator models and services | Generic internals move to `TedToolkit.CppBindings.Generator`; OCCT parsing/classification internals move to `TedToolkit.CppBindings.Occt.Generator` | Preserve non-public visibility unless a separately approved contract requires exposure |
| `TedToolkit.Occt.Analyzer.OcctHeaderTypeGenerator` | `TedToolkit.CppBindings.Occt.SourceGenerators.OcctHeaderTypeGenerator` in the non-packable OCCT SourceGenerators component | Rename namespace; remain public only for Roslyn discovery and ship only as an analyzer asset of the OCCT Generator package |
| Generated `TedToolkit.Occt.Generator.OcctHeaderType` | `TedToolkit.CppBindings.Occt.Generator.OcctHeaderType` | Rename namespace and preserve the generated OCCT header-selection enum role |
| `GeneratedCodeOnlyUsageAnalyzer` and `ValueLifetimeAnalyzer` | Same type names under `TedToolkit.CppBindings.Analyzers` | Rename namespace; remain public only for Roslyn discovery and ship only as analyzer assets |

The table is exhaustive for the current public Runtime, Generator, generated header-selector,
source-generator, and analyzer families found on the approved baseline. A newly discovered material
public contract or a request to retain an old identity is an escalation trigger and requires renewed
change approval; work-item planning cannot decide it.

### GitHub repository rename handoff

The repository maintainer executes this ordered handoff using GitHub's documented
[repository rename behavior](https://docs.github.com/en/repositories/creating-and-managing-repositories/renaming-a-repository):

1. Confirm administrator permission and that `TedToolkit/TedToolkit.CppBindings` is available.
   Inventory GitHub Pages, actions hosted from this repository, reusable-workflow references,
   webhooks, environments, branch rules, package links, and external integrations; record each as
   not applicable or resolved by a named owner before proceeding. Pages must have an approved new
   URL/custom-domain disposition. Hosted actions and reusable workflows must have every maintained
   consumer reference updated through an approved coordinated cutover; if either cannot be closed,
   stop and escalate an alternative repository strategy before merging the internal migration.
2. Freeze the exact reviewed internal migration candidate, pass its required gates, merge it through
   the normal default-branch process, and record the resulting commit. Do not rename from an
   unreviewed or locally dirty candidate.
3. Rename the GitHub repository to `TedToolkit.CppBindings`, then update local `origin`, CI and
   integration references, source links, Pages or hosted-action references when applicable, and
   maintained consumer instructions. Do not treat GitHub redirects as the final configuration.
4. Capture the new URL and verify the default branch, commit history, issues, pull requests, tags,
   releases, permissions, branch rules, secrets/environments, webhooks, Actions, badges, and a clean
   clone/build from the authoritative URL.
5. If preflight fails, do not merge or rename. If the external rename cannot complete in the same
   maintenance window after the internal candidate merged, the repository maintainer restores the
   old authoritative state through a reviewed revert commit and retries only from a new exact
   candidate in a later approved window. If post-rename validation cannot be fixed safely, rename
   back while the old name remains available, restore all remote references, revert the internal
   identity migration through the normal reviewed commit process, and retain the captured failure
   evidence. A reused old name or another condition that prevents rollback is an immediate
   escalation.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../generate-unversioned-model-driven-bindings/change.md contract=AC-06 -->

| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | The generated exact-layout managed/native OCCT replacement builds and runs against real pinned OCCT without an active ABI-v1 path | `../generate-unversioned-model-driven-bindings/change.md`, AC-06 | Source contract is completed on the exact Git baseline selected for migration implementation |

<!-- section: delivery-brief -->
## Delivery disposition

This Controlled change requires several independently verifiable deliveries: generic core
extraction, OCCT provider migration, analyzer packaging and consumption, repository-wide identity
and documentation migration, and the final GitHub operational handoff. After change approval,
`plan-work-items` will create the smallest dependency-ordered map and stop for separate map approval.
The GitHub rename remains an operational handoff rather than a development work item.

Likely touchpoints are the solution and project tree under `src/` and `tests/`, package/build props,
generator and Runtime public namespaces, analyzer packaging, README and durable architecture records,
CI/repository metadata, and the current OCCT package change. Exact private file moves, internal type
names, and extraction mechanics remain implementation choices inside the approved boundaries.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=boundary shape=manual -->
<!-- primary-proof: AC-02 purpose=structural shape=component -->
<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
<!-- primary-proof: AC-05 purpose=regression shape=integration -->

| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | The new GitHub identity, local remote, repository metadata, CI, history, and settings are authoritative and operational | From the exact verified candidate, perform the maintainer-owned rename checklist and record the new URL plus post-rename validation |
| AC-02 | Primary | The `.slnx` and production/package `.csproj` inventory gives every introduced project one documented responsibility, contains no generic-core-to-provider edge, and maps every public Generator, source-generator, and analyzer family to its approved owner; the renamed solution builds | Enumerate solution membership, public Generator/source-generator/analyzer surfaces, and `ProjectReference`/package edges; fail on a missing disposition, core-to-provider edge, or undocumented introduced project; then run `dotnet build TedToolkit.CppBindings.slnx -c Release` |
| AC-03 | Primary | Public Runtime API and lifetime contracts expose every approved identity disposition plus Value, Owned, and OCCT Handle categories with no Borrowed wrapper | Run public API contract tests and the Runtime and OCCT Runtime TUnit projects in Release |
| AC-04 | Primary | An isolated consumer receives the standalone analyzer assets, expected diagnostics, suppressions, and no analyzer runtime dependency | Pack to an isolated feed and build the analyzer contract consumer matrix |
| AC-05 | Primary | The migrated generator and Windows defaults reproduce deterministic OCCT bindings and real native lifetime behavior without a core-to-OCCT dependency | Run Generator, Runtime, analyzer, and representative native integration projects in Release against the pinned `win-x64` matrix |
| Migration inventory | Conditional | No tracked current project, package, namespace, metadata, documentation, or CI identity remains under the old authoritative name except explicit historical/migration references | Run a bounded repository identity scan and review every allowed old-name occurrence |

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-05 pass; the generic core and OCCT provider boundaries, dependency direction,
ownership policy, analyzer distribution, Windows default profile, and new identities are reflected
in code, tests, packages, the solution, current documentation, architecture, and principles; the
GitHub repository and local remote use `TedToolkit.CppBindings`; the existing OCCT package delivery
record is revised or superseded so it cannot reintroduce old identities; no compatibility alias,
Borrowed wrapper, CGAL implementation, unsupported platform claim, or remote package publication is
included. After merge and durable-record extraction, this completed change record is removed under
the repository retention policy.
