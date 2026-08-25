# ADR-0002: Separate ABI identity from the native library basename

- Status: Accepted
- Date: 2026-08-25
- Decision owner: TedToolkit.Occt maintainers
- Approval: User approval in the current Codex task on 2026-08-25.
- Decision scope: Naming of generated native library artifacts for the versioned C interoperability boundary.
- Applicable product intent: None
- Applicable principles: None
- Supersedes: The fixed native library basename decision in [ADR-0001](ADR-0001-stable-c-interop-abi.md); all other ADR-0001 decisions remain unchanged.
- Superseded by: [ADR-0006](ADR-0006-generate-an-unversioned-matched-interop-boundary.md) for ABI-major assumptions. The clean configurable artifact-basename decision remains active.

## Decision at a glance

TedToolkit.Occt will identify ABI majors through their canonical protocol, generate the clean
default native library basename `ted_toolkit_occt`, and allow consumers to configure that artifact
basename independently.

## Context and decision question

ADR-0001 assigns ABI major 1 the native library basename `ted_toolkit_occt_abi_v1` so incompatible
majors can coexist. The current generator embeds that basename in its generated CMake project, so
every consumer receives the same artifact filename regardless of its product, packaging, or
deployment naming requirements.

The canonical header, exported symbol namespace, and bootstrap version export already identify and
validate the ABI independently of the artifact filename. ADR-0001 also records that ABI version 1
has no released compatibility baseline. The decision question is whether the artifact basename
must remain part of the ABI identity or may use a clean default and be consumer-configurable without
weakening version detection and compatibility.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | The canonical header, symbol namespace, calling convention, ownership rules, and bootstrap version contract remain the ABI authority. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | Configuration cannot inject CMake syntax, select a directory, or include a platform-specific library prefix or suffix. | The value is emitted into a cross-platform generated CMake project. | Must |
| Decision driver | A consuming product can align the native artifact with its own packaging and deployment naming. | The current generator offers no naming input. | High |
| Decision driver | Default deployment names remain concise and do not expose internal protocol terminology or version decoration. | `abi_v1` describes the boundary rather than the consumer-facing library purpose. | High |
| Decision driver | Different artifact names must not alter link dependencies, exported symbols, or ABI version validation. | ABI consumers link and validate behavior independently of the physical filename. | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep the fixed basename | Documented: this is the current generated CMake behavior. Confidence: High. | No | Preserves one canonical filename but prevents consumer packaging control. | Rejected |
| Make the CMake target and artifact basename configurable together | Documented: target references are used by the repository's native consumer. Confidence: High. | Partially | Exposes an internal build identity and makes downstream linking configuration-dependent. | Rejected |
| Keep a stable internal target and configure only its output basename, defaulting to `ted_toolkit_occt` | Documented: CMake separates target identity from its `OUTPUT_NAME`; ADR-0001 records no released ABI v1 baseline. Confidence: High. | Yes | Adds validation and proof obligations; ABI-major filename coexistence becomes opt-in. | Selected |

## Decision

Each ABI major retains a stable, versioned internal native target identity. The consumer-facing
artifact basename defaults to the unversioned `ted_toolkit_occt`.

Consumers may configure the generated artifact basename. The configured value is a portable file
basename only: it contains no directory, platform library prefix, or extension, and it is restricted
to a syntax that can be emitted as inert CMake data. Invalid values are rejected before the native
project is materialized.

Configuration changes only the platform artifact name derived by the build system. It does not
change the CMake target identity, canonical header name, exported identifiers, ABI version, calling
convention, ownership boundary, or supported platform matrix.

## Why this decision now

The fixed basename combines two concerns that already have independent enforcement: ABI identity is
defined by the canonical surface and checked through the bootstrap export, while the artifact
basename is a packaging and loading concern. Separating them gives consumers the required deployment
control without making build-graph references or ABI symbols configurable.

There is no released ABI version 1 compatibility baseline, so the default can be corrected before
consumers depend on it. Configuring the target itself provides no additional consumer outcome and
would expand the mutable contract.

## Evidence and links

- [ADR-0001: Establish a stable C interoperability ABI](ADR-0001-stable-c-interop-abi.md)
- [C interoperability ABI major 1](../interop-abi-v1.md)

## Consequences and accepted trade-offs

- Generation without the new option changes from `ted_toolkit_occt_abi_v1` to
  `ted_toolkit_occt`; repository consumers and documentation must migrate together.
- Consumers can choose a product-specific native filename but must use the same configured name in
  their deployment and loading configuration.
- ABI-major filename coexistence is no longer automatic. Consumers that need side-by-side majors
  must configure distinct basenames and own collision avoidance.
- Generated-project validation becomes part of the public generator behavior.
- Documentation must distinguish the stable default basename from the ABI identity.

## Downstream delivery constraints

- Use `ted_toolkit_occt` as the default artifact basename and preserve
  `ted_toolkit_occt_abi_v1` as the internal CMake target for ABI major 1.
- Apply the configured basename only to the native target's platform output name.
- Reject invalid configuration before writing any generated project artifact.
- Preserve every ABI-major-1 header, symbol, version, ownership, and compatibility rule from
  ADR-0001.
- Keep repository native and managed boundary proof valid with the default, and prove that a valid
  custom basename produces the requested loadable artifact without changing its exports.
- Document that the configured value excludes platform prefixes and extensions.

## Exit requirements

Any replacement must preserve deterministic ABI-major discovery and compatibility validation even
when artifact filenames differ between consumers.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess whether loading configuration belongs in generated managed bindings. | TedToolkit.Occt maintainers | Before the managed invocation layer becomes a supported public surface. | Open |
| Reassess the naming boundary if one generated project must emit several differently named copies of the same ABI target. | TedToolkit.Occt maintainers | When that packaging requirement appears. | Open |
