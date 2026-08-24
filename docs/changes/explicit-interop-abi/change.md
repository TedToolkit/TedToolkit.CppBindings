# Establish an explicit and verifiable C interoperability ABI

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: draft -->

- Priority: P1
- Approval: None while Draft.

<!-- section: goal-rationale -->
## Goal and rationale

The generator must translate OCCT C++ semantics into an explicit, restricted, and verifiable C transport contract. `TypeModel.CppTypeName` currently represents both the native C++ spelling and the exported wrapper signature, so functions declared with `extern "C"` can still expose C++ references, STL types, and `opencascade::handle<T>`. C linkage fixes symbol linkage; it does not make those parameter representations safe for P/Invoke or a C caller. The ABI must become authoritative before managed invocation is completed.

<!-- section: scope -->
## Scope and non-goals

- In scope: separate the source C++ type, ABI transport, C++ adaptation, managed transport, and public managed projection; implement the approved ABI vocabulary; convert at the wrapper boundary; reject declarations without a safe mapping; prevent STL and compiler-internal records from becoming generated targets.
- Non-goals: generate final P/Invoke declarations or managed method bodies, finish every managed lifetime type, fix native CMake linking, or promise coverage of the complete OCCT type system.
- Preserved behavior: representative existing public C# names and member selection, OCCT operation results, and native error information remain available. Current prototype native signatures and their names are not a released compatibility baseline; establishing deterministic ABI-version-1 symbol names is explicitly in scope.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Type model | Raw C++ spelling is reused as the wrapper signature | Source type, ABI transport, C++ adaptation, managed transport, and public type have separate non-fallback responsibilities | Public managed types may differ from transport types |
| OB-02 | Exported declarations | C++ references, template instances, STL classes, and OCCT handle classes can appear | Only ADR-approved C scalars, semantic values, buffers, one error carrier, and opaque handles appear | Exports retain C linkage and the approved C calling convention |
| OB-03 | Unsupported types | The generator may recursively emit STL/compiler wrappers or unusable signatures | It rejects the member before export with a deterministic diagnostic containing declaration and type evidence | Other supported members retain the approved selection behavior |
| OB-04 | C++ invocation adaptation | ABI arguments may be passed directly into OCCT using compiler-specific representation | The wrapper explicitly constructs, borrows, retains, releases, reads, or writes OCCT values according to the ABI contract | OCCT APIs themselves are unchanged |
| OB-05 | Existing projection and semantics | The mixed model already produces public names, member selection, error details, and OCCT call results | Representative supported declarations retain those public names, selected members, error information, and OCCT results after model separation | Managed P/Invoke method bodies remain a later change |
| OB-06 | Exception outcome | Success is inferred from a null native type-name pointer, while concrete native type names drive managed exception mapping | Every operation returns one error value whose fixed-width kind is authoritative; concrete native type, message, and stack text remain optional diagnostics | No separate status value or process-global last-error state is introduced |

<!-- acceptance-case: AC-01 -->
### AC-01 — Supported types produce a pure C transport declaration

```gherkin
Scenario: Generate a wrapper for a supported OCCT operation
  Given the operation uses only approved scalars, semantic values, buffers, or opaque handles
  When its native wrapper and canonical ABI declaration are generated
  Then the export contains no C++ reference, template instance, or standard-library type
  And the adapter can invoke the original OCCT operation
```

<!-- acceptance-case: AC-02 -->
### AC-02 — References, handles, and buffers do not leak the C++ ABI

```gherkin
Scenario: An operation uses references, an OCCT handle, or string input and output
  Given each type has an ADR-approved transport and ownership rule
  When the wrapper is generated
  Then the export contains only the corresponding C transport values
  And the original C++ types appear only inside adapter implementation
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Unsupported types are rejected deterministically

```gherkin
Scenario: An operation uses an unmapped STL or compiler-internal type
  Given the type is outside the approved ABI vocabulary
  When the generator analyzes the operation
  Then it emits no export for that operation
  And it reports the declaration, source location, source type, and missing mapping information
```

<!-- acceptance-case: AC-04 -->
### AC-04 — An ABI projection cannot fall back to raw C++ spelling

```gherkin
Scenario: A new OCCT type rule is incomplete
  Given the rule defines only a source C++ type or public C# type
  When that type appears in an interop signature
  Then contract validation rejects the incomplete mapping
  And it does not reuse raw C++ spelling as an ABI type
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Public projection and member selection remain stable

```gherkin
Scenario: A representative supported declaration moves to the separated model
  Given its baseline public C# name and selected member set
  When the same declaration is generated with the ABI model
  Then its public C# name and selected member set match the baseline
  And only the cross-language transport changes as approved
```

<!-- acceptance-case: AC-06 -->
### AC-06 — A C consumer observes correct calls and failures

```gherkin
Scenario: A plain C fixture calls representative exports
  Given a shared library with approved scalar, semantic value, buffer or handle, and failure paths
  When the C fixture compiles, links, and invokes those symbols
  Then successful calls return the same observable OCCT results
  And every frozen error kind and each documented derived-before-base exception collision returns the approved stable error kind
  And failures preserve any available type and message diagnostics whose storage can be released by the library
  And a nonzero error kind remains observable when diagnostic text allocation is unavailable
  And catch-all kind 255 and an unrecognized reserved kind are both treated as failures
```

<!-- acceptance-case: AC-07 -->
### AC-07 — The by-value error result is valid through P/Invoke

```gherkin
Scenario: A minimal managed boundary fixture invokes the canonical ABI
  Given a blittable sequential managed declaration that mirrors ted_occt_error
  When it invokes representative success and failure exports using cdecl on each supported triplet
  Then its size and field offsets match the native C layout
  And it observes the same kinds and diagnostic pointers as the C fixture
  And it releases each owned error through ted_occt_error_clear using its authoritative owner slot
```

## Architecture constraints, alternatives, and risks

- Approval prerequisite: [ADR-0001](../../adr/ADR-0001-stable-c-interop-abi.md) must be Accepted and pinned to an approved revision. Its transport vocabulary, ownership, errors, buffers, versioning, and compatibility rules govern this delivery. The current Proposed status keeps this change blocked from approval.
- The selected direction is an explicit C transport ABI with C++ adapters. Continuing to place C++ types under `extern "C"` and switching to C++/CLI were considered and rejected in ADR-0001.
- A real C consumer must compile, link, and call representative generated exports. C++ compilation under `extern "C"` is structural evidence only.
- A minimal managed boundary fixture must independently prove the by-value `ted_occt_error` layout and calling convention on every initially supported triplet. This fixture proves ABI transport only and does not implement the final managed API surface.
- `Standard_Transient` ownership is governed by intrusive retain/release; direct deletion of a live transient target is prohibited.
- Once consumed, an ABI is difficult to reverse. Any change to released signatures, layouts, error-kind values, ownership, encodings, or compatibility requires a new approved ABI-major decision.

<!-- section: delivery-brief -->
## Delivery brief

- Delivery disposition: one Controlled delivery. If evidence shows that the core model and multiple adapter families require independently verifiable releases, invoke `plan-work-items` rather than hiding additional deliveries here.
- Outcome and target area: type resolution, projection models, canonical C declarations, and C++ wrapper generation enforce one accepted ABI contract.
- Prerequisites: trustworthy target parsing; Accepted ADR-0001 pinned to its approved revision; a Microsoft Testing Platform executable test project; a compatible C/C++ toolchain, triplet, and valid `VCPKG_ROOT` for native boundary proof.
- Likely touchpoints (non-binding): type models/results, Resolver and type rules, record/method/parameter models, C++ generation, Generator test configuration, contract/integration fixtures, and architecture documentation.
- Private choices left open: internal model decomposition, rule registration, diagnostic object shape, adapter organization, and test fixture layout.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance, contract, regression | Contract plus C++ compile | Approved types produce only the canonical C vocabulary and adapters invoke OCCT | `dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx`, then compile the generated native fixture |
| AC-02 | Contract and boundary | Contract | References, handles, strings, and buffers are restored to C++ types only inside adapters | Same Generator command and canonical-header contract assertions |
| AC-03 | Acceptance and regression | Component | Unsupported types produce no export and return a stable, locatable diagnostic | Same Generator command |
| AC-04 | Architecture and regression | Unit | A type rule missing any required ABI projection cannot enter wrapper generation | Same Generator command |
| AC-05 | Regression and compatibility | Contract | Representative public C# names, selected members, and public projections match a semantic baseline | Same Generator command using semantic assertions rather than full-file snapshots |
| AC-06 | Acceptance, boundary, regression | Integration with a real C consumer plus deterministic fault injection | A C11 fixture exercises every frozen kind, the documented derived/base precedence, catch-all `UNKNOWN` 255, a distinct unrecognized reserved kind, ownership transfer, stale-copy prohibition, and idempotent authoritative-slot clear. A test-only diagnostic allocator seam fails each allocation point and proves the original kind remains, all diagnostic pointers become null, acquired storage is released, and the process does not terminate. | `cmake --preset ted-occt-abi-v1-consumer`, `cmake --build --preset ted-occt-abi-v1-consumer`, then `ctest --preset ted-occt-abi-v1-consumer --output-on-failure` |
| AC-07 | Acceptance, boundary, regression | Integration with a minimal managed P/Invoke fixture | Fixture-only native layout queries are compared with `Marshal.SizeOf` and `Marshal.OffsetOf`; success and failure exports return the struct by value using cdecl, and every owned payload is cleared by owner-slot reference on every initially supported triplet | `dotnet run --project tests/TedToolkit.Occt.Runtime.Interop.Tests/TedToolkit.Occt.Runtime.Interop.Tests.csproj -c Release -- --report-trx` against the same native fixture used by AC-06 |

Conditional evidence: if Accepted ADR-0001 permits fixed structs, cross-module release, or cross-compiler consumption, add the applicable layout, allocator-boundary, and compatibility proof. Text snapshots cannot replace a real compiled boundary. This Draft remains blocked from approval while ADR-0001 is Proposed.

<!-- section: completion-criteria -->
## Completion

ADR-0001 is Accepted and its constraints are implemented; AC-01 through AC-07 pass; representative wrappers compile and are called by real C and minimal P/Invoke consumers; no unapproved C++ reference, template, STL type, or OCCT handle representation appears in an export; representative public projections, member selection, OCCT results, and error information remain correct; Generator and Runtime documentation describe the supported matrix and rejection behavior. Final managed API invocation remains outside this change.
