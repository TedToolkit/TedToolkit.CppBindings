# Generate unversioned bindings from one semantic model

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: approved -->

- Priority: P1
- Approval: User approval in the current Codex task on 2026-08-25 after independent review of the
  unversioned generation, immutable fingerprint bootstrap, exact-match loading, catalog, native
  adapter, migration, and proof contracts recorded here.

<!-- section: goal-rationale -->
## Goal and rationale

For every supported OCCT operation, the Generator produces one coherent, unversioned C/C++/C#
binding chain from the parsed normalized semantic model. The current native path instead selects a
handwritten nine-operation catalog, emits declaration-specific C content, and copies a handwritten
C++ adapter with duplicated final symbols; this prevents scalable generation and permits layers to
drift while still compiling.

The removed pre-version `CppGenerator` proves that parsed record, method, and required-header
metadata can drive C++ includes and invocation bodies. Its raw C++ export signatures, macro-owned
error boundary, and record-level lifetime guesses are not safe to restore. The replacement keeps
that model-driven emitter responsibility while consuming only validated cross-language operation
semantics and emitting the approved C transport boundary.

<!-- section: scope -->
## Scope and non-goals

- In scope: canonical semantic operation modeling; generated C11 declarations, C++ adapters,
  native build description, managed transports/imports/public calls, operation manifest, exact
  contract fingerprint, deterministic unversioned naming, fail-closed dispositions, and migration
  of active source/build/tests/current documentation away from ABI-major-1 scaffolding.
- In scope: a `CppGenerator`-owned native adapter emission path driven by the configured OCCT header
  selection and its normalized supported operations, including deterministic required includes,
  source C++ receiver/argument/result conversion, invocation bodies, and cleanup/error adapters.
- In scope: removal of the handwritten conformance catalog, embedded handwritten operation adapter,
  handwritten final operation symbols, version bootstrap/range checks, and every active `v1`
  identifier or path.
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

### Generated catalog boundary

- The current `AbiV1ConformanceCatalog.CreateOperations()` declaration list is removed as a
  production input. No replacement source file may enumerate specific OCCT operations by hand.
- Each generation run derives one candidate catalog from the configured parsed headers. Duplicate
  selections and Clang redeclarations are first coalesced by canonical semantic identity. Every
  resulting candidate receives exactly one generated, unsupported, or explicitly excluded
  disposition; a duplicate catalog identity after canonicalization, a missing disposition, or an
  identity collision between different semantics fails generation.
- Fully mapped candidates become one frozen supported-operation catalog only after validation of
  their source identity, transport, adapter, managed projection, direction, nullability, ownership,
  error, and lifetime semantics. All C, C++, C#, naming, fingerprint, and build emitters consume
  that exact snapshot rather than rebuilding or filtering independent operation lists.
- Generic fingerprint, error, allocation, and cleanup support may be synthesized by category-level
  rules. Such rules cannot name an OCCT declaration or become a second declaration-specific
  catalog.
- The serialized operation manifest is a deterministic audit projection of the frozen catalog, not
  another editable authority. Configured-header order, AST traversal order, and duplicate selection
  do not change catalog identity, entry order, dispositions, or emitted artifacts.
- Tests must derive expected candidates from controlled parsed inputs and inspect the resulting
  dispositions, manifest, and emitted artifacts. Production catalog creation cannot also serve as
  the test oracle for which operations should exist.

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
| OB-07 | Operation catalog | `AbiV1ConformanceCatalog` manually enumerates supported production operations, and tests can use the same list as their expectation | Parsed configured candidates produce one validated frozen catalog and deterministic dispositions; every emitter and the manifest consume that same snapshot | A catalog remains available for deterministic generation, inspection, and fingerprinting |

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
  Then CppGenerator emits the exact required OCCT includes and C++ invocation adapters for every supported selected operation
  And no declaration-specific adapter for an unselected or unsupported operation is emitted
  And the generated CMake project compiles those adapters into the configured native library basename
```

<!-- acceptance-case: AC-08 -->
### AC-08 — The generated catalog is complete, deterministic, and the only operation authority

```gherkin
Scenario: Build a catalog from configured parsed declarations
  Given configured headers contain supported, unsupported, and excluded operations plus duplicate selections or redeclarations
  When semantic normalization and validation complete
  Then every selected canonical candidate has exactly one deterministic disposition
  And every fully mapped candidate appears exactly once in one frozen supported-operation catalog
  And duplicate selections and redeclarations are coalesced before catalog validation
  And unsupported and excluded candidates appear in no emitted binding layer
  And every emitter, the contract fingerprint, and the serialized manifest agree exactly with that catalog

Scenario: Change or reorder configured declarations
  Given two generation inputs contain the same canonical declarations in different selection or traversal orders
  When both catalogs are generated
  Then their catalog entries, dispositions, manifests, fingerprints, and emitted artifacts are byte-identical
  But adding or removing a supported canonical declaration changes the catalog and every affected output layer coherently
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
- A generated catalog may be an in-memory type and a serialized manifest, but neither may contain a
  manually curated declaration list. Tests that call production catalog construction to decide
  their own expected operation set do not prove catalog completeness and must be replaced.
- Escalate if a supported external C consumer requires native compatibility across releases, two
  native contract generations must coexist, an operation requires declaration-specific executable
  source, or retained ownership/error semantics must change.

<!-- section: delivery-brief -->
## Delivery disposition

This cross-cutting migration requires a separately approved work-item map after the change contract
is approved. Semantic normalization and cross-language generation, managed/native exact-match
initialization, and replacement boundary/build proof have independently verifiable outputs and
cannot honestly be treated as one delivery. Change approval therefore authorizes
`plan-work-items`, not implementation.

Real planning prerequisites are pinned Active GEN-01 and Accepted ADR-0006; the package change's
Draft/suspended disposition; and the .NET, CMake, C/C++ compiler, and pinned OCCT toolchain
used by the existing native proof. Likely touchpoints are non-binding: a reintroduced
`src/core/TedToolkit.Occt.Generator/Generators/CppGenerator.cs`; Generator parse/model/resolver and
ABI generation areas, including removal of `AbiV1ConformanceCatalog` and its `Conformance` source
boundary; `GenerateCppModule` and native project materialization; managed Runtime/import
generation; Generator and Runtime tests, including tests that currently enumerate production
catalog entries as their oracle; root CMake/console fixtures; and current interop and component
documentation. Work-item planning leaves normalized model decomposition,
translation-unit batching, source layout, digest encoding, generated-file retention, test
organization, and edit order private to delivery.

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
| AC-07 | Acceptance, regression, boundary | Component plus native Integration | Two distinct configured header selections produce correspondingly distinct complete adapter/include sets; unsupported and unselected operations are absent; each generated project builds the configured library basename | Run Generator TUnit fixtures for selection, include closure, invocation categories, and exclusion, then configure and build each materialized CMake project against the pinned OCCT toolchain |
| AC-08 | Acceptance, contract, regression | Component plus generated-artifact contract | Controlled parsed candidates produce one complete frozen catalog with exactly one disposition per canonical candidate; duplicate selections/redeclarations coalesce; post-canonicalization duplicates and semantic identity collisions fail; every emitter and manifest reconciles to the catalog; reorder inputs are identical while a semantic add/remove changes all affected layers | Run Generator TUnit fixtures with supported, unsupported, excluded, duplicate-selection, redeclaration, duplicate-catalog-identity, missing-disposition, collision, reordered, added, and removed candidates; validate manifest-to-artifact identities without using production catalog construction as the expected-operation oracle |

Known repository gates remain:

```powershell
dotnet build TedToolkit.Occt.slnx -c Release
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release --no-build -- --report-trx
```

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-08 pass on the supported Windows x64 OCCT matrix; all required binding layers and
their exact fingerprint are generated from one canonical model; the strict C11 and generated
managed consumers pass against the same generated native library; no production per-operation
handwritten catalog, handwritten final symbol, adapter/import list, partial unsupported operation, ABI
major/minor mechanism, or version-decorated active artifact remains; current documentation and
the explicitly re-approved package change and its delivery map describe the replacement truth; and
no package is remotely published by this change.
