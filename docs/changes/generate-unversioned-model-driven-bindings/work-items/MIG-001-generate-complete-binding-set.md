# MIG-001: Generate one complete binding set directly from RecordModel

<!-- work-item-format: 2 -->

- Approval: User approval in the current Codex task on 2026-08-25 for RecordModel-centered Draft
  content SHA-256 `1A3B5039DA4FCE29074A3B3E85BA4F35D7DA90EB525F8755739F5AD86DCE9A12`.

## Outcome

Configured parsed OCCT `RecordModel` instances receive deterministic interop projection results and
produce a complete mutually compatible manifest, C header, one C++ adapter source per supported
record, managed transport/import/public source, and native build description. No parallel
`AbiOperationModel`, operation Catalog, or handwritten operation set participates.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: `RecordModel`/`MethodModel` and resolved
  type projections, their small interop mapping results, canonical manifest/fingerprint input, and
  all text emitters governed by GEN-01 and ADR-0006.
- In scope: canonical redeclaration/selection coalescing; complete mapping validation; unsupported
  and excluded dispositions; deterministic algorithmic names; invariant bootstrap emission;
  cross-layer completeness checks; removal of the production `Abi` and operation Catalog graphs;
  lightweight interop facts referencing existing record/method/type models; `CppGenerator` include, conversion, invocation, and one-source-per-type
  emission; deterministic source inventory and CMake enumeration; reproducible replacement artifact
  materialization without reliance on the handwritten conformance catalog or copied adapter/import
  sources. The isolated legacy proof remains until MIG-003 proves and performs the repository-wide
  replacement.
- Non-goals: dynamic native loading, export-resolution sequencing, compiling the generated native
  project against OCCT, replacing root ABI-v1 build fixtures, package creation, or wider OCCT
  declaration-category coverage.
- Likely touchpoints (non-binding): Generator parse/model/resolver and removal of ABI contracts/generation;
  `CSharpGenerator`; a generated-native `CppGenerator`; `GenerateCppModule`; Generator tests and
  controlled Clang fixtures.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| Approved parent | One accepted migration contract and pinned governing revisions | Renewed approved `change.md` |
| Parser baseline | Configured OCCT headers already produce declaration/type models usable by controlled fixtures | Existing Generator parser and tests |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-01 | Owns | Prove one fully mapped parsed operation yields exactly one compatible entry in every required layer and incomplete/mismatched chains reject materialization. |
| AC-02 | Supports | Prove a parsed fixture change propagates through every replacement output and supply the no-handwritten-authority candidate to MIG-003 for final repository replacement inspection. |
| AC-03 | Supports | Supply the canonical SHA-256 digest, fixed bootstrap source, managed expected digest, and operation identities consumed by MIG-002. |
| AC-04 | Owns | Prove incomplete siblings emit no layer and receive one stable disposition without affecting supported siblings. |
| AC-05 | Supports | Supply generated source for every currently enabled transport, ownership, error, and cleanup category consumed by MIG-003. |
| AC-06 | Owns | Prove identical and reordered inputs produce byte-identical complete artifacts. |
| AC-07 | Supports | Supply selection-driven C++ includes, adapters, and the configured-basename CMake description consumed by MIG-003. |
| AC-08 | Owns | Prove RecordModel is the only declaration graph, canonical coalescing and dispositions are complete, collisions reject, and every emitter/fingerprint agrees directly with the same records. |
| AC-09 | Owns | Prove small interop projections reference existing models, contain no copied declaration identity, and reject every incomplete C++ plan without emitting a partial layer. |
| AC-10 | Owns | Prove each supported canonical owner produces exactly one correctly partitioned adapter source, unsupported-only owners produce none, source collisions fail before writes, and source/CMake output is deterministic. |

<!-- work-item: delivery-constraints -->
## Constraints

- The parsed `RecordModel` graph is the only declaration-specific authority. No production source or
  embedded resource may enumerate supported OCCT operations or final operation symbols manually.
- The bootstrap symbol, cdecl convention, and 32-byte output transport are immutable and excluded
  from the manifest fingerprint domain. Every other resolution-, layout-, ownership-, error-,
  lifetime-, and cleanup-sensitive field participates in the length-delimited canonical encoding.
- Methods are emitted only after complete projection validation; unsupported or excluded
  operations emit no binding layer. The C header remains valid C11 and C++ and exposes no C++/STL
  representation.
- Public managed ownership, handle, and exception semantics remain governed by ADR-0004 and
  ADR-0005.
- Private model decomposition, canonical serialization representation, helper structure, operation
  order within a type source, and test organization remain implementation choices. Translation-unit
  batching and source layout are fixed by the parent's one-source-per-type contract.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 / AC-04 / AC-08 | Acceptance and regression | Component plus generated-artifact Contract | Controlled parsed records have one disposition per method; supported methods appear exactly once and compatibly in every layer; partial, duplicate, colliding, and incomplete cases fail closed; production contains no `Abi` operation model or Catalog. | Run `dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx` with complete, unsupported, excluded, duplicate-selection, redeclaration, missing-rule, duplicate-identity, and collision fixtures plus bounded structural inspection. |
| AC-02 support | Acceptance and structural | Component plus bounded candidate inspection | The replacement pipeline consumes no per-operation catalog, copied operation adapter/import list, or literal final operation symbol; changing a parsed fixture changes every affected generated layer. | Run the Generator TUnit propagation cases, then inspect the replacement production inputs and emitted candidate with bounded forbidden-pattern searches defined by the replacement names. |
| AC-06 | Acceptance and reproducibility | Contract | Normal, reversed, and duplicate selection inputs yield byte-identical manifests, fingerprints, C/C++/C# sources, and build descriptions; semantic add/remove changes all affected artifacts. | Generate controlled inputs into clean roots and byte-compare the approved artifact set in Generator TUnit cases. |
| AC-09 | Acceptance and regression | Component plus generated-artifact Contract | Every enabled interop category adds only mapping facts referencing an existing record/method/type; missing receiver, conversion, invocation, cleanup, error, or lifetime semantics produces one unsupported disposition and no partial layer. | Run the Generator TUnit project with complete category fixtures and one-at-a-time incomplete C++ plan fixtures, then inspect projection construction for copied declaration graphs and forbidden `Abi`/Catalog authority. |
| AC-10 | Acceptance, structural, and reproducibility | Component plus generated native project | Supported owners produce one adapter source each containing only their operations; unsupported-only owners produce none; the source inventory and sorted CMake list are exact; reordered inputs are byte-identical; path collisions fail without partial writes. | Run Generator TUnit fixtures with multiple supported owners, one unsupported-only owner, reversed declarations, overloads, type-owned lifetime operations, and colliding source paths; reconcile the source inventory to definitions and configure the generated CMake project. |

<!-- work-item: definition-of-done -->
## Done

- AC-01, AC-04, AC-06, AC-08, AC-09, and AC-10 have passing primary proof, and AC-02 fixture
  propagation support evidence is supplied to MIG-003.
- The replacement generation path has no dependency on the handwritten operation catalog or copied
  operation adapters/import lists; the isolated legacy proof is unchanged until MIG-003 replaces it.
- MIG-002 receives the canonical digest/bootstrap/managed identity outputs, and MIG-003 receives a
  complete deterministic generated native/managed/build candidate with passing component proof.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

The implementation handoff records candidate revision, actual changed artifacts, owned contract
IDs, evidence purpose and execution shape, commands, assertions, results/counts, fixture and
toolchain prerequisites, structural inspection results, and the exact outputs supplied to MIG-002
and MIG-003. Mutable status remains in `work-items.md`.

## Risks and implementation notes

- Existing tests that construct `AbiV1ConformanceCatalog` cannot be the expected-operation oracle;
  controlled parsed inputs must provide independent expectations.
- `CSharpGenerator` currently emits type shapes while the ABI subsystem separately emits a small C
  surface. Avoid preserving those parallel authorities behind new names.
