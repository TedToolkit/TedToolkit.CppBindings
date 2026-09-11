# ADR-006: Centralize common native-error projection and keep Provider extensions local

- Status: Accepted
- Date: 2026-09-09
- Decision owner: TedToolkit maintainers
- Decision scope: Native-error category numbering, generated catch emission, managed diagnostic
  consumption, managed exception ownership, and Provider-local error extensions.
- Applicable product intent: None
- Applicable principles: [Repository design principles](../principles/README.md), especially GEN-01
  and GEN-04
- Related ADRs: [ADR-002](ADR-002-cpp-bindings-platform.md),
  [ADR-003](ADR-003-native-function-table-bootstrap.md), and
  [ADR-004](ADR-004-provider-extension-and-cgal-profile.md)
- Supersedes: The provider-neutral native-error projection ownership described by ADR-004; ADR-004
  otherwise remains in force.
- Superseded by: None
- Approval source: The maintainer approved the revised Shared native-error contract and explicitly
  continued implementation in the Codex task on 2026-09-09 with “没错，就是这样。我同意，然后开始修改！”.

## Decision at a glance

Shared Generator and Runtime own one provider-neutral native-error contract. Shared kinds are fixed
at 0 through 8 and 255. Values 9 through 254 belong to each Provider independently, so two Providers
may assign the same value to unrelated local failures. Provider catches precede Shared catches, and
Provider Runtime projection sees only otherwise-unrecognized local values.

## Context and decision question

Every Provider Runtime currently repeats UTF-8 diagnostic copying, native cleanup, fallback behavior,
and common exception mapping. Provider Generators also repeat standard C++ catch mappings. These
copies have already drifted: OCCT assigned the lower identifier to its local `Standard_Failure`, and
CGAL mapped both overflow and underflow to kind 7 even though its Runtime interpreted that kind as
arithmetic failure.

The stable part of the contract is not provider policy. Argument, range, arithmetic, invalid-state,
null-object, allocation, overflow, standard C++, and unknown failures mean the same thing for every
generated binding. Native-library failures such as OCCT `Standard_Failure` remain local. The decision
is where the shared lifecycle and numbering authority ends and a Provider extension begins.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | One semantic authority emits every provider-neutral layer | GEN-01 | Must |
| Hard constraint | Shared never branches on or depends on a Provider | GEN-04 and ADR-004 | Must |
| Hard constraint | Diagnostic storage is cleared exactly once by the native module that allocated it | ADR-003 and current native carrier contract | Must |
| Hard constraint | Provider extension identifiers need no cross-Provider uniqueness | Maintainer decision | Must |
| Decision driver | Common failure types should be catchable without knowing the originating Provider | Maintainer decision | High |
| Decision driver | A new Provider should supply only genuine native-library failure semantics | Current duplication audit | High |

## Options considered

| Option | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- |
| Keep complete projection and catch tables in every Provider | No | Preserves duplication and permits numeric and behavioral drift | Rejected |
| Share only diagnostic cleanup while Providers map every kind | Partly | Still leaves several common numbering and exception authorities | Rejected |
| Register every Provider extension in one global Shared catalog | No | Couples Shared to Providers and imposes unnecessary global numbering | Rejected |
| Fix common categories in Shared and delegate only local values | Yes | Intentionally replaces Provider-prefixed common exception types | Selected |

## Decision

The native carrier remains the existing type-independent record containing an integer kind and
diagnostic pointers. It does not expose Provider policy. Shared owns these exact meanings:

| Kind | Shared meaning |
| ---: | --- |
| 0 | None |
| 1 | Argument |
| 2 | ArgumentOutOfRange |
| 3 | Arithmetic |
| 4 | InvalidOperation |
| 5 | NullObject |
| 6 | OutOfMemory |
| 7 | Overflow |
| 8 | StandardException |
| 255 | Unknown |

Kinds 9 through 254 are local to one Provider Generator/Runtime pair. Their values are not globally
registered and need not be unique across Providers. A Provider cannot override a Shared kind. An
otherwise-unrecognized nonzero value becomes the Shared unknown-native exception after the matching
Provider extension declines it.

Shared Generator owns this exact ordered standard C++ catch sequence:

| Order | Caught native type | Shared kind |
| ---: | --- | ---: |
| 1 | `std::bad_alloc` | 6 |
| 2 | `std::out_of_range` | 2 |
| 3 | `std::overflow_error` | 7 |
| 4 | `std::underflow_error` | 3 |
| 5 | `std::invalid_argument` | 1 |
| 6 | `std::domain_error` | 1 |
| 7 | any other `std::logic_error` | 4 |
| 8 | any other `std::exception` | 8 |
| 9 | any other thrown value (`catch (...)`) | 255 |

Kinds 5 and any other common kind not produced by that table are set by generated validation or by
a Provider typed catch whose native-library meaning matches the Shared category. A Provider profile
supplies only typed catches required by its native library; those catches may target a Shared kind or
a Provider-local kind without changing either meaning. They are emitted before the Shared sequence
so a Provider-specific subtype retains its intended category. OCCT maps `Standard_Failure` to local
kind 9, while an ordinary `std::exception` maps to Shared kind 8. The Shared table itself maps CGAL
overflow to 7 and underflow to 3; CGAL does not repeat those catches.

Shared Runtime owns strict UTF-8 diagnostic copying, fallback text, common kind interpretation, and
the exact-once call to the originating module's clear export. It performs cleanup even if diagnostic
decoding or Provider projection fails, and diagnostic loss never converts failure into success. Its
public generated-only projection contract accepts the originating clear function and an optional
Provider extension. The extension receives copied diagnostics and only an otherwise-unrecognized
local kind; it cannot own or clear native diagnostic memory.

Shared Runtime also owns this exact public exception surface. Constructors used by projection are
not public.

| Contract or kind | Public Shared type and base |
| --- | --- |
| Common diagnostics | `INativeException`, with read-only `string? NativeTypeName` and `string? NativeStackTrace` |
| Provider-neutral base | abstract `NativeException : Exception, INativeException` |
| 1 | sealed `NativeArgumentException : ArgumentException, INativeException` |
| 2 | sealed `NativeArgumentOutOfRangeException : ArgumentOutOfRangeException, INativeException` |
| 3 | sealed `NativeArithmeticException : ArithmeticException, INativeException` |
| 4 | sealed `NativeInvalidOperationException : InvalidOperationException, INativeException` |
| 5 | sealed `NativeNullObjectException : NativeException` |
| 6 | sealed `NativeOutOfMemoryException : OutOfMemoryException, INativeException` |
| 7 | sealed `NativeOverflowException : OverflowException, INativeException` |
| 8 | sealed `NativeStandardException : NativeException` |
| 255 or unresolved nonzero | sealed `NativeUnknownException : NativeException` |

Provider-specific failures retain Provider exception types and implement `INativeException`.
Provider-prefixed copies of common exceptions are removed without compatibility shims because the
packages have not established a published compatibility baseline.

The raw discriminator is generated plumbing, not a consumer operation result or a second public error
classification. Public visibility required for independently generated assemblies is marked as
generated-only and remains absent from generated consumer-facing operation signatures.

## Consequences and accepted trade-offs

- Common native failures become consistently catchable through Shared exception types across all
  Providers; source that caught an unreleased Provider-prefixed common type must update.
- Adding a common kind requires an architecture revision. Adding a Provider-local failure requires
  only the matching Provider Generator/Runtime pair and component proof.
- Local extension numbers are meaningful only together with the producing module and matching
  Runtime; logs and tests must not interpret them globally.
- Shared gains lifecycle and mapping logic but no Provider name, native type, or dependency.
- The selected order corrects the existing OCCT and CGAL drift: `StandardException` is 8,
  `OcctFailure` is 9, overflow is 7, and underflow is 3.

## Downstream delivery constraints

- Generate Provider typed catches before the immutable Shared standard catch sequence.
- Keep the fixed Shared table identical in generated native code, Shared Runtime, tests, and current
  architecture documentation without duplicating implementation authority.
- Clear diagnostic storage exactly once through the originating native module on every nonzero path,
  including malformed UTF-8, extension failure, and unknown-kind fallback.
- Keep Provider projection limited to local exception construction; it must not reinterpret Shared
  kinds or duplicate diagnostic memory ownership.
- Prove overlapping local values with at least two Provider-local test projections rather than a
  repository-wide uniqueness rule.

## Exit requirements

A replacement must retain one provider-neutral mapping authority, exact-once originating-module
cleanup, Provider-independent Shared packages, and independently extensible Provider-local failure
semantics. Supersede this ADR before changing a Shared number or introducing a global extension
registry.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Migrate every Provider to the Shared projection | TedToolkit maintainers | Before completing the active unified-provider change | Open |
| Reassess the common table | TedToolkit maintainers | A failure category must have the same meaning for at least two Providers but cannot use an existing Shared kind | Open |
| Reassess extension transport | TedToolkit maintainers | A Provider cannot express a local failure using the current copied diagnostics | Open |
