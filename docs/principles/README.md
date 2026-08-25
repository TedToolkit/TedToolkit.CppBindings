# Repository design principles

## Scope and precedence

- Governed scope: architecture, implementation, and review decisions for TedToolkit.Occt source
  generation and generated native and managed binding artifacts.
- External hard constraints that take precedence: supported toolchain and platform requirements,
  C11 interoperability, explicit ownership and lifetime contracts, and same-library cleanup.
- Product intent: None currently recorded.
- Precedence: approved product intent guides principles; principles guide architecture design;
  approved architecture constrains change design; approved work items constrain implementation.

## Principle index

| ID | Title | Strength | Status | Owner | Review trigger | Document |
| --- | --- | --- | --- | --- | --- | --- |
| GEN-01 | Generate every binding layer from one semantic source | Required | Active | TedToolkit.Occt maintainers | A binding artifact cannot be derived without declaration-specific code or a generated output requires a manual patch | This file |

## Principles

### GEN-01: Generate every binding layer from one semantic source

- Status: Active
- Strength: Required
- Scope: The Generator and every generated C declaration, C++ adapter, managed transport/import,
  and public C# binding.
- Owner: TedToolkit.Occt maintainers
- Review trigger: A new output language or ABI backend is introduced; an operation appears to
  require declaration-specific executable code; or any generated artifact requires a manual patch.

#### Default

The parsed and normalized semantic model is the single source for every supported binding
operation. Generic validation, mapping, naming, and emission rules transform that model into all
native and managed artifacts in one coherent pipeline.

The Generator must not embed knowledge of a specific OCCT header, declaration, type, source
location, operation, final export symbol, adapter body, managed import, or public wrapper. It must
not maintain a manually curated per-operation catalog or copy handwritten binding source into the
generated output. Export names and every paired declaration and implementation are derived
deterministically from the same canonical operation identity.

#### Rationale

Declaration-specific source creates parallel authorities that can disagree while still compiling.
One semantic source prevents drift between the C contract, C++ implementation, managed import, and
public API; allows coverage to scale beyond a conformance sample; and makes regeneration and review
reproducible.

#### Practical implications

- Generator code may contain language syntax emitters, transport vocabulary, category-level type
  and ownership policies, compatibility rules, and deterministic naming algorithms.
- Declaration-specific exceptions, when unavoidable, are validated semantic metadata or mapping
  data consumed by the common pipeline; they are not handwritten C, C++, C#, or final symbols.
- One supported operation produces its complete native and managed chain from one model. If any
  required projection or implementation cannot be derived, the complete operation is reported as
  unsupported and no partial artifact is emitted.
- Generated artifacts are never edited by hand. A required correction changes the source model,
  mapping policy, validator, or emitter and is then regenerated.
- Tests prove cross-layer identity and completeness from generated outputs rather than comparing
  them with separately handwritten operation lists.

#### Exception route

Any declaration-specific executable binding, manually assigned final symbol, handwritten generated
artifact, or second operation authority requires an Accepted ADR before implementation. The ADR
must identify why the common semantic pipeline cannot represent the case, how cross-layer drift is
prevented, and the objective condition for removing the exception.

## Exception route

A proposed deviation from a Required principle needs an Accepted ADR before implementation.
Emergency changes that cannot satisfy this gate are not shipped as supported generated bindings.

## Maintenance

- Principle-set owner: TedToolkit.Occt maintainers
- Review cadence or objective review triggers: Review whenever the Generator adds an output layer,
  a declaration-specific mapping, or a manual generated-source step.
- Last reviewed: 2026-08-25
