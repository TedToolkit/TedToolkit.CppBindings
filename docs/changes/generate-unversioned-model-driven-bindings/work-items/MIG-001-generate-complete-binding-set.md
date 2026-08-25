# MIG-001: Generate one complete binding set from the parsed catalog

<!-- work-item-format: 2 -->

- Approval: User approval in the current Codex task on 2026-08-25 for Draft content SHA-256
  `45BC5F8EB7BB346689F632585C205620C5864EA184D64E9DF48454C6892DB17E`.

## Outcome

Configured parsed OCCT declarations produce one validated frozen catalog, deterministic
dispositions, and a complete mutually compatible manifest, C header, C++ adapter source, managed
transport/import/public source, and native build description. This supplies the only semantic and
artifact authority consumed by later loading and real-boundary deliveries.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: the normalized candidate/disposition
  model, frozen supported-operation catalog, canonical manifest/fingerprint input, and all text
  emitters governed by GEN-01 and ADR-0006.
- In scope: canonical redeclaration/selection coalescing; complete mapping validation; unsupported
  and excluded dispositions; deterministic algorithmic names; invariant bootstrap emission;
  cross-layer completeness checks; `CppGenerator` include and invocation emission; reproducible
  replacement artifact materialization without reliance on the handwritten conformance catalog or
  copied adapter/import sources. The isolated legacy proof remains until MIG-003 proves and performs
  the repository-wide replacement.
- Non-goals: dynamic native loading, export-resolution sequencing, compiling the generated native
  project against OCCT, replacing root ABI-v1 build fixtures, package creation, or wider OCCT
  declaration-category coverage.
- Likely touchpoints (non-binding): Generator parse/model/resolver and ABI contracts/generation;
  `CSharpGenerator`; a generated-native `CppGenerator`; `GenerateCppModule`; Generator tests and
  controlled Clang fixtures.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| Approved parent | One accepted migration contract and pinned governing revisions | `change.md` at `6237a3291aad4fe423b392c31126c9add14d2dc6` |
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
| AC-08 | Owns | Prove candidate completeness, canonical coalescing, unique dispositions, collision rejection, and one-catalog agreement across emitters and fingerprint. |

<!-- work-item: delivery-constraints -->
## Constraints

- The parsed normalized model is the only declaration-specific authority. No production source or
  embedded resource may enumerate supported OCCT operations or final operation symbols manually.
- The bootstrap symbol, cdecl convention, and 32-byte output transport are immutable and excluded
  from the manifest fingerprint domain. Every other resolution-, layout-, ownership-, error-,
  lifetime-, and cleanup-sensitive field participates in the length-delimited canonical encoding.
- Supported operations are frozen only after complete validation; unsupported or excluded
  operations emit no binding layer. The C header remains valid C11 and C++ and exposes no C++/STL
  representation.
- Public managed ownership, handle, and exception semantics remain governed by ADR-0004 and
  ADR-0005.
- Private model decomposition, canonical serialization representation, translation-unit batching,
  source layout, and test organization remain implementation choices.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 / AC-04 / AC-08 | Acceptance and regression | Component plus generated-artifact Contract | Controlled parsed candidates have one disposition each; supported candidates appear exactly once and compatibly in every layer; partial, duplicate, colliding, and incomplete cases fail closed. | Run `dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx` with complete, unsupported, excluded, duplicate-selection, redeclaration, missing-rule, duplicate-identity, and collision fixtures. |
| AC-02 support | Acceptance and structural | Component plus bounded candidate inspection | The replacement pipeline consumes no per-operation catalog, copied operation adapter/import list, or literal final operation symbol; changing a parsed fixture changes every affected generated layer. | Run the Generator TUnit propagation cases, then inspect the replacement production inputs and emitted candidate with bounded forbidden-pattern searches defined by the replacement names. |
| AC-06 | Acceptance and reproducibility | Contract | Normal, reversed, and duplicate selection inputs yield byte-identical manifests, fingerprints, C/C++/C# sources, and build descriptions; semantic add/remove changes all affected artifacts. | Generate controlled inputs into clean roots and byte-compare the approved artifact set in Generator TUnit cases. |

<!-- work-item: definition-of-done -->
## Done

- AC-01, AC-04, AC-06, and AC-08 have passing primary proof, and AC-02 fixture propagation support
  evidence is supplied to MIG-003.
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
