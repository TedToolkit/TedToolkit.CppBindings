# Route every provider through the shared generation model

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: approved -->
<!-- delivery-shape: multi-item -->

- Priority: P2
<!-- approval-source: maintainer approved the revised Shared native-error contract and explicitly continued in the Codex task on 2026-09-09 with "没错，就是这样。我同意，然后开始修改！" -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

Make the shared Generator and Runtime the single implementation authorities for provider-neutral plan
construction, managed/native emission, function-table bootstrap, native-error transport and projection,
and output publication. Provider Generators and Runtimes remain independently consumable owners of
native-library profiles, discovery, semantic policy, and provider-local error extensions, but no longer
carry parallel binding engines or copies of the common managed projection. FCL and Manifold currently
duplicate complete C# and C++ renderers, while all four provider Runtimes repeat diagnostic consumption
and common exception mapping; the copies have already drifted on the meaning of error kind 7.

<!-- section: scope -->
## Scope and non-goals

- In scope: provider-neutral buffer and composite-result semantics required by more than one provider;
  migration of FCL and Manifold to `SemanticGenerationProvider`; removal of their private plan/output
  pipelines and complete source renderers; consolidation of generic CGAL/OCCT result, error, native
  project, and legacy emitter helpers where the shared model can own them; one Shared native-error
  category contract, managed projection, diagnostic interface, and exception family; provider-local
  extension projection; provider-boundary tests; and current architecture/documentation updates.
- Non-goals: changing a locked provider profile, native dependency version, public generated Windows
  binding operations, native package layout, platform/RID support, provider-specific failure semantics,
  or the separate Windows generation-host orchestration change.
- Compatibility: generated OCCT, CGAL, Manifold, and FCL operation signatures, ownership and cleanup
  behavior, native artifact identities, inventories, and package-consumer behavior remain unchanged.
  Native failures in the common categories intentionally migrate from provider-prefixed exception types
  to Shared `Native*Exception` types with the corresponding familiar .NET base and common diagnostics.
  Provider-specific failures retain provider exception types. These packages are unreleased, so the
  superseded provider-prefixed common exception types and FCL/Manifold Generator-only APIs receive no
  compatibility shims.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | FCL and Manifold Generator consumption | Each provider exposes a parallel plan type and directly renders and writes complete binding sources | Both providers expose the shared asynchronous generation contract and submit provider facts to the shared semantic engine | Locked profile identities, generated public APIs, native behavior, inventories, and deterministic generation |
| OB-02 | Shared/provider implementation boundary | Providers may emit their own NativeApi, function table, standard error transport, CMake boilerplate, declaration bodies, and complete managed native-error projection | Shared owns provider-neutral generation and managed Runtime mechanisms; providers own only finite profiles, discovery, semantic policy, native dependencies, provider-local error extensions, and unavoidable library-specific semantic inputs | Shared never branches on a provider name or depends on a provider assembly |
| OB-03 | Native error category and projection contract | Common categories and diagnostic ownership are repeated in four Runtimes; OCCT uses 8 for `OcctFailure` and 9 for `StandardException`, while CGAL projects both overflow and underflow through kind 7 as arithmetic | Shared owns kinds 0 through 8 and 255, common diagnostic consumption, and common `Native*Exception` results; 8 is `StandardException`, 9 through 254 are Provider-local and may overlap across Provider Runtimes; OCCT uses 9 for `OcctFailure`; CGAL projects overflow as 7 and underflow as 3 | Every nonzero kind fails, cleanup runs exactly once in the originating native module, diagnostic loss does not suppress failure, and Provider-specific exception meaning remains local |

<!-- acceptance-case: AC-01 -->
### AC-01 — FCL uses the shared semantic pipeline

```gherkin
Scenario: Generate the locked FCL profile through Shared
  Given the supported FCL profile and provider-owned semantic inputs
  When an FCL caller creates a plan through the shared asynchronous provider contract
  Then Shared emits the complete paired managed/native plan and the former FCL plan, writer, NativeApi, function-table, error, and complete renderer implementations are absent
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Manifold uses the shared semantic pipeline

```gherkin
Scenario: Generate the locked Manifold profile through Shared
  Given the supported Manifold profile and provider-owned semantic inputs
  When a Manifold caller creates a plan through the shared asynchronous provider contract
  Then Shared emits the complete paired managed/native plan and the former Manifold plan, writer, NativeApi, function-table, error, and complete renderer implementations are absent
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Existing semantic providers contain no generic emitter copies

```gherkin
Scenario: Generate OCCT and CGAL after consolidation
  Given their existing discovery results and finite semantic profiles
  When each provider creates its generation plan
  Then generic result projection, standard error transport, native project construction, and legacy emitter adapters are supplied by Shared wherever they are not native-library policy
```

<!-- acceptance-case: AC-04 -->
### AC-04 — All locked Windows providers preserve consumer behavior

```gherkin
Scenario: Build and consume every provider after migration
  Given the four locked Windows provider profiles and their supported toolchains
  When their Generator, Windows package, isolated consumer, and coexistence verification runs
  Then public generated operation APIs, paired exports and slots, ownership, cleanup, approved Shared and provider-specific exception behavior, native artifact identities, inventories, package layouts, and real native calls retain their approved results
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Native error projection has one common authority and local Provider extensions

```gherkin
Scenario: Project common and Provider-specific native failures
  Given a native error produced by any supported Provider module
  When its matching managed Runtime consumes the carrier
  Then Shared alone interprets kinds 0 through 8 and 255, kind 8 means StandardException, the originating module clears diagnostics exactly once, and a Provider interprets an otherwise unrecognized kind only through its own extension without requiring that extension number to be unique across Providers

Scenario: Project OCCT and CGAL failures after correcting the category drift
  Given OCCT Standard_Failure, a standard C++ exception, CGAL overflow, and CGAL underflow failures
  When the generated adapters and matching Runtime projection handle them
  Then OCCT Standard_Failure uses local kind 9, the standard C++ exception uses common kind 8, CGAL overflow uses common kind 7, CGAL underflow uses common kind 3, and each throws the approved Shared or Provider-specific exception
```

## Constraints and risks

- [GEN-01](../../principles/README.md) requires every binding layer to derive from one normalized
  semantic model. [ADR-004](../../adr/ADR-004-provider-extension-and-cgal-profile.md) keeps provider
  profiles and policy outside Shared and fixes the provider-to-shared dependency direction.
- [ADR-006](../../adr/ADR-006-shared-native-error-projection.md) fixes the Shared category set,
  Provider-local extension space, common managed projection, and exception ownership boundary.
  This design is pinned to its accepted uncommitted Git blob
  `6b984e8ec5c17f7e732ac8d1da84dd8702290c03`.
- A common abstraction is admitted only when at least two providers need the semantic category or
  when it is provider-neutral bootstrap/output infrastructure. Shared must not acquire FCL,
  Manifold, CGAL, or OCCT names, declarations, dependency targets, or policy branches.
- Shared kinds are exactly `None=0`, `Argument=1`, `ArgumentOutOfRange=2`, `Arithmetic=3`,
  `InvalidOperation=4`, `NullObject=5`, `OutOfMemory=6`, `Overflow=7`, `StandardException=8`, and
  `Unknown=255`. Values 9 through 254 have no cross-Provider meaning; two Provider Runtimes may use
  the same value for different local failures. A Provider extension cannot override a Shared kind.
- The main risks are subtly changed bulk-buffer validation, composite/owned result construction,
  function-slot ordering, exception cleanup, public catch behavior, or finalization behavior.
  Recovery is one-revision restoration of the former provider renderers and exception surfaces; no
  persisted consumer data requires migration.
- The active CGAL vcpkg header-inventory work is a worktree collision, not a logical prerequisite;
  its final discovery/profile behavior must be preserved when the CGAL item is implemented.
- The separate central Windows generation host should consume this change's uniform provider
  contract rather than add temporary adapters to the old FCL and Manifold APIs.
- Escalate for a changed generated operation API, provider profile, native carrier layout or artifact
  layout, ownership category, another Shared error category, globally unique Provider extension
  numbering, new platform/RID, provider branch in Shared, or need for a compatibility layer for the
  unreleased removed Generator or exception APIs.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline. Full Windows proof requires the configured `VCPKG_ROOT`,
CMake, MSVC, and installed locked provider dependencies.

<!-- section: delivery-brief -->
## Delivery disposition

The shared semantic extension and its first proving provider, the second duplicated provider,
the existing semantic-provider cleanup, and final cross-provider enforcement are independently
verifiable outcomes with real supplied inputs. `work-items.md` owns their separately approved map.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=component -->
<!-- primary-proof: AC-02 purpose=acceptance shape=component -->
<!-- primary-proof: AC-03 purpose=structural shape=component -->
<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
<!-- primary-proof: AC-05 purpose=acceptance shape=component -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | FCL produces a deterministic shared `GenerationPlan` with one paired semantic authority and no provider-private generation pipeline | `dotnet run --project tests/TedToolkit.CppBindings.Fcl.Generator.Tests/TedToolkit.CppBindings.Fcl.Generator.Tests.csproj -c Release -- --report-trx` |
| AC-02 | Primary | Manifold produces a deterministic shared `GenerationPlan` with one paired semantic authority and no provider-private generation pipeline | `dotnet run --project tests/TedToolkit.CppBindings.Manifold.Generator.Tests/TedToolkit.CppBindings.Manifold.Generator.Tests.csproj -c Release -- --report-trx` |
| AC-03 | Primary | Structural verification finds one provider-neutral implementation of generic emission and no provider-to-provider or Shared-to-provider dependency | `pwsh -NoProfile -File Build/VerifyProviderBoundaries.ps1` |
| AC-04 | Primary | The repository pipeline builds, packages, and exercises the four locked providers and their coexistence without approved behavioral drift | `dotnet run --project Build/Build.csproj -c Release` |
| AC-05 | Primary | Shared and Provider Generator emission tests plus Runtime tests cover the fixed catch order and codes, every common kind, overlapping local extension values, corrected OCCT/CGAL mappings, strict diagnostic fallback, and exact-once originating-module cleanup | Run the Shared, OCCT, and CGAL Generator TUnit projects and the Shared, OCCT, CGAL, Manifold, and FCL Runtime TUnit projects with `dotnet run -c Release -- --report-trx` |
| Generator package surface | Conditional | Shared, OCCT, and CGAL Generator packages remain independently consumable after their public model changes | Run `Build/VerifyGeneratorPackage.ps1`, `Build/VerifyOcctGeneratorPackage.ps1`, and `Build/VerifyCgalGeneratorPackage.ps1` |

<!-- section: completion-criteria -->
## Completion

All five acceptance cases pass on one exact integrated candidate; the unreleased obsolete Generator
and common exception entry points and duplicate emitters/projections are absent; current architecture
and provider documentation describe the resulting boundary; no provider-specific policy has entered
Shared; and candidate-bound implementation review is Ready. After merge and release of all references,
delete this active change record under repository policy; Git history retains it.
