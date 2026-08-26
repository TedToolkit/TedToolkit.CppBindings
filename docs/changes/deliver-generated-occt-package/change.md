# Deliver the ready-to-use Windows exact-layout OCCT binding package

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: draft -->
<!-- delivery-shape: multi-item -->

- Priority: P1
<!-- approval-source: none -->
<!-- candidate-binding: none -->
- Approval: The current architecture changes the public package and managed assembly identity from
  `TedToolkit.Occt` to `TedToolkit.Occt.Windows` and rejects the former platform-neutral package
  direction. This revised contract and delivery map require explicit reapproval before
  implementation.

<!-- section: goal-rationale -->
## Goal and rationale

A .NET consumer can install `TedToolkit.Occt.Windows` and call the safely supported OCCT surface on
the pinned `win-x64` matrix without installing OCCT, vcpkg, Clang, CMake, or the Generator. The
Windows-named package and assembly ship one exactly matched managed/native artifact set whose public
object, inheritance, generic, operation, exception, and lifetime surfaces follow GEN-02, GEN-03,
and the current generated binding architecture.

<!-- section: scope -->
## Scope and non-goals

- In scope: the `TedToolkit.Occt.Windows` package and managed assembly; release-time enumeration of
  every pinned public OCCT header; deterministic generated,
  unsupported, and excluded dispositions; exact sequential C# object layouts; generated inheritance
  interfaces; proved generic templates including eligible `NCollection_Array1<T>` specializations;
  extension-method operations; transient `Handle<T>` ownership; trivial values; non-transient
  `Owned<T>` ownership; managed exceptions; exact contract matching; complete `win-x64`
  native closure; source-faithful documentation; and isolated NuGet consumption proof.
- In scope: `TedToolkit.Occt` as the generated C# root-namespace default, independent from the
  Windows package/assembly identity; release selection of the post-emission native build; and a
  minimal declaration-agnostic `TedToolkit.Occt.Runtime` dependency while declaration-specific data
  and operations remain in the generated Windows assembly.
- Release baseline: OCCT 8.0.1, `x64-windows`, cdecl C-compatible exports, one machine-readable
  native build identity that pins the vcpkg baseline, MSVC toolset, CRT, packing, and all
  layout-affecting options, `net8.0` as the only compiled managed target, and an unversioned exact-match native
  artifact set.
- Non-goals: an unsuffixed public `TedToolkit.Occt` binding package or assembly; full OCCT coverage;
  invented support for unsupported declarations; another RID, architecture, or platform package;
  independently upgradeable native artifacts; consumer-time generation; remote feed publication;
  or compatibility with unreleased ABI-v1/descriptor prototypes.
- Compatibility: the generated public managed API becomes the first package baseline. Native
  layout identity is specific to the shipped pinned native closure and is not a portable C++ ABI.
- Preserved behavior: unsupported declarations fail closed; native exceptions project to managed
  exception categories; ownership and cleanup are explicit and same-library; package claims are
  derived from generated coverage and real consumer evidence.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Coverage | No consumable package has a complete pinned declaration inventory | Every in-scope declaration has one deterministic disposition | Unsupported declarations do not block supported siblings |
| OB-02 | Public object model | Prior drafts assume semantic values, descriptor classes, and explicit offsets | Supported C++ objects are exact sequential structs with interface inheritance and extension behavior | Legal OCCT names remain source-faithful |
| OB-03 | Generic templates | No public generic layout contract exists | Proved specializations share one generic struct; unknown specializations fail before native access | Closed native operations remain generated and exact-match |
| OB-04 | Lifetime | Prior drafts assume `Handle<out T>` and do not define non-transient RAII ownership | `Handle<T>` owns only a transient intrusive reference; trivial values need no disposal; eligible non-transient RAII structs implement `IOcctRaii`, and `Owned<T> where T : unmanaged, IOcctRaii` directly contains one in managed storage without a public owner hierarchy | Handle release and Owned destruction each occur exactly once |
| OB-05 | Consumption | Consumers need repository tooling and native installation | `TedToolkit.Occt.Windows` restores and runs with its matched Windows managed/native closure | Unsupported RIDs fail before native probing |
| OB-06 | Namespace and Runtime boundary | Package, assembly, namespace, and Runtime admission are not yet a complete contract | Package and assembly use `TedToolkit.Occt.Windows`; generated APIs retain the `TedToolkit.Occt` namespace; Runtime alone defines shared owners and Windows uses their ordinary public API | Other wrappers can use the same Runtime contract without Windows privilege |

<!-- acceptance-case: AC-01 -->
### AC-01 — Every pinned declaration has one truthful disposition

```gherkin
Scenario: Inventory the pinned OCCT headers
  Given the approved OCCT version, triplet, and header universe
  When release generation runs
  Then every canonical declaration is generated, unsupported, or excluded exactly once
  And every unsupported reason identifies the missing layout, ownership, operation, or language rule
```

<!-- acceptance-case: AC-02 -->
### AC-02 — The package exposes the accepted object and operation model

```gherkin
Scenario: Inspect the generated public surface and call representative operations
  Given trivial, transient, inherited, generic, and non-transient RAII declarations are supported
  When a consumer builds and calls the public API
  Then each C++ object has an exact unmanaged sequential struct and no FieldOffset or BaseType
  And interfaces express inheritance
  And instance behavior uses extension syntax without public Value extraction
  And every shipped object and registered closed generic passes its native and managed layout contract
  And representative operations pass real native behavior and lifecycle journeys
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Ownership categories release correctly

```gherkin
Scenario: Construct, copy, call, and release representative objects
  Given one trivial value, one Standard_Transient target, and one non-transient RAII target
  When the consumer exercises their generated APIs
  Then the value copies without disposal
  And Handle<T> aliases and releases one intrusive reference exactly once
  And the non-transient RAII target implements IOcctRaii while the trivial and Standard_Transient targets do not
  And Owned<T> placement-constructs in managed storage, explicitly clones when requested, and destructs through the matching library without intrusive Release or native storage free
```

<!-- acceptance-case: AC-04 -->
### AC-04 — The Windows package restores and runs in isolation

```gherkin
Scenario: Consume the packed artifact from a clean supported application
  Given only an isolated feed, clean NuGet caches, and a win-x64 net8.0 consumer
  When the consumer references TedToolkit.Occt.Windows, restores, builds, and runs representative generated operations
  Then native loading resolves only packaged assets
  And the exact fingerprint matches before any OCCT operation resolves
  And every required operation and cleanup export resolves into one complete generated static managed function table before it is published
  And generated calls use exact typed table slots while Handle<T> and Owned<T> retain their matching cleanup pointers
  And the package ID and managed assembly name are TedToolkit.Occt.Windows
  And the public generated API uses the TedToolkit.Occt root namespace
  And Runtime contains no declaration-specific layout, import, symbol, specialization, or expected fingerprint
  And Windows contains no generated Runtime source and has no InternalsVisibleTo or private-access privilege
  And no development-time tool or external OCCT installation is required
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Package support and failure claims are truthful

```gherkin
Scenario: Inspect the package and try an unsupported RID
  Given the packed candidate and generated coverage report
  When package assets, dependencies, licenses, documentation, and RID behavior are inspected
  Then every managed and native runtime dependency and required notice is present exactly once
  And documentation distinguishes the Windows package family from the pinned win-x64 layout matrix and coverage limits
  And no unsuffixed TedToolkit.Occt binding package or assembly is shipped
  And an unsupported RID fails before native library probing
```

## Constraints and risks

- [Repository design principles](../../principles/README.md) and the
  [generated binding architecture](../../architecture/generated-binding-system.md) govern the
  package.
- The accepted [ADR-001 native loader and function-table decision](../../adr/ADR-001-native-release-binding/README.md),
  pinned by the generator change to its approved content, is a supplied Generator contract rather
  than package-owned behavior.
- The package cannot build a candidate until the Handle, Owned, and generator changes supply
  verified inputs. Its exact native build identity must be approved before the revised package and
  map can be approved.
- Public managed compatibility begins with the first package; native and managed internals remain
  an inseparable exact-match set.
- `TedToolkit.Occt.Windows` is a reference wrapper built against Runtime's ordinary public owner and
  exception contracts while owning its generated Windows loader and function table. It receives no
  Runtime access unavailable to an independently named wrapper.
- `TedToolkit.Occt.Windows` does not imply support for every Windows architecture. The initial and
  only approved matrix is `win-x64`; adding another Windows RID requires separate complete layout
  proof and renewed architecture review.
- Layout, closed generic, native dependency, licensing, and module-origin evidence are release
  blockers, not documentation caveats.
- Escalate if another platform, independently upgradeable native ABI, public raw pointer, unproved
  layout, ownership in a struct, or remote publication enters scope.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../generate-unversioned-model-driven-bindings/change.md contract=AC-06 -->

| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | The generated exact-layout managed/native replacement builds and runs against real pinned OCCT without an active ABI-v1 path, with every resource cleaned up by its producing library | `../generate-unversioned-model-driven-bindings/change.md`, AC-06 | Source contract is Completed on the exact Git baseline selected for package implementation |

<!-- section: delivery-brief -->
## Delivery disposition

The three-item map remains appropriate with narrower ownership: PKG-001 owns complete coverage
inventory; PKG-002 materializes and verifies the full `TedToolkit.Occt.Windows` managed/native
candidate by consuming completed Generator and Runtime contracts without modifying them; and
PKG-003 owns package composition and isolated consumption. All rows remain Draft until the revised
parent and map are explicitly approved.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=component -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-03 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-04 purpose=journey shape=end-to-end -->
<!-- primary-proof: AC-05 purpose=boundary shape=contract -->

| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Every pinned declaration has one deterministic generated, unsupported, or excluded disposition, and unsupported cases identify the missing proof | Run Generator coverage cases against the pinned installed OCCT headers |
| AC-02 | Primary | Exhaustive layout and public API contracts prove every shipped object, generic, interface, receiver, namespace, and Runtime boundary | Build the full generated candidate and run its exhaustive layout and public API contract matrix |
| AC-03 | Primary | Representative real native journeys prove value, Handle, and Owned construction, copying, invocation, and same-library cleanup exactly once | Run the Generator and Runtime TUnit integration journeys against the full native candidate |
| AC-04 | Primary | A clean `net8.0` win-x64 consumer uses only packaged assets, passes exact match, publishes one complete function table, invokes typed slots, and retains matching owner cleanup pointers | Pack to an isolated feed and run a clean `net8.0` win-x64 consumer |
| AC-05 | Primary | Package identity, native dependency closure, notices, support claims, module origin, and unsupported-RID failure match the approved boundary | Inspect the nupkg and run dependency, license, and RID contract fixtures |

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-05 pass; `TedToolkit.Occt.Windows` is the only public generated binding package and
assembly; it retains the `TedToolkit.Occt` namespace and contains the separate approved Handle and Owned models and
exact-layout API baseline for `win-x64`; every shipped closed generic, operation, layout, exception,
and cleanup path has real boundary evidence; isolated supported consumption needs no developer
tools; native and license closure is complete; unsupported RIDs and declarations fail truthfully;
current documentation matches generated coverage; and publication remains a separate explicit
action.
