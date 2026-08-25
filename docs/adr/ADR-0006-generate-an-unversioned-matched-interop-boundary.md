# ADR-0006: Generate an unversioned matched interop boundary

- Status: Accepted
- Date: 2026-08-25
- Decision owner: TedToolkit.Occt maintainers
- Approval: User approval in the current Codex task on 2026-08-25.
- Decision scope: The authority, naming, compatibility, and generation boundary shared by native
  OCCT adapters and generated managed bindings.
- Applicable product intent: None
- Applicable principles: [GEN-01](../principles/README.md#gen-01-generate-every-binding-layer-from-one-semantic-source)
- Supersedes: The versioned identity, major/minor compatibility, and header-as-authority directions
  of [ADR-0001](ADR-0001-stable-c-interop-abi.md), plus the
  major-version assumptions of [ADR-0002](ADR-0002-separate-abi-identity-from-library-basename.md)
  and the version-coupled native-boundary wording of
  [ADR-0003](ADR-0003-distribute-generated-occt-bindings.md),
  [ADR-0004](ADR-0004-preserve-occt-managed-type-and-handle-semantics.md), and
  [ADR-0005](ADR-0005-project-native-failures-into-managed-exceptions.md). ADR-0001's C11
  transport, ownership, error, same-library cleanup, and fail-closed constraints remain in force;
  ADR-0003's package direction, ADR-0004's managed handle model, and ADR-0005's managed exception
  model remain in force.
- Superseded by: None

## Decision at a glance

TedToolkit.Occt will generate one unversioned, exactly matched C/C++/C# interop contract from a
single normalized semantic model, with no manually curated operation catalog, handwritten final
symbol, or native ABI major/minor protocol.

## Context and decision question

The current production native path does not consume the parsed OCCT model. It selects nine
operations from a handwritten ABI-major-1 catalog, emits a header containing hardcoded
declaration-specific transports, and copies a handwritten C++ adapter whose final export symbols
duplicate the catalog's identities. Managed invocation remains a separate incomplete path. These
parallel authorities can drift and cannot scale to the complete generated binding surface.

ADR-0001 introduced ABI major 1 so incompatible native contracts could coexist and be detected.
No supported TedToolkit.Occt package or native ABI has been released, and the intended product ships
the generated managed assembly together with its generated native dependency. The decision
question is whether this unreleased versioned protocol and handwritten conformance surface should
remain the architecture, or whether every interop layer should instead be regenerated as one
unversioned, exact-match artifact set from the parsed semantics.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Every supported binding operation and all of its native and managed representations have one semantic authority. | [GEN-01](../principles/README.md#gen-01-generate-every-binding-layer-from-one-semantic-source) and the current duplicated catalog/adapter path | Must |
| Hard constraint | No handwritten final operation symbol, declaration-specific adapter/import, or manually patched generated artifact enters the supported binding. | Maintainer direction on 2026-08-25; GEN-01 | Must |
| Hard constraint | Public native declarations remain valid C11, expose no C++/STL/compiler representation, and make direction, nullability, ownership, errors, and cleanup explicit. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | Unsupported operations fail closed as complete operations without weakening unrelated supported coverage. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) and [ADR-0003](ADR-0003-distribute-generated-occt-bindings.md) | Must |
| Decision driver | Header, adapter, managed import, and public wrapper changes are coherent and reproducible for the same inputs. | Current layers are produced from different authorities. | High |
| Decision driver | Native names describe the TedToolkit.Occt boundary without `v1`, ABI-major directories, or major/minor bootstrap state. | Maintainer direction on 2026-08-25 | High |
| Decision driver | Loading a mismatched native artifact fails before an operation is invoked even though no semantic version is exposed. | Same-basename native deployment can otherwise bind a stale artifact. | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep the versioned handwritten conformance slice | Documented: this is the current path and its nine operations have boundary tests. Confidence: High. | No | Retains independent ABI evolution, but duplicates per-operation knowledge and does not scale from parsed declarations. | Rejected |
| Remove version text but retain handwritten per-operation adapters and symbols | Documented: this is a mechanical rename of the current architecture. Confidence: High. | No | Removes `v1` cosmetically while preserving the parallel authorities that caused the redesign. | Rejected |
| Generate an unversioned exact-match artifact set from one normalized semantic model | Documented: the repository already parses declarations and already has separate transport/public projections; deterministic identity and validation exist but are not connected to every output. Confidence: High for direction, Medium until complete native/managed generation is proved. | Yes | Gives up native ABI-major coexistence and requires consumers to use a matched generated set. | Selected |
| Generate from one model but keep an ABI major in all identifiers | Documented: generation and versioning are independent concerns. Confidence: High. | Partially | Fixes duplicated source but retains the version protocol and naming explicitly rejected by the maintainer. | Rejected |

## Decision

The parsed and normalized semantic operation model is the generation authority. It contains or
derives every approved source identity, receiver, parameter, result, transport, adapter, managed
projection, direction, nullability, ownership, error, and lifetime contract required to decide and
emit one operation. Generic support operations and transport declarations join the same canonical
model before validation; they do not form a handwritten parallel surface.

One validated model emits the C11 header, C++ adapters, native build description, managed
transports/imports, public C# binding, and an operation manifest. A supported operation appears
exactly once in every required layer. If a layer cannot be derived, the complete operation is
unsupported and no partial declaration, symbol, adapter, import, or wrapper is emitted.

All final identifiers are algorithmic. Operation symbols use an unversioned TedToolkit.Occt prefix,
readable canonical owner and operation stems, and a deterministic digest of the complete semantic
operation identity. No final symbol is assigned or copied by hand. Common transport and support
identifiers are emitted from the same naming policy.

The canonical header is `ted_toolkit_occt.h`, the default native artifact basename is
`ted_toolkit_occt`, and C identifiers, macros, build targets, directories, commands, fixtures, and
current documentation carry no ABI major or minor. There is no ABI version constant, version
bootstrap export, compatibility range, or side-by-side major protocol.

Instead, the complete canonical interop manifest has a deterministic contract fingerprint. The
generated managed binding and generated native artifact carry the same fingerprint and require an
exact match before any OCCT operation is resolved or invoked. The fingerprint detects mismatched
artifacts; it is not a semantic version and defines no additive compatibility range.

The fingerprint domain includes every resolution- or layout-sensitive part of the generated
boundary: emitted support and operation identifiers; calling convention; ordered parameter and
result transports; direction, nullability, and ownership; transport declaration kinds, field order,
field types, fixed layouts, and numeric constants; and error, lifetime, and cleanup contracts. Its
canonical encoding remains a private generation choice, but no layer may omit a field that can
change symbol resolution, memory interpretation, ownership, or cleanup.

Managed initialization resolves only the generated unversioned fingerprint support export before
the match is established. A missing fingerprint export, including an old ABI-v1 native artifact,
and a present but different fingerprint both produce `BadImageFormatException` and resolve no OCCT
operation export. The missing case reports that no compatible contract identity is available; the
different case may report both identities for diagnosis. Neither case falls through as an
`EntryPointNotFoundException` from an operation call.

The native ABI is an internal generated boundary of one shipped artifact set, not an independently
versioned compatibility product. A C consumer may compile against the generated header, but must be
rebuilt and revalidated with each delivered artifact set. After the first public package release,
managed public API compatibility remains a product commitment independent of native symbol
versioning.

## Why this decision now

The native ABI has not been released, so the repository can remove the experimental versioned
surface without migrating external consumers. GEN-01 makes parallel operation authorities a
prohibited recurring design, and the intended package already controls the managed/native artifact
pair. Exact contract matching preserves stale-library detection without imposing a public
major/minor protocol or version-decorated identifiers.

Reconsider this direction only if the native C ABI itself becomes a separately distributed public
product, incompatible native generations must coexist in one process, or an external consumer must
upgrade native and managed artifacts independently.

## Evidence and links

- [Current ABI subsystem boundary](../../src/core/TedToolkit.Occt.Generator/Abi/README.md)
- [Current Generator pipeline](../../src/core/TedToolkit.Occt.Generator/README.md)
- [ADR-0001: Establish a stable C interoperability ABI](ADR-0001-stable-c-interop-abi.md)
- [ADR-0002: Separate ABI identity from the native library basename](ADR-0002-separate-abi-identity-from-library-basename.md)
- [ADR-0003: Distribute generated OCCT bindings as a ready-to-use package](ADR-0003-distribute-generated-occt-bindings.md)

## Consequences and accepted trade-offs

- The handwritten conformance catalog and handwritten operation adapters cease to be supported
  production authorities.
- Native and managed generation must integrate through one canonical model and manifest; this is a
  larger generator responsibility than emitting independent type shapes.
- Native symbols and support types become clean and unversioned while remaining deterministic and
  collision-checked.
- An exact fingerprint rejects additive as well as incompatible mismatches. Managed and native
  artifacts must therefore be deployed as one atomic package set.
- Different native contract generations are not supported side by side under one artifact name in
  the same process.
- Historical ADRs retain their versioned examples as decision history. Active source, build,
  fixtures, generated output, and current user documentation do not.
- Existing C11 transport safety, explicit ownership, error projection, same-library cleanup, and
  fail-closed rejection obligations remain unchanged.
- Package composition, managed handle ownership, and managed exception decisions remain unchanged;
  their references to an ABI major become references to the exact-match generated native boundary.

## Downstream delivery constraints

- One canonical semantic operation identity drives eligibility, naming, every emitted layer, and
  cross-layer completeness validation.
- Production generation contains no declaration-specific operation catalog, handwritten final
  operation symbol, copied adapter implementation, or manually maintained import list.
- The emitted header remains valid C11 and C++ without OCCT or C++ standard-library declarations.
- Exact contract fingerprint validation completes before resolving or invoking generated OCCT
  operations. A missing or different fingerprint produces `BadImageFormatException`; only the
  fingerprint support export may be resolved before rejection.
- The fingerprint domain contains every generated identifier, calling convention, ordered
  transport signature, direction/nullability/ownership rule, transport layout and numeric
  constant, and error/lifetime/cleanup contract that can affect resolution or memory safety.
- No active artifact or current interface exposes ABI major/minor numbers or version-decorated
  names.
- Retained ownership, error, lifetime, and cleanup guarantees are proved through generated native
  and managed code, not a separate handwritten conformance implementation.
- The generated artifact set and operation manifest are reproducible from identical pinned inputs.

## Exit requirements

A replacement must preserve one-source cross-layer completeness, fail-closed mappings, C11
transport safety, same-library cleanup, and deterministic mismatch detection. Introducing semantic
native versions or side-by-side native compatibility requires a new accepted ADR with an evidenced
external interoperability need.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Apply ADR-0006 supersession metadata to ADR-0001 through ADR-0005 without rewriting historical rationale. | TedToolkit.Occt maintainers | When ADR-0006 is accepted | Completed 2026-08-25 |
| Update current delivery records to consume the unversioned exact-match boundary. | TedToolkit.Occt maintainers | Before the dependent delivery change is approved | Open |
| Reassess public native compatibility. | TedToolkit.Occt maintainers | A supported consumer must update native and managed artifacts independently | Open |
| Reassess side-by-side loading. | TedToolkit.Occt maintainers | Two generated native contracts must coexist in one process | Open |
