# Establish an explicit and verifiable C interoperability ABI

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: approved -->

- Priority: P1
- Approval: User approval in the current Codex task on 2026-08-24 for content SHA-256
  `3617B1A181AE56E4D2611C3CCD7FBE85C6973D2D52E5295E79938F53EB34300E`.

<!-- section: goal-rationale -->
## Goal and rationale

The generator must translate OCCT C++ semantics into an explicit, restricted, and verifiable C
transport contract before managed invocation is completed. Today `TypeModel.CppTypeName` is both a
source C++ spelling and an exported wrapper type, so `extern "C"` declarations can still expose C++
references, templates, STL records, and `opencascade::handle<T>`. C linkage controls symbol linkage;
it does not make those representations safe for P/Invoke or a C caller.

<!-- section: scope -->
## Scope and non-goals

- In scope: separate source C++, ABI transport, C++ adaptation, managed transport, and public
  managed projections; generate the ABI-major-1 header and native library; implement the approved
  scalar, semantic value, UTF-8 buffer, transient handle, error, and lifetime contracts; reject a
  member before export when any mapping is incomplete.
- Initial conformance slice: `gp_Pnt2d` as a semantic value, `Geom2d_CartesianPoint` as a
  `Standard_Transient` handle, and `TCollection_AsciiString` as a UTF-8 value. Representative
  operations must construct and observe a point, construct/get/set/retain/release a transient
  Cartesian point, and round-trip UTF-8 through library-owned output storage.
- Frozen transports for this slice: `ted_occt_v1_pnt2d` contains `double x` followed by `double y`
  with no additional validity rule; Cartesian points use
  `typedef struct ted_occt_v1_geom2d_cartesian_point ted_occt_v1_geom2d_cartesian_point` and pointers
  to that incomplete type; UTF-8 uses ADR-0001's byte view and owned-byte carriers.
- Non-goals: final generated `LibraryImport`/`DllImport` declarations or public managed method
  bodies, complete managed lifetime types, every `Geom2d_BSplineCurve` dependency, callbacks,
  nested/non-contiguous collections, other triplets, or the complete OCCT type system.
- Preserved behavior: the approved-base revision's public C# names and selected member set for the
  conformance roots, observable OCCT results, and available native diagnostics remain available.
  Prototype native signatures and names are not a released compatibility baseline.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Type projection | Raw C++ spelling can become the export type | Every projection layer is explicit and non-fallback | Public managed and transport types may differ |
| OB-02 | Canonical C surface | Prototype symbols can contain C++ ABI types and derive names from C# projections | `ted_toolkit_occt_v1.h` contains only approved C11 types and stable ABI-identity symbols for `ted_toolkit_occt_abi_v1` | Exports use C linkage and cdecl |
| OB-03 | Unsupported members | Recursive discovery can emit STL/compiler wrappers or partial signatures | The member is omitted and one stable diagnostic identifies the declaration and missing mapping | Other supported members retain approved selection |
| OB-04 | Adaptation and lifetime | ABI values may be passed directly and transient instances can be deleted incorrectly | C++ adapters validate, convert, retain, release, and commit outputs according to ADR-0001 | OCCT APIs are unchanged |
| OB-05 | Failure outcome | Null type-name state implies success and concrete C++ types drive mapping | One fixed-width error kind is authoritative; optional diagnostics remain library-owned | No last-error state or separate status is introduced |

<!-- acceptance-case: AC-01 -->
### AC-01 — The generated ABI is canonical C11

```gherkin
Scenario: Generate ABI-major-1 declarations twice from the same semantic model
  Given every receiver, parameter, result, and failure path has an approved ABI mapping
  When the canonical header and wrappers are generated in different input orders
  Then both runs produce the same globally unique operation symbols
  And the header compiles as C11 and C++ without OCCT or C++ standard-library headers
  And no symbol identity depends on a C# projection name or raw C++ spelling
```

<!-- acceptance-case: AC-02 -->
### AC-02 — The conformance slice crosses only approved transports

```gherkin
Scenario: Generate representative point, Cartesian-point, and UTF-8 operations
  Given the approved gp_Pnt2d, Geom2d_CartesianPoint, and TCollection_AsciiString mappings
  When their canonical declarations and C++ adapters are generated
  Then the declarations contain only the frozen semantic value, typed opaque handle, byte carriers, scalars, and error value
  And C++ references, OCCT handles, and TCollection types appear only inside adapter implementation
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Unsupported types fail closed

```gherkin
Scenario: Analyze a member containing an unmapped STL, compiler-internal, or ownership-ambiguous type
  Given no complete mapping exists for that type and direction
  When exportability is validated
  Then no declaration or wrapper body is emitted for that member
  And one deterministic diagnostic reports the declaration, source location, source type, direction, ownership, and missing rule
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Projection layers cannot fall back

```gherkin
Scenario: A type rule supplies only source or public-managed information
  Given an interop signature requires the incomplete rule
  When the signature contract is validated
  Then generation rejects the member before naming or emission
  And it never substitutes source C++ or public C# spelling as a transport type
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Established managed projection intent remains stable

```gherkin
Scenario: Regenerate existing projection fixtures and the configured development target after model separation
  Given the approved base revision's Generator semantic assertions and Geom2d_BSplineCurve declaration selection
  When the same fixture models and target declaration are processed
  Then their public C# names and selected member identities match the approved-base baseline
  And only the explicitly approved cross-language transport behavior changes
```

<!-- acceptance-case: AC-06 -->
### AC-06 — A plain C consumer observes OCCT behavior and safe ownership

```gherkin
Scenario: A C11 consumer calls the generated conformance slice
  Given the x64-windows OCCT 8.0.1 shared library and canonical header
  When it invokes point, transient-handle, UTF-8, success, and failure paths
  Then successful results match the corresponding OCCT operations
  And retain and release preserve intrusive ownership without directly deleting a live transient target
  And every owned error or byte buffer is released by its authoritative owner slot
  And allocation failure preserves a nonzero authoritative error kind without terminating the process
```

<!-- acceptance-case: AC-07 -->
### AC-07 — A minimal P/Invoke consumer agrees with the native ABI

```gherkin
Scenario: A managed boundary fixture loads ABI major 1
  Given blittable sequential declarations for the version-1 error, point, and buffer carriers
  When it verifies the reported ABI major and invokes representative exports using cdecl
  Then native and managed sizes and field offsets agree
  And success, unknown/reserved failure, diagnostic pointers, and owner-slot cleanup match the C consumer
  And an incompatible ABI major is rejected before any operation export is invoked
```

## Architecture constraints and risks

- Governing decision: [ADR-0001](../../adr/ADR-0001-stable-c-interop-abi.md) is Accepted at commit
  `49fb72c4010505a409b03ea25433136fd2fe306c`. That revision's version boundary, symbol identity,
  transport, ownership, error, buffer, and compatibility rules govern this delivery.
- The first supported matrix is Windows x64, the MSVC x64 ABI, cdecl, and OCCT 8.0.1 from vcpkg
  triplet `x64-windows`. Portable types do not imply support for another triplet.
- `Standard_Transient` lifetime uses intrusive retain/release. Direct deletion of a live transient
  target, stale copied ownership tokens, and escaping borrowed results are invalid.
- A real C consumer and a minimal managed boundary consumer are required. Text snapshots and C++
  compilation under `extern "C"` are insufficient.
- ABI version 1 is not released until all proof passes. After release, changing a symbol, layout,
  error-kind value, ownership rule, encoding, or release obligation requires a new ABI major.
- Escalate to architecture design for another triplet, callback, cross-process boundary, public
  allocator, unsupported resource, or evidence that a transient result cannot be promoted safely.

<!-- section: delivery-brief -->
## Delivery brief

- Delivery disposition: one Controlled delivery. If implementation evidence requires independently
  releasable model, adapter-family, or consumer-boundary deliveries, stop and invoke
  `plan-work-items` rather than hiding additional boundaries here.
- Outcome and target area: type resolution and projection models, ABI identity, canonical C
  declaration generation, C++ adapters, shared native support, and boundary fixtures enforce one
  accepted ABI-major-1 contract.
- Start conditions: ADR-0001 commit `49fb72c4010505a409b03ea25433136fd2fe306c`; .NET SDK 10;
  CMake 3.28 or newer; Ninja and
  `clang-cl` targeting the MSVC x64 ABI; `VCPKG_ROOT` containing `opencascade:x64-windows` 8.0.1.
  The current environment lacks `VCPKG_ROOT` and Ninja, so native proof cannot run yet.
- Supplied baseline: existing Generator semantic tests, native-boundary fixture patterns, and the
  configured `Geom2d_BSplineCurve` declaration selection at the approved base revision. The new canonical
  header, versioned library, CMake presets, and expanded boundary assertions are delivery outputs,
  not start prerequisites.
- Likely touchpoints (non-binding): type resolution/results, record/method/parameter models, C++
  and C header generation, embedded native support, Generator tests, native consumer fixtures,
  Runtime ABI carriers, and Generator/Runtime documentation.
- Private choices left open: internal type decomposition, rule registration, diagnostic object
  shape, adapter organization, test-file layout, and whether the managed fixture remains in the
  existing Generator test project or gains a project for materially different execution needs.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance, contract, structural | Contract plus C11/C++ compile | Order-independent generation yields identical valid C11 declarations and ABI-identity symbols | Generator test command below, then CMake consumer configure/build |
| AC-02 | Acceptance, boundary | Contract | Frozen value, handle, and UTF-8 transports appear in the header; C++ types remain in adapters | Generator test command below |
| AC-03 | Acceptance, regression | Component | Unsupported members emit no partial artifact and one complete deterministic diagnostic | Generator test command below |
| AC-04 | Architecture, regression | Unit | Incomplete projection rules cannot reach naming or emission | Generator test command below |
| AC-05 | Regression, compatibility | Contract | Semantic public-name and member-identity assertions match the approved base | Generator test command below |
| AC-06 | Acceptance, boundary, regression | Integration with real C consumer | C11 calls prove results, retain/release, error precedence, faulted diagnostics, and same-library cleanup | CMake/CTest commands below |
| AC-07 | Acceptance, boundary, regression | Integration with minimal P/Invoke consumer | Native layout queries match managed layout; version check, by-value errors, reserved failures, and cleanup agree | Generator test command below against the AC-06 library |

```powershell
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx
cmake --preset ted-occt-abi-v1-consumer
cmake --build --preset ted-occt-abi-v1-consumer
ctest --preset ted-occt-abi-v1-consumer --output-on-failure
dotnet restore TedToolkit.Occt.slnx
dotnet build TedToolkit.Occt.slnx -c Release --no-restore
```

The C consumer must exercise every frozen error kind, documented derived-before-base collision,
catch-all value 255, a distinct unrecognized reserved value, idempotent authoritative-slot cleanup,
and deterministic failure of each diagnostic allocation point. Cross-compiler or additional-triplet
proof is required only if the approved scope expands.

<!-- section: completion-criteria -->
## Completion

ADR-0001 is Accepted and implemented; AC-01 through AC-07 pass on the initial supported matrix; the
canonical header and versioned library are called by real C and P/Invoke consumers; unsupported
members fail closed; no unapproved C++ representation appears in an export; conformance-root public
projection intent remains stable; and Generator and Runtime documentation describe the supported
matrix, ownership rules, version check, and rejection behavior. Final managed invocation remains a
later change.
