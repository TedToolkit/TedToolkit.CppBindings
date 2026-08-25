# Deliver a ready-to-use generated OCCT binding package

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: approved -->

- Priority: P1
- Approval: User approval in the current Codex task on 2026-08-25 for the revised public handle,
  managed exception, delivery, and proof contracts recorded here.

<!-- section: goal-rationale -->
## Goal and rationale

A .NET consumer can install `TedToolkit.Occt` and call the safely projected OCCT surface on the
verified Windows x64 matrix without installing vcpkg, OCCT, Clang, CMake, or the Generator. The
repository currently exposes development-time Generator and Runtime components, but it has no
consumable binding package and its generated managed invocation layer is incomplete. Delivering the
package now turns the accepted ABI and package architecture into the first usable product boundary.

<!-- section: scope -->
## Scope and non-goals

- In scope: a public `TedToolkit.Occt` assembly and NuGet package; release-time enumeration of every
  public OCCT `.hxx` header; deterministic generated/unsupported/excluded dispositions; callable
  managed projections for fully mapped operations; source-faithful managed type names; explicit
  ownership through one handwritten covariant Runtime `Handle<out T>` contract and the only supported internal
  reference-type owner for `Standard_Transient` targets; generated target-specific lifetime glue;
  idiomatic managed exceptions with preserved native diagnostics; the ABI-major-1 native adapter;
  complete `win-x64` native runtime assets; a clean package-consumer proof; coverage and support
  documentation.
- Release baseline: OCCT 8.0.1 from vcpkg builtin baseline
  `f89a4a1da4e3176a8d1a14c1825b9b2f98e48843` and `x64-windows` triplet, the Windows x64 MSVC ABI,
  cdecl, SDK-style .NET 8 or newer, and ABI major 1.
- Unsupported by default: STL and compiler implementation types, complex or non-contiguous
  containers, callbacks, and declarations whose direction, nullability, conversion, or lifetime
  cannot be proved. They receive explicit dispositions and do not become public wrappers.
- Non-goals: 100% callable OCCT member coverage, Linux delivery or support claims, another CPU
  architecture or RID, .NET Framework or `packages.config`, ABI major 2, consumer-time generation,
  loading an arbitrary system OCCT installation, publishing the package to a remote feed, or a
  public logging or telemetry integration.
- Preserved behavior: ABI-major-1 symbols, transports, ownership, errors, version validation, and
  same-library cleanup remain governed by ADR-0001. Generator and Runtime remain separate
  development/package boundaries and do not flow to consumers except for the Runtime dependency
  required by generated public code.
- Compatibility: unreleased prototype managed invocation shapes are not a compatibility baseline.
  The first package establishes the `Handle<T>` projection as public API. Once released, generated
  public API removal or incompatible change requires explicit compatibility handling, and an
  incompatible native transport requires a new ABI major.

### Coverage inventory boundary

- The header universe is every regular `*.hxx` file recursively below the pinned
  `installed/x64-windows/include/opencascade` root. A header key is its forward-slash relative path,
  compared ordinally; filesystem traversal order cannot change the inventory.
- The declaration universe contains named namespace-scope records, enums, aliases, functions,
  function templates, and variables, plus every named record member and nested type whose spelling
  location belongs to the header universe. Private/protected members are still inventoried and may
  receive an explicit non-public exclusion. Comments, macros, namespace containers, using
  directives, and other non-wrapper syntax are covered by their header row rather than counted as
  independent declaration candidates.
- A macro-generated declaration belongs to the header containing its expansion location. A
  declaration reached only through a transitive header outside the pinned OCCT root receives no
  independent coverage row, but any supported candidate that references it records it in that
  candidate's generated or unsupported reason.
- Redeclarations and forward declarations are deduplicated by canonical semantic identity. The
  definition is authoritative when it is in the header universe; otherwise the lowest ordinal
  owning header is authoritative and a forward-only declaration is unsupported. Explicit template
  specializations in the header universe are candidates; implicit compiler declarations and
  implicit template instantiations are not separate candidates.
- Every declaration key combines kind, fully qualified semantic name/signature and template
  arguments, and authoritative owning-header key. Keys and counts must be identical across header
  and AST traversal orders; no source location outside the pinned root or traversal index may enter
  identity.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Consumer package | No ready-to-use binding package exists | `TedToolkit.Occt` restores, builds, deploys, and runs in a clean supported consumer | Consumers use ordinary SDK-style `PackageReference` |
| OB-02 | Header coverage | Callers select named top-level headers and unselected headers have no result | Every pinned public `.hxx` and discovered declaration has a deterministic generated, unsupported, or explicitly excluded disposition | Unsupported surface does not block unrelated supported operations |
| OB-03 | Generated invocation | Generated C# expresses incomplete shapes without production imports and method bodies | Every public generated operation maps one-to-one through a managed import, canonical ABI identity/export, and compiled native adapter with defined error and lifetime behavior | Public API remains separate from ABI transport representation |
| OB-04 | Unsupported surface | Parse discovery can reach STL, compiler, collection, or lifetime-ambiguous types | The complete affected operation is omitted before naming/emission and its reason is reported | No raw C++ spelling or invented ownership crosses the ABI |
| OB-05 | Release input | Generation depends on a machine-level vcpkg installation | OCCT version, vcpkg baseline/triplet, ABI major, generator revision, and rules are pinned for the package | Regeneration from the same inputs is deterministic |
| OB-06 | Platform delivery | Windows has boundary proof but no consumer package; Linux is only a portable design intent | The package contains and loads its full `win-x64` native closure; unsupported RIDs fail clearly and Linux is not advertised | The managed API and ABI remain RID-neutral |
| OB-07 | Managed OCCT semantics | The Runtime has no production lifetime abstraction, and the conformance model flattens an owned `opencascade::handle<T>` to public `T` | Legal OCCT identifiers remain source-faithful; values remain values; every owned `Standard_Transient` result uses the single covariant `Handle<out T>` contract with a Runtime-provenance owner, deterministic disposal, internal ownership duplication, base-handle covariance, and safe borrowed invocation | ABI-major-1 ownership and same-library cleanup remain unchanged |

<!-- acceptance-case: AC-01 -->
### AC-01 — Every pinned public header has a disposition

```gherkin
Scenario: Generate the release coverage inventory
  Given the pinned OCCT 8.0.1 x64-windows public include root
  When the controlled release generation completes
  Then every public .hxx header and discovered declaration appears exactly once as generated, unsupported, or explicitly excluded
  And every unsupported or excluded result includes a stable reason
  And declaration keys and counts are unchanged when header and AST traversal order changes
  And no unaccounted header can produce a package
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Unsupported operations fail closed without shrinking supported coverage silently

```gherkin
Scenario: A declaration uses STL, a complex container, a callback, or an unproved lifetime
  Given one value in the operation lacks a complete approved cross-language mapping
  When exportability and public projection are evaluated
  Then no ABI declaration, adapter body, managed import, or public method is emitted for that operation
  And one deterministic disposition identifies the declaration, source location, source type, direction, ownership, and missing rule
  And unrelated fully mapped operations remain eligible
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Every generated operation has a complete callable chain

```gherkin
Scenario: Validate the complete generated operation manifest
  Given the candidate public API and canonical ABI operation manifests
  When every generated public operation is validated and the native library is compiled and linked
  Then exactly one managed import, canonical ABI identity/export, and native adapter implementation exists for that operation
  And the managed import and native export agree on calling convention, transport, direction, nullability, ownership, and error behavior
  And removing or changing any one layer fails validation before packaging

Scenario: Execute every enabled transport and ownership category
  Given representative generated operations for every transport, ownership, receiver, result, and error category enabled by the candidate
  When a managed consumer calls their generated public APIs
  Then observed results match the corresponding OCCT behavior
  And every nonzero ABI error kind returned by a fallible operation, including an unrecognized reserved value, is thrown directly as the ADR-0005 managed exception while preserving its exact OcctErrorKind value
  And every native-origin exception exposes IOcctException with copied optional native type, message, and stack diagnostics after native error cleanup
  And consumers can catch but cannot construct or derive the library-provided native-origin exception types
  And IOcctException remains an open diagnostic contract and does not authenticate native provenance
  And missing native messages and native argument metadata follow the deterministic ADR-0005 fallback contract
  And a real Standard_Failure exposes a nonempty bounded NativeStackTrace distinct from the managed Exception.StackTrace
  And every owned handle, diagnostic, and buffer is released through its allocating library
```

<!-- acceptance-case: AC-04 -->
### AC-04 — The Windows package is self-contained for consumers

```gherkin
Scenario: Run a clean win-x64 package consumer
  Given a net8.0 SDK-style application, an empty isolated NuGet cache, and a local feed containing only the candidate and its declared consumer dependencies
  And VCPKG_ROOT, native search paths, system OCCT installations, Clang, CMake, and Generator assets are unavailable to the consumer
  When the application restores, builds, publishes for win-x64, and invokes representative generated APIs in the sanitized environment
  Then NuGet selects the packaged native assets
  And the ABI-major-1 adapter and every required OCCT runtime dependency load from the package closure
  And the loaded module paths resolve only below the isolated publish output
  And the application completes the representative calls successfully
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Release generation is reproducible and reviewed

```gherkin
Scenario: Regenerate from the same pinned release inputs
  Given identical OCCT, vcpkg, triplet, ABI, generator, and rule revisions
  When the managed API, canonical header, native project, and coverage inventory are generated twice
  Then their generated UTF-8/LF contract artifacts and public API baseline are byte-identical
  And an unreviewed API, ABI, or coverage-baseline delta fails the release gate
```

<!-- acceptance-case: AC-06 -->
### AC-06 — Platform and coverage documentation is truthful

```gherkin
Scenario: Inspect the package README and metadata
  Given the first verified package candidate
  When a consumer reads its support and coverage documentation
  Then Windows x64, OCCT 8.0.1, and SDK-style .NET 8 or newer are the only claimed supported matrix
  And Linux is described as an unverified future target
  And full-header disposition is explicitly distinguished from full callable-member coverage
  And STL, complex-container, callback, and ownership-ambiguous exclusions are documented
```

<!-- acceptance-case: AC-07 -->
### AC-07 — Package composition contains only the required consumer and native closure

```gherkin
Scenario: Inspect and reconcile the candidate package
  Given the pinned vcpkg installed dependency graph and the produced TedToolkit.Occt package
  When managed dependencies, package asset groups, recursive native imports, and license manifests are inspected
  Then Generator, analyzer, source-generation, and release-only build assets are absent from the consumer dependency graph
  And every non-system native import is supplied by the win-x64 package closure
  And every packaged native dependency maps to its recorded source package, version, license, and notice artifact
  And removing a required native library or notice, or adding a Generator dependency, fails the package gate
```

<!-- acceptance-case: AC-08 -->
### AC-08 — Unsupported RIDs fail before native probing

```gherkin
Scenario: Resolve native bindings for an unverified RID
  Given a runtime identifier other than the verified win-x64 matrix
  When a consumer first invokes a generated native operation
  Then PlatformNotSupportedException identifies win-x64 as the supported RID
  And no package-external or system OCCT native location is probed or loaded
```

<!-- acceptance-case: AC-09 -->
### AC-09 — Managed types preserve OCCT value and handle semantics

```gherkin
Scenario: Generate representative value and transient projections
  Given gp_Pnt2d is an approved semantic value and Geom2d_CartesianPoint derives from Standard_Transient
  When their supported constructors, results, parameters, and methods are generated
  Then the public value type is named gp_Pnt2d
  And each owned transient result is Handle<Geom2d_CartesianPoint>
  And Handle<T> accepts only generated nonconstructible Standard_Transient target descriptors
  And Handle<out T> has exactly one handwritten Runtime interface definition and one supported internal reference-type Runtime owner
  And generated transient target descriptors use source-faithful class inheritance without duplicate I-prefixed type interfaces
  And a transient type with multiple direct bases is rejected until an explicit projection rule exists
  And generated output contains no Handle<T> definition or public lowercase borrowed handle type
  And instance-like operations are callable on that handle without exposing a raw pointer, IntPtr, ref target, or copyable owning struct
  And an identifier requiring unapproved mangling or colliding with another public projection is rejected with a deterministic disposition

Scenario: Pass a derived owner to a base-handle parameter
  Given Handle<Geom2d_CartesianPoint> owns one native token and Geom2d_CartesianPoint derives from Geom2d_Point
  When a generated operation accepting Handle<Geom2d_Point> is called with that derived handle
  Then covariance passes the same Runtime owner without boxing, allocation, native retain, or ownership transfer
  And the operation leases the owner for the call and does not dispose it

Scenario: Reject a consumer-defined handle implementation
  Given a consumer implementation of Handle<Geom2d_Point>
  When it is passed to a generated operation
  Then ArgumentException identifies the invalid handle argument
  And no invocation lease is acquired and no native location is probed

Scenario: Alias, borrow, and dispose a generated transient handle
  Given a managed consumer owns a generated Handle<Geom2d_CartesianPoint>
  When it aliases the handle, invokes a borrowed operation, and disposes the owner through either alias
  Then the alias shares the original disposed state and adds no native ownership token
  And no public Retain or ownership-duplication method is generated
  And a call that leases before disposal may complete while one starting after disposal fails before native invocation
  And Dispose returns without waiting for an existing lease and native release occurs after the final lease ends
  And the ownership token is released exactly once through its ABI-major-1 library
  And release of the Runtime-supplied valid owner slot has a native noexcept contract, returns no ABI error, and cannot make Dispose throw
  And Dispose, deferred release, and finalization invoke no consumer callback and do not depend on logging or telemetry
  And using either alias after disposal throws ObjectDisposedException before native invocation
  And lifetime leasing makes no claim that concurrent OCCT operations are thread-safe

Scenario: Finalize an undisposed generated transient handle
  Given the last managed reference to an undisposed handle becomes unreachable
  When finalization and collection complete
  Then no finalizer exception escapes
  And its ownership token is released exactly once after every invocation lease
  And the allocating ABI module remains loaded through that release

Scenario: Checked-downcast a generated transient handle
  Given a generated Handle<TBase> and an approved native checked downcast
  When the checked downcast succeeds
  Then generated interop acquires any required native reference internally
  And the returned Handle<TTarget> owns one independent token and must be disposed independently
  And no conversion reinterprets a managed or native pointer
  And a failed checked downcast follows the mapped OCCT null or error contract
```

## Governing constraints and risks

- [ADR-0001](../../adr/ADR-0001-stable-c-interop-abi.md) at
  `49fb72c4010505a409b03ea25433136fd2fe306c` governs canonical ABI-major-1 transports, naming,
  versioning, errors, ownership, rejection, and compatibility except for the artifact basename
  superseded by ADR-0002.
- [ADR-0002](../../adr/ADR-0002-separate-abi-identity-from-library-basename.md) at
  `aa505395e44d479fafb0c2535ae4f6488bca4f70` governs the native artifact basename. The official
  package ships and loads the default `ted_toolkit_occt` basename; the managed loader consumes the
  generated/package-selected basename and validates the ABI bootstrap export rather than inferring
  ABI identity from the filename.
- [ADR-0003](../../adr/ADR-0003-distribute-generated-occt-bindings.md) at
  `34175b30221936a32f60586f3c935b9a356bf875` governs release-time generation, full-header
  disposition, dependency direction, RID-native packaging, and Windows-first support claims.
- [ADR-0004](../../adr/ADR-0004-preserve-occt-managed-type-and-handle-semantics.md) at
  `f348fe0eeb8a0448206d46fbebb3e1090d19172f` governs source-faithful public names and managed
  `Handle<T>` semantics.
- [ADR-0005](../../adr/ADR-0005-project-native-failures-into-managed-exceptions.md) at
  `f348fe0eeb8a0448206d46fbebb3e1090d19172f` governs the public managed error enum, exception
  inheritance, native diagnostic surface, and error-owner cleanup boundary.
- The selected vcpkg builtin baseline is
  [`f89a4a1da4e3176a8d1a14c1825b9b2f98e48843`](https://github.com/microsoft/vcpkg/commit/f89a4a1da4e3176a8d1a14c1825b9b2f98e48843),
  whose registry baseline records `opencascade` 8.0.1 port-version 0. The local execution input is
  `C:\vcpkg` at checkout `127402f1c75bb3d5ff6bce04b285faa4930a5aca`, whose history contains the
  pinned baseline and whose installed `x64-windows` graph reports OCCT 8.0.1 with the expected
  public include root. Execution sets `VCPKG_ROOT` to that root while the manifest baseline, not the
  later host checkout revision, remains the release identity. Exact header count, coverage, native
  dependency closure, package size, and generation resource use remain delivery measurements.
- The package must carry applicable OCCT and transitive native dependency license notices. Static
  linking, native-module trimming, or omission of dynamic dependencies requires license and runtime
  closure evidence, not a package-size assumption.
- Full-header parsing may exceed practical memory or build time. Optimization may change private
  batching or caching, but it cannot skip headers, make results order-dependent, or weaken the
  complete-disposition gate.
- Unsupported surface is an accepted limitation, but silent loss of a previously generated public
  member after release is not. Coverage and public API baselines must expose such changes.
- Source-method `noexcept` is not a generation or compatibility input: callable operations retain
  one uniform ABI error and native-exception translation boundary. Generated lifetime release uses
  its separate `noexcept` contract from AC-09.
- Recovery before remote publication is deletion of the local package candidate and regeneration
  from the pinned inputs. Remote publication, release migration, and consumer upgrade policy require
  separate authorization and are outside this change.
- Escalate for an accepted ABI naming change, ABI major 2, Linux or another RID support, a required
  STL/callback/complex-container mapping, a new ownership category, consumer-time generation,
  static-linking policy, replacement of the explicit `Handle<T>` public ownership model, or evidence
  that the delivery cannot be separated into independently verifiable work items.

<!-- section: delivery-brief -->
## Delivery disposition

This Controlled change requires multiple independently verifiable deliveries: reproducible complete
header inventory and coverage policy; callable generated managed/native bindings including the
public handle contract; and the self-contained Windows package and clean-consumer boundary. This
approved revision and its five pinned ADRs authorize `plan-work-items` to replace the existing Draft map pinned to commit
`992a60261bd449f312bd02c401539c3b4d6479a9` with the smallest dependency map assigning one final
owner to every acceptance case. No implementation is authorized until that replacement map
receives separate approval.

Likely delivery areas are the Generator pipeline and models, generated managed imports/public API,
Runtime integration, native adapter materialization, build/package configuration, coverage/public
API baselines, clean consumer fixtures, and package/README documentation. These are non-binding;
private types, algorithms, generated-file retention, batching, cache design, test organization, and
edit order remain open to implementers.

Real start conditions are the five pinned accepted ADRs, .NET SDK 10, CMake 3.28 or newer, Ninja,
`clang-cl` targeting the MSVC x64 ABI, and a vcpkg checkout containing baseline
`f89a4a1da4e3176a8d1a14c1825b9b2f98e48843` resolving OCCT 8.0.1 port-version 0 for `x64-windows`.
The verified local host supplies .NET SDK 10.0.400, CMake 4.4.2, clang-cl 22.1.3, Visual Studio's
bundled Ninja, and `C:\vcpkg`; the work-item plan records these paths and how package output is
supplied to clean-consumer proof.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance, structural | Component over real pinned headers | Coverage inventory accounts for every in-boundary canonical header/declaration exactly once and rejects gaps | Run the planned Generator coverage command against the pinned baseline in normal and reversed/randomized traversal orders; require identical keys/counts and zero unaccounted headers |
| AC-02 | Acceptance, regression | Component | Unsupported operation produces no partial artifacts and one complete stable disposition while a supported sibling remains | Generator TUnit command, including explicit unsupported-type and mixed-surface fixtures |
| AC-03 | Acceptance, compatibility, boundary, regression | Contract plus Integration through public managed API | One-to-one operation manifest proves every callable chain; public API and table-driven Runtime proof fix every ABI v1 and reserved error mapping, construction/fallback contract, and cleanup; real native calls cover every enabled transport/ownership/error category and expose a distinct OCCT stack | Run the planned operation-manifest validator with missing/mismatched-layer fault fixtures; run public API baseline plus Runtime mapping/fallback/diagnostic-allocation/clear proof; compile/link the full candidate; then run existing CMake/CTest and generated managed-consumer commands against the same library |
| AC-04 | Acceptance, journey | End-to-end isolated local package consumer | Minimum net8.0 restore/build/publish/run loads only candidate package assets with no development or system-native fallback | Use a fresh temporary NuGet cache and isolated local feed, sanitize environment/native paths, publish `win-x64`, assert loaded module origins, and run the consumer executable |
| AC-05 | Acceptance, compatibility | Contract and structural | Two clean generations match all UTF-8/LF generated contract artifacts, coverage inventory, canonical header, and public API baseline byte-for-byte | Run the planned release-generation command twice in separate output roots, byte-compare approved text artifacts, then execute the public API compatibility gate; raw differences are retained on failure |
| AC-06 | Acceptance, documentation | Structural review | README and package metadata contain only the proved matrix and required exclusion language | Pack candidate, inspect embedded README/metadata, and run repository documentation/link checks when available |
| AC-07 | Acceptance, license, structural | Package contract | Managed/package assets contain no release tooling; recursive native dependency and license manifests reconcile completely | Inspect the `.nupkg`, resolved consumer graph, vcpkg installed graph, recursive PE imports, and notice inventory; fault fixtures remove one native DLL/notice and add one Generator dependency |
| AC-08 | Acceptance, regression | Component at native resolution boundary | Unsupported RID throws the documented exception and records zero native probes | Run the planned resolver test with an unsupported RID and probe recorder, and inspect the package RID asset graph |
| AC-09 | Acceptance, compatibility, boundary, regression | Contract plus Integration through public managed API | Public API baselines preserve legal OCCT names and class inheritance, reference the single handwritten covariant Runtime `Handle<out T>` and supported internal owner, reject generated/lowercase borrowed/duplicate I-prefixed handle definitions, multiple direct bases, unapproved collisions, and consumer-defined handle implementations, and expose no ownership-duplication helper; real calls prove zero-boxing/allocation derived-to-base conversion, aliasing, internal checked-downcast ownership, foreign-handle rejection before native access, lease/dispose ordering, finalization, module liveness, noexcept successful release of every Runtime-supplied valid owner slot, exactly-once release, and use-after-dispose rejection | Inspect Runtime and two independent generated target outputs for exactly one public `Handle<out T>` contract and no public borrowed or duplicate inheritance interface; inspect the derived-to-base call path for reference-type covariance and verify zero allocation over repeated conversions; verify the native release contract and empty-error result for valid and already-empty Runtime owner slots; then execute create/call/alias/checked-downcast/foreign-handle/concurrent-dispose/finalize/release cases against the built ABI-major-1 library through the generated public API with observable token, native-probe, and module-lifetime hooks |

Existing affected gates remain:

```powershell
dotnet restore TedToolkit.Occt.slnx
dotnet build TedToolkit.Occt.slnx -c Release --no-restore
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release --no-build -- --report-trx
cmake --preset ted-occt-abi-v1-consumer
cmake --build --preset ted-occt-abi-v1-consumer
ctest --preset ted-occt-abi-v1-consumer --output-on-failure
```

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-09 pass on the pinned Windows x64 matrix; the produced `TedToolkit.Occt` package
works from an isolated consumer without the development toolchain or system OCCT; every public
header has a deterministic disposition; every shipped public operation has complete managed/native
invocation, error, and lifetime behavior; legal OCCT names and `Handle<T>` ownership are present in
the public baseline; native and license closure is present; unsupported RIDs fail before native
probing; unsupported surface and Linux status are documented accurately; all existing affected
gates pass; and no remote publication or unsupported-platform claim has occurred.
