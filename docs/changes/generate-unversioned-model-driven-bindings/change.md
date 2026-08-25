# Generate unversioned bindings from one semantic model

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: approved -->

- Priority: P1
- Approval: User approval in the current Codex task on 2026-08-25 for independently reviewed Draft
  content SHA-256 `E1E0F502E8D8473D50946B5EF80C95823784F474EB7D4A285AD63938BA5BEACD`, removing the parallel
  `Abi`/Catalog model, retaining `RecordModel` as declaration authority, deriving only lightweight
  interop projections, revising the three-item map, and continuing MIG-001 implementation.

<!-- section: goal-rationale -->
## Goal and rationale

For every supported OCCT operation, the Generator produces one coherent, unversioned C/C++/C#
binding chain directly from `RecordModel`, `MethodModel`, and their resolved types. The current
native path selects a handwritten nine-operation catalog, while the interrupted replacement began
constructing a second `AbiOperationModel`/Catalog graph. Both approaches duplicate parsed
declaration identity and permit layers to drift while still compiling.

The removed pre-version `CppGenerator` proves that parsed record, method, and required-header
metadata can drive C++ includes and invocation bodies. Its raw C++ export signatures, macro-owned
error boundary, and record-level lifetime guesses are not safe to restore. The replacement keeps
that model-driven emitter responsibility while consuming only validated cross-language operation
semantics and emitting the approved C transport boundary.

<!-- section: scope -->
## Scope and non-goals

- In scope: `RecordModel`-centered semantic operation projection; generated C11 declarations, C++ adapters,
  native build description, managed transports/imports/public calls, operation manifest, exact
  contract fingerprint, deterministic unversioned naming, fail-closed dispositions, and migration
  of active source/build/tests/current documentation away from ABI-major-1 scaffolding.
- In scope: a `CppGenerator`-owned native adapter emission path driven by the configured OCCT header
  selection and its normalized supported operations, including deterministic required includes,
  source C++ receiver/argument/result conversion, invocation bodies, and cleanup/error adapters.
- In scope: removal of the handwritten conformance catalog, embedded handwritten operation adapter,
  handwritten final operation symbols, version bootstrap/range checks, and every active `v1`
  identifier or path.
- In scope: removal of the production `Abi` namespace/directory, `AbiOperationModel`, operation
  Catalog types, and every manual or parallel operation graph. Generic C transport mapping,
  validation, naming, fingerprint, error, and lifetime rules move beside the normal model/generator
  pipeline and derive a lightweight per-method interop projection without copying declaration data.
- In scope: exactly one generated operation-adapter `.cpp` for each canonical owning type that has
  at least one supported operation, plus explicitly non-type common support source when required;
  deterministic collision checking and native build enumeration are part of this layout contract.
- Non-goals: complete OCCT header inventory, increasing supported declaration categories, changing
  public managed ownership or exception semantics, packaging or publishing NuGet artifacts, Linux
  support, consumer-time generation, or preserving the unreleased ABI-major-1 prototype.
- Non-goal: restoring the historical `CppGenerator` verbatim or exposing its raw C++ references,
  templates, object layouts, exception types, or allocation guesses as the public native contract.
- Compatibility: no supported native or managed package has been released. The prototype v1 surface
  is removed without a transition alias. Future managed public API compatibility remains separate;
  native and managed artifacts form one exact-match generated set.
- Preserved behavior: public native declarations remain valid C11; C++/STL/compiler representations
  do not cross the boundary; ownership, direction, nullability, error translation, same-library
  cleanup, and complete-operation fail-closed rejection remain mandatory.
- Historical ADR text remains intact except for status/supersession metadata required when the new
  decision is accepted. Active user and maintainer documentation means the repository root README,
  component READMEs, and non-ADR/non-change guides; these describe only the replacement
  architecture. Temporary delivery records may retain migration rationale, but every dependent
  Draft is reconciled with the replacement before this change completes.

### RecordModel authority and interop projection

- The current `AbiV1ConformanceCatalog.CreateOperations()` and the interrupted replacement Catalog
  types are removed. No replacement production type may enumerate, copy, or independently identify
  OCCT operations.
- `RecordModel` owns one parsed record and its ordered `MethodModel` declarations. Resolver-produced
  `TypeModel` data remains attached to those declarations. A lightweight interop projection may add
  C transport, direction, nullability, ownership, conversion, error, cleanup, and lifetime decisions
  only by referencing the originating record/method/parameter/result; it may not copy their owner,
  operation kind, names, headers, or source identity into another domain graph.
- Each record is projected once after parsing. Every method receives exactly one generated,
  unsupported, or explicitly excluded result. Duplicate record/method identities, missing
  dispositions, or generated symbol/file collisions fail before materialization.
- All C, C++, C#, naming, fingerprint, manifest, and build emitters traverse the same ordered
  `RecordModel` collection and its attached projection results. No emitter rebuilds a global
  operation list or filters through a Catalog.
- Generic fingerprint, error, allocation, and cleanup support may be synthesized by category-level
  rules. Such rules cannot name an OCCT declaration or become a second declaration-specific
  catalog.
- The serialized operation manifest is a deterministic audit projection of the emitted records and
  methods, not another editable authority. Configured-header order, AST traversal order, and
  duplicate selection do not change identities, dispositions, or emitted artifacts.
- Tests must derive expected candidates from controlled parsed inputs and inspect the resulting
  dispositions, manifest, and emitted artifacts. Production projection cannot also serve as
  the test oracle for which operations should exist.

### Header-to-C++ generation boundary

- `DeclOptions` selects the root headers and records eligible for generation. Clang AST declarations
  and their canonical identities are the source; generated C++ is not assembled from a handwritten
  declaration list or by parsing header text templates.
- For every currently supported record-callable category, constructors, destructors, instance and
  static methods, overloads, operators, and conversions are enrolled only when selected and fully
  mapped. This change does not add a currently unsupported callable category, and namespace-level
  free functions remain outside scope.
- Transitive includes provide declarations and types needed to compile selected operations; they do
  not implicitly enroll every operation from dependency headers.
- Normalization produces a complete emission-oriented operation containing canonical source
  identity, owning type, required includes, C transport, C++ receiver/argument/result conversions,
  invocation expression, error translation, cleanup, and lifetime semantics. A declaration that
  cannot supply the complete plan receives a disposition and emits no layer.
- `CppGenerator` consumes one `RecordModel` and its validated method projections, as do the C and
  managed emitters, and emits
  definitions that exactly match the generated C declarations. Each body converts the C transport,
  invokes the real OCCT declaration, converts the result, and applies the common error/lifetime
  contract; it does not reconstruct an OCCT implementation.
- Inline declarations compile through their owning header. Non-inline declarations resolve through
  the pinned linked OCCT libraries. Every generated adapter remains traceable to source header,
  declaration location, canonical owner type, and generated source file.

### Model responsibility

The C ABI remains an emitted boundary, not a production domain model. These responsibilities are
required:

| Area | Required disposition |
| --- | --- |
| `RecordModel`, `MethodModel`, `ParameterModel`, and `TypeModel` | Remain the only declaration graph and expose only the additional resolved interop facts that every emitter actually needs. |
| `Abi/Conformance`, `Abi/Contracts`, Catalog and binding-set operation graphs | Remove rather than rename; no production `AbiOperationModel`, supported-operation wrapper, candidate Catalog, or handwritten operation list remains. |
| Direction, nullability, ownership, transport, conversion, error, and lifetime vocabulary | Keep as small generic interop values attached to or projected from existing declaration/type models; they contain no copied declaration identity and no ABI-major policy. |
| Validation and diagnostics | Validate each originating method's complete cross-language projection and fail closed when any required rule is missing or contradictory. |
| Naming, C-header, manifest, fingerprint, and build generation | Traverse the same deterministic record/method projections directly; no global Catalog is introduced. |
| `CppGenerator` | Accept one `RecordModel`, emit one matching type source, and derive includes and invocation syntax from its methods and resolved projections. |

The interop projection is deterministic and one-way, but it is not a second declaration model.
Emitters may not recover missing behavior from handwritten adapter bodies or independently
reinterpret Clang declarations.

### Per-type C++ source layout

- A canonical owning type with one or more supported operations produces exactly one adapter source.
  Constructors, destructor, instance/static methods, overloads, operators, conversions, and
  type-owned lifetime adapters for that type are defined in that file and in no other type file.
- The deterministic file stem is derived from canonical type identity through the common naming
  policy. All file identities are computed and collision-checked before any output is written; two
  distinct canonical types that map to one source path fail generation without partial output.
- A selected type with only unsupported or excluded operations produces dispositions but no empty
  `.cpp`. Input or AST order does not affect file names, contents, or build order.
- Fingerprint bootstrap and genuinely type-independent error/allocation/cleanup code may occupy one
  explicitly named common support translation unit. It may not contain an OCCT operation adapter or
  become a fallback bucket for operations without an owner type.
- The generated CMake source list contains every generated type source and common support source
  exactly once in ordinal path order. Shared declarations and category helpers belong in generated
  headers or private helpers rather than duplicating operation definitions across translation units.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Generation authority | A handwritten catalog selects production operations independently of the parsed model | The parsed normalized semantic model is the only operation authority consumed by every emitter | Unsupported operations remain isolated from supported siblings |
| OB-02 | Cross-language output | The C header, handwritten C++ adapter, and incomplete managed path have separate operation knowledge | Each supported operation has exactly one compatible C declaration, C++ adapter, managed import/public call, and manifest row generated together | C11 transport and managed/native separation remain |
| OB-03 | Native identity | Header, symbols, macros, directories, commands, and bootstrap state are decorated with ABI v1 | Active artifacts are unversioned and final names are derived by one deterministic naming policy | Names remain deterministic and collision-checked |
| OB-04 | Mismatch detection | Managed proof checks an ABI major before resolving operation exports | Generated managed and native artifacts require the exact same deterministic contract fingerprint | Mismatch fails before operation resolution or invocation |
| OB-05 | Unsupported mapping | Incomplete mapping can be represented, but the production conformance input is already fully handwritten | Any incomplete generated operation receives one deterministic disposition and emits no layer | No C++ spelling or invented ownership is used as fallback |
| OB-06 | Configured native surface | `DeclOptions` drives parsing and C# shapes while native materialization always copies the same adapter | The configured OCCT headers and their normalized supported operations determine the generated C++ includes and adapter implementations | Native output remains one shared library with deterministic build metadata |
| OB-07 | Declaration authority | `AbiV1ConformanceCatalog` or a replacement operation Catalog can independently enumerate and identify operations already represented by `RecordModel` | The configured deterministic `RecordModel`/`MethodModel` graph is the only declaration authority traversed directly by every emitter and the manifest | Deterministic generation, inspection, fingerprinting, and dispositions remain available without a Catalog |
| OB-08 | Interop projection | ABI classes duplicate owner, operation, parameter, result, kind, and source identity while C++ adapter behavior lives elsewhere | Small generic interop values reference existing declaration/type models and add only C transport, conversion, ownership, error, cleanup, and lifetime decisions | C11 transport, direction, nullability, ownership, and fail-closed validation remain |
| OB-09 | Native source granularity | The current adapter is handwritten as one fixed source; the removed generator wrote one source per record | Each canonical owner type with supported operations generates exactly one adapter `.cpp`, while type-independent support is isolated and CMake enumerates the deterministic set | All sources still build one configured shared library |

<!-- acceptance-case: AC-01 -->
### AC-01 — One semantic operation produces its complete binding chain

```gherkin
Scenario: Generate a supported parsed operation
  Given a parsed OCCT operation has complete source, transport, adapter, managed, ownership, nullability, direction, error, and lifetime semantics
  When generation completes
  Then exactly one manifest row, C declaration, C++ adapter, managed import, and public managed call are emitted from that operation
  And every layer has the same canonical identity and compatible contract
  And removing or mismatching one layer fails generation before native materialization
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Binding output contains no declaration-specific handwritten authority

```gherkin
Scenario: Inspect production generation inputs
  Given the supported operation set is generated
  When Generator production source and materialized binding inputs are inspected
  Then there is no manually curated per-operation catalog
  And there is no copied handwritten operation adapter or managed import list
  And no final operation symbol is assigned as a source literal
  And changing the parsed semantic fixture changes every affected layer through the common pipeline
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Active interop artifacts are unversioned and exactly matched

```gherkin
Scenario: Generate and load a matching artifact set
  Given one canonical semantic manifest
  When its native and managed artifacts are generated and loaded together
  Then active production source, build configuration, generated output, executable fixtures, and current user and maintainer documentation contain no ABI major or minor decoration
  And managed code verifies the exact generated contract fingerprint before resolving or invoking an OCCT operation
  And representative generated calls succeed

Scenario: Load a native artifact with a different fingerprint
  Given managed and native artifacts were generated from different canonical manifests
  When managed code initializes the native boundary
  Then initialization throws BadImageFormatException identifying a contract mismatch
  And only the fingerprint support export was resolved
  And no generated OCCT operation export is resolved or invoked

Scenario: Load a native artifact without fingerprint support
  Given the selected native artifact is the old ABI-v1 library or another library without the generated fingerprint support export
  When managed code initializes the native boundary
  Then initialization throws BadImageFormatException identifying that no compatible contract identity is available
  And the failure is not deferred as EntryPointNotFoundException from an operation call
  And no generated OCCT operation export is resolved or invoked

Scenario: Change one fingerprint-sensitive field
  Given two canonical manifests differ in exactly one resolution, layout, ownership, error, lifetime, or cleanup-sensitive field
  When their fingerprints are generated
  Then the 32-byte SHA-256 fingerprints differ
  And both artifacts expose the same fixed fingerprint bootstrap symbol, cdecl convention, and output transport
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Incomplete semantics fail closed without partial output

```gherkin
Scenario: Generate sibling operations with complete and incomplete mappings
  Given one operation is fully mapped and one lacks any required cross-language or lifetime rule
  When the canonical generation pipeline runs
  Then the complete operation emits its entire binding chain
  And the incomplete operation emits no declaration, symbol, adapter, import, or public call
  And one deterministic disposition identifies its semantic identity and missing rule
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Generated code preserves the real interop boundary

```gherkin
Scenario: Exercise generated value, handle, UTF-8, error, and cleanup categories
  Given representative parsed OCCT operations for every currently enabled transport and ownership category
  When the generated native library is compiled and called by both a strict C11 consumer and the generated managed API
  Then observed values and mutations match OCCT behavior
  And native exceptions become the preserved managed error contract
  And every owned handle, error diagnostic, and buffer is released exactly once through its allocating library
  And no generated public native declaration exposes a C++ or unsupported representation
```

<!-- acceptance-case: AC-06 -->
### AC-06 — Identical inputs reproduce the complete generated contract

```gherkin
Scenario: Generate twice from identical pinned inputs
  Given identical OCCT declarations, Generator revision, options, and semantic mapping rules
  When complete generation runs in two clean output roots
  Then the manifests, fingerprints, headers, native sources, managed sources, and build descriptions are byte-identical
  And source or traversal order does not change operation identities or output order
```

<!-- acceptance-case: AC-07 -->
### AC-07 — Configured OCCT headers drive the generated native adapter

```gherkin
Scenario: Generate native adapters for a configured header selection
  Given generation selects one or more OCCT headers whose parsed operations include supported and unsupported members
  When the complete binding pipeline materializes the native project
  Then CppGenerator emits definitions matching the generated C declarations with the exact required OCCT includes and C++ invocation adapters for every supported selected operation
  And no declaration-specific adapter for an unselected or unsupported operation is emitted
  And the generated CMake project compiles those adapters into the configured native library basename

Scenario: Invoke a generated adapter derived from a header declaration
  Given a controlled configured header declares representative currently supported constructors, destructors, instance or static methods, overloads, operators, or conversions
  When its AST declarations are normalized and the generated native and managed projects are built
  Then each generated adapter is traceable to its source header, declaration location, canonical owner type, and generated source file
  And each adapter converts its C transport and invokes the real inline or linked OCCT declaration
  And representative generated managed calls observe the declared OCCT behavior

Scenario: Parse transitive dependencies of a selected header
  Given a selected header includes dependency headers containing otherwise eligible declarations
  When generation completes
  Then dependency declarations needed to compile selected operations contribute types and includes
  But their operations are not enrolled unless their owning record is also selected
```

<!-- acceptance-case: AC-08 -->
### AC-08 — RecordModel is the only declaration and operation authority

```gherkin
Scenario: Project configured parsed records for interop generation
  Given configured headers contain supported, unsupported, and excluded operations plus duplicate selections or redeclarations
  When RecordModel normalization and interop projection complete
  Then every selected canonical candidate has exactly one deterministic disposition
  And every fully mapped method is emitted exactly once from its originating RecordModel
  And duplicate selections and redeclarations are coalesced before projection validation
  And unsupported and excluded candidates appear in no emitted binding layer
  And every emitter, the contract fingerprint, and the serialized manifest agree exactly with the same record/method projections
  And production source contains no Abi operation model, operation Catalog, or handwritten operation list

Scenario: Change or reorder configured declarations
  Given two generation inputs contain the same canonical declarations in different selection or traversal orders
  When both record collections are projected and generated
  Then their dispositions, manifests, fingerprints, and emitted artifacts are byte-identical
  But adding or removing a supported canonical declaration changes every affected output layer coherently
```

<!-- acceptance-case: AC-09 -->
### AC-09 — Interop semantics extend RecordModel without duplicating it

```gherkin
Scenario: Project parsed methods into their interop emission facts
  Given configured parsed operations exercise every currently enabled transport, receiver, ownership, error, cleanup, and lifetime category
  When semantic projection, validation, identity assignment, and emission complete
  Then no production operation set or identity depends on an Abi namespace, Catalog, Conformance list, copied declaration graph, or ABI-major value
  And each interop projection references its originating RecordModel and MethodModel rather than copying their identity, parameters, result, headers, or source location
  And those projections are the only additional inputs to dispositions, C declarations, C++ adapters, managed bindings, the manifest, and the fingerprint
  And each generated method has a validated source invocation and every required cross-language conversion and lifetime rule

Scenario: Project an operation with an incomplete C++ plan
  Given a parsed declaration has valid C transport but lacks a receiver, argument, result, invocation, cleanup, error, or lifetime rule required by its category
  When the method projection validator runs
  Then the operation receives one deterministic unsupported disposition
  And no C, C++, managed, manifest-supported, or fingerprint-supported operation artifact is emitted
```

<!-- acceptance-case: AC-10 -->
### AC-10 — Native operation adapters are emitted one source per canonical type

```gherkin
Scenario: Materialize supported operations owned by multiple types
  Given two canonical owner types have supported operations and a third selected type has only unsupported operations
  When the native project is generated
  Then exactly one operation-adapter cpp file is emitted for each of the two supported owner types
  And each type file defines all and only the adapters and type-owned lifetime behavior of its owner
  And the unsupported-only type emits no empty cpp file
  And the generated source inventory maps every supported operation to its owning generated source
  And CMake lists every type source and the explicitly named common support source exactly once in ordinal path order

Scenario: Reorder declarations for the same owner types
  Given two inputs contain the same canonical operations in different header or AST traversal orders
  When both native projects are generated
  Then their source file sets, file names, file contents, and CMake source order are byte-identical

Scenario: Map distinct canonical types to the same source path
  Given two selected canonical owner types collide after generated-file naming
  When native materialization begins
  Then generation fails with both canonical type identities and the conflicting path
  And no generated project file is written or overwritten
```

## Governing constraints and risks

- [GEN-01](../../principles/README.md#gen-01-generate-every-binding-layer-from-one-semantic-source)
  is Active at `c6fd61b5a4d3d78a965ee7404535374f8eceb2d3`.
- [ADR-0006](../../adr/ADR-0006-generate-an-unversioned-matched-interop-boundary.md) is Accepted at
  `357fadd140b49b8e6b2a40b16191c420e30a4efb`.
- ADR-0001 remains authoritative only for retained C11 transport, ownership, error, same-library
  cleanup, and fail-closed semantics. Its versioning, authority, and naming decisions are replaced
  by accepted ADR-0006.
- The former approval of `deliver-generated-occt-package` at
  `bb7e1289e6ec56e3187eaae8de5e3a88553de4a9` is suspended by its Draft disposition at
  `bda0c98c0614a24e2835ae70aab8a3f5b5c3b6d1` after accepted ADR-0006. Its Draft work-item map remains
  unauthorized. This disposition is sufficient to plan
  and execute this migration; the package contract and map must be reconciled with the completed
  replacement and explicitly re-approved before package delivery resumes and before this migration
  can be marked complete. This migration does not silently approve package delivery.
- Exact fingerprint matching deliberately rejects additive mismatch and requires atomic deployment
  of generated managed/native artifacts. Side-by-side native contract generations are unsupported.
- The bootstrap export is invariant and excluded from the fingerprint domain: its symbol is exactly
  `ted_toolkit_occt_contract_fingerprint`, its calling convention is cdecl, and it writes exactly
  32 digest bytes to a non-null caller-owned `uint8_t*` buffer.
- The fingerprint is SHA-256 over a length-delimited canonical UTF-8 manifest encoding. Its domain
  includes every other emitted support and operation identifier,
  calling convention, ordered transport signature, direction/nullability/ownership rule, transport
  declaration layout and numeric constant, and error/lifetime/cleanup contract. Its encoding is a
  private implementation choice, but omitting a resolution- or memory-sensitive field is outside
  this change.
- Removing the v1 scaffold can temporarily leave no buildable native proof if layers are removed
  before their generated replacements exist. Delivery must preserve an atomic revert boundary and
  must not delete the current proof until the replacement reaches its primary boundary proof.
- Historical `CppGenerator` invocation rendering may inform include discovery and C++ call syntax,
  but its exported signatures, `CSHARP_WRAPPER` macros, raw delete/destructor choices, and direct
  ref-count manipulation are evidence to replace, not compatibility behavior to preserve.
- A serialized manifest may audit emitted methods, but no in-memory operation Catalog or copied
  declaration graph is permitted. Tests derive expectations from controlled parsed records rather
  than production projection code.
- One-source-per-type is an observable artifact and build-layout contract, not a private batching
  choice. Helper decomposition and operation order within a type source remain private provided the
  generated output is deterministic and contains no duplicate definition.
- Escalate if a supported external C consumer requires native compatibility across releases, two
  native contract generations must coexist, an operation requires declaration-specific executable
  source, retained ownership/error semantics must change, or one-source-per-type cannot preserve
  ODR, symbol identity, cross-type ownership, or required OCCT template instantiation semantics.

<!-- section: delivery-brief -->
## Delivery disposition

This cross-cutting migration requires a separately approved work-item map after the change contract
is approved. Semantic normalization and cross-language generation, managed/native exact-match
initialization, and replacement boundary/build proof have independently verifiable outputs and
cannot honestly be treated as one delivery. Change approval therefore authorizes
`plan-work-items`, not implementation.

The revised three-item map must assign AC-07, AC-09, AC-10, removal of the parallel `Abi`/Catalog
graph, and per-type source ownership before MIG-001 implementation continues.

Real planning prerequisites are pinned Active GEN-01 and Accepted ADR-0006; the package change's
Draft/suspended disposition; and the .NET, CMake, C/C++ compiler, and pinned OCCT toolchain
used by the existing native proof. Likely touchpoints are non-binding: a reintroduced
`src/core/TedToolkit.Occt.Generator/Generators/CppGenerator.cs`; Generator parse/model/resolver and
removal of the production `Abi` tree; `GenerateCppModule` and native project materialization;
managed Runtime/import generation; Generator and Runtime tests, including tests that currently
enumerate production conformance operations as their oracle; root CMake/console fixtures; and current interop and component
documentation. Work-item planning leaves normalized model decomposition,
private helper structure, operation ordering within each type source, digest encoding,
generated-file retention, test organization, and edit order private to delivery. Translation-unit
batching and source layout are not private: operation adapters follow the one-source-per-type
contract above.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance, contract, regression | Component plus generated-artifact contract | A supported semantic fixture produces exactly one compatible row in every required layer; missing or mismatched layers reject materialization | Run the Generator TUnit project with complete, missing-layer, duplicate-layer, and mismatched-contract fixtures |
| AC-02 | Acceptance, structural | Repository structure plus component generation | Production source contains no per-operation catalog, handwritten operation adapter/import list, or literal final operation symbol; a changed semantic fixture propagates to every output | Inspect production source and embedded resources, run bounded forbidden-pattern checks, and execute a two-fixture cross-layer generation comparison |
| AC-03 | Acceptance, boundary, regression | Contract plus Integration | Matching unversioned artifacts initialize and call successfully; the fixed bootstrap is identical across manifests; each single-field mutation in every fingerprint-domain category changes the digest; a different fingerprint and an absent fingerprint export both throw `BadImageFormatException` after resolving at most fingerprint support; active artifacts contain no version decoration | Mutate one canonical fixture field at a time across identifiers, conventions, signatures, direction/nullability/ownership, layouts/constants, error, lifetime, and cleanup and compare SHA-256 results; build two generated native candidates from different manifests plus a native fixture with no fingerprint support; load matching, differing, and absent-fingerprint cases with export-resolution instrumentation; scan production source, root/component build configuration, generated output, executable tests/fixtures, root/component READMEs, and non-ADR/non-change guides; and inspect dependent Draft records separately for reconciliation |
| AC-04 | Acceptance, regression | Component | One incomplete sibling emits no layer and one precise disposition while its supported sibling remains complete | Run the Generator TUnit project with mixed complete/incomplete semantic fixtures |
| AC-05 | Acceptance, boundary, regression | Contract plus Integration through C11 and generated managed API | Real generated value, handle, UTF-8, error, and cleanup paths match OCCT and satisfy retained C11/ownership rules | Build the generated native project; compile and run the strict C11 consumer; run generated managed boundary cases through the Generator and Runtime TUnit projects |
| AC-06 | Acceptance, reproducibility | Structural contract comparison | Two clean runs produce byte-identical canonical manifests, fingerprints, C/C++/C# sources, and build descriptions independent of traversal order | Generate into two clean output roots with normal and reversed input order and byte-compare the approved generated artifacts |
| AC-07 | Acceptance, regression, boundary | Component plus native and managed Integration | Two distinct configured header selections produce correspondingly distinct complete adapter/include sets; transitive dependencies do not enroll operations; unsupported and unselected operations are absent; source traceability is preserved; generated definitions compile, link to the real OCCT declarations, and execute through managed calls | Run Generator TUnit fixtures for selection, include closure, source trace, invocation categories, and exclusion; configure and build each materialized CMake project against the pinned OCCT toolchain; then invoke representative generated operations through the managed boundary |
| AC-08 | Acceptance, contract, regression | Component plus generated-artifact contract | Controlled parsed records provide the only declaration graph; every method has one disposition; duplicate selections/redeclarations coalesce; identity collisions fail; every emitter and manifest reconciles directly to the same records; reorder inputs are identical while a semantic add/remove changes all affected layers | Run Generator TUnit fixtures with supported, unsupported, excluded, duplicate-selection, redeclaration, missing-disposition, collision, reordered, added, and removed records/methods; validate manifest-to-artifact identities and structurally verify production source contains no `Abi` operation model, Catalog, or handwritten operation list |
| AC-09 | Acceptance, contract, regression | Component plus generated-artifact contract | Lightweight interop projections reference existing record/method/type objects and add only transport/conversion/lifetime facts; every missing C++ conversion/invocation/error/lifetime category fails closed | Run Generator TUnit fixtures across every enabled operation and transport category plus one-at-a-time missing receiver, argument, result, invocation, cleanup, error, and lifetime rules; inspect projection construction for copied declaration identity and forbidden `Abi`/Catalog authority |
| AC-10 | Acceptance, structural, regression | Component plus generated native project | Supported owner types produce exactly one correctly partitioned source each; unsupported-only types produce none; common support is isolated; CMake is exact and sorted; reorder is byte-identical; path collisions fail before writes | Run Generator TUnit fixtures with two supported types, one unsupported-only type, reversed declarations, overloads and type-owned lifetime operations, plus colliding canonical type names; reconcile the generated source inventory to operation ownership and definitions, then configure and build the generated CMake project |

Known repository gates remain:

```powershell
dotnet build TedToolkit.Occt.slnx -c Release
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release --no-build -- --report-trx
```

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-10 pass on the supported Windows x64 OCCT matrix; all required binding layers and
 their exact fingerprint are generated from the canonical `RecordModel` graph and its lightweight
 interop projections; the strict C11 and generated
managed consumers pass against the same generated native library; every supported canonical owner
type has exactly one adapter source and every such source is listed exactly once in the native
 build; no production `Abi` operation model/directory, operation Catalog, handwritten operation
 list, handwritten final symbol, adapter/import list, partial unsupported operation, ABI major/minor mechanism, conformance/v1 model authority, or
version-decorated active artifact remains; current documentation and
the explicitly re-approved package change and its delivery map describe the replacement truth; and
no package is remotely published by this change.
