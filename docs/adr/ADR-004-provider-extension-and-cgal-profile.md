# ADR-004: Extend the shared binding platform through finite provider profiles

- Status: Accepted
- Date: 2026-09-05
- Decision owner: TedToolkit maintainers
- Decision scope: Source topology, provider dependency direction, reusable semantic generation,
  provider package responsibilities, and the default CGAL Windows binding profile.
- Applicable product intent: None
- Applicable principles: [Repository design principles](../principles/README.md)
- Related ADRs: [ADR-002](ADR-002-cpp-bindings-platform.md) and
  [ADR-003](ADR-003-native-function-table-bootstrap.md)
- Supersedes: None
- Superseded by: None
- Approval source: The maintainer approved the shared/provider folder structure and the default
  EPICK-based finite CGAL profile in the Codex task on 2026-09-05, then explicitly authorized the
  design and delivery work with “你先开始吧”.

## Decision at a glance

Organize reusable binding mechanisms under one shared platform and each native library under one
provider boundary. Every provider supplies real Generator, Runtime, and Windows packages. The CGAL
Windows package generates the complete representable closure of a finite, explicit template
profile, initially the predefined EPICK kernel across its supported 2D and 3D surface.

## Context and decision question

The repository already publishes provider-neutral Generator and Runtime contracts, but most of the
semantic model, Clang transport analysis, template closure, layout admission, and C#/C++ rendering
still resides physically and nominally in the OCCT Generator. Copying that implementation for CGAL
would create two semantic authorities and make future providers repeat the same extraction.

CGAL also differs materially from OCCT. It is predominantly header-only and exposes an open-ended
template and concept-model API. A Windows package cannot instantiate an infinite surface, so
“generate as much as possible” requires a finite profile and an objective completeness boundary.
The decision is how to separate reusable generation from provider policy and how to define a
truthful, extensible CGAL package surface.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | One semantic model remains the authority for every generated layer | [GEN-01](../principles/README.md) | Must |
| Hard constraint | Shared code contains no OCCT, CGAL, concrete declaration-set, or native-library policy | [ADR-002](ADR-002-cpp-bindings-platform.md) and [GEN-04](../principles/README.md) | Must |
| Hard constraint | Both OCCT and CGAL remain usable and receive real automated verification | Maintainer request | Must |
| Hard constraint | No broad declaration category is excluded when a concrete capability is representable | [GEN-05](../principles/README.md) | Must |
| Decision driver | Future providers should add policy and inputs rather than copy the semantic engine | Maintainer request and current source audit | High |
| Decision driver | CGAL templates require a finite set of closed instantiations for a distributable binary package | CGAL concept-model and template API | High |
| Decision driver | Every advertised provider package must own real behavior rather than exist for empty symmetry | [ADR-002](ADR-002-cpp-bindings-platform.md) | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Copy the OCCT Generator and rename it for CGAL | Current OCCT implementation; high confidence | No | Fast initial duplication creates two model and emitter authorities | Rejected |
| Put provider branches and names throughout one shared Generator | Current reusable stages show this is possible; high confidence | No | Shared packages would acquire provider policy and grow conditional behavior | Rejected |
| Extract a provider-neutral semantic engine with explicit provider policy inputs | Current models and emitters are largely declaration-agnostic; high confidence | Yes | Requires a controlled refactor before the second provider | Selected |
| Claim all public CGAL templates without fixing instantiations | CGAL template surface; high confidence | No | The advertised surface is unbounded and cannot be compiled or proved | Rejected |
| Ship a finite default profile and support additional explicit profiles | CGAL predefined kernels and compiled wrapper model; high confidence | Yes | Completeness is profile-relative rather than universal | Selected |

## Decision

Source is organized by responsibility:

```text
src/
├── shared/
│   ├── TedToolkit.CppBindings.Generator
│   └── TedToolkit.CppBindings.Runtime
├── providers/
│   ├── occt/
│   │   ├── TedToolkit.CppBindings.Occt.Generator
│   │   ├── TedToolkit.CppBindings.Occt.Runtime
│   │   └── TedToolkit.CppBindings.Occt.Windows
│   └── cgal/
│       ├── TedToolkit.CppBindings.Cgal.Generator
│       ├── TedToolkit.CppBindings.Cgal.Generator.Tool (internal host)
│       ├── TedToolkit.CppBindings.Cgal.Runtime
│       └── TedToolkit.CppBindings.Cgal.Windows
└── tools/
    └── provider-neutral development tools
```

The shared Generator owns compiler-facing declaration and type transport, normalized semantic
models, dependency closure, layout and lifecycle evidence vocabulary, deterministic naming,
admission diagnostics, native project primitives, and paired managed/native emission. These
mechanisms accept provider metadata and policies; they never inspect a provider name or known
provider declaration.

A provider owns declaration roots, header discovery, finite template profiles, type and lifetime
classification rules, provider-specific native dependencies, exception/check projection,
unsupported evidence, default namespace and native artifact identity. Provider code depends on the
shared Generator and Runtime; shared code never depends on a provider. OCCT retains its intrusive
`Standard_Transient` and `handle<T>` semantics. CGAL does not inherit those semantics.

Every provider delivers three real package responsibilities:

- `*.Generator` configures and executes provider-specific discovery and normalization through the
  shared semantic engine.
- `*.Runtime` owns only handwritten, platform-neutral semantics that are genuinely specific to the
  provider. For CGAL this includes its managed check/exception and polymorphic-result contracts;
  declaration-specific layouts and operations remain generated.
- `*.Windows` contains the exact-match generated managed assembly and native wrapper for the pinned
  Windows toolchain matrix and references the matching shared and provider Runtime packages.

The default CGAL Windows profile pins CGAL 6.2, `win-x64`, the proved MSVC toolchain, and
`CGAL::Exact_predicates_inexact_constructions_kernel` (EPICK). It admits the supported 2D and 3D
declarations and algorithms reachable from the profile's explicit public roots and closed template
instances. Generator callers may define additional finite kernel and template profiles.

Completeness is measured against the selected profile: generation attempts every public declaration
and dependency in its closure, emits every declaration whose layout, transport, invocation,
lifetime, and exception behavior can be proved, and reports each rejected declaration with the
narrowest failed proof. Silent omission and broad template, package, or namespace deny-lists are
not allowed.

The CGAL native wrapper compiles header-defined operations into the same inseparable managed/native
function-table artifact model selected by ADR-003. Non-header-only dependencies such as GMP and
MPFR remain target-native dependencies and are packaged or resolved according to the proved Windows
artifact contract.

## Why this decision now

A second provider is the first concrete test of the shared boundary anticipated by ADR-002. The
source audit shows that the current OCCT Generator combines reusable semantic machinery with a
smaller set of genuine OCCT policies. Extracting the reusable machinery now prevents CGAL from
creating a second model and emitter implementation. A finite EPICK profile gives the Windows
package a reproducible completeness claim while leaving the Generator open to additional kernels.

## Evidence and links

- [C++ bindings platform architecture](../architecture/cpp-bindings-platform.md)
- [Generated binding architecture](../architecture/generated-binding-system.md)
- [CGAL 6.2 manual](https://doc.cgal.org/latest/Manual/index.html)
- [CGAL EPICK reference](https://doc.cgal.org/latest/Kernel_23/classCGAL_1_1Exact__predicates__inexact__constructions__kernel.html)
- [CGAL header-only usage](https://doc.cgal.org/latest/Manual/usage.html)
- [CGAL checks and exception behavior](https://doc.cgal.org/latest/Manual/devman_checks.html)

## Consequences and accepted trade-offs

- Existing project paths, solution entries, build scripts, documentation links, and test paths move,
  while assembly, package, namespace, and public API identities remain stable.
- The shared Generator grows a real semantic provider contract rather than an OCCT-shaped adapter
  around completed text output.
- OCCT must prove unchanged generation, packaging, lifetime, and native behavior after extraction.
- The CGAL Windows package is broad but deliberately profile-bounded; adding a kernel or closed
  template set changes the advertised generated surface and requires matching proof.
- CGAL's header-only implementation does not remove the generated native artifact because .NET
  still needs compiler-instantiated wrappers and same-toolchain exception and lifetime behavior.
- A provider Runtime package is justified by provider semantics, not naming symmetry. If a proposed
  provider has no such semantics, this decision does not require an empty Runtime package.

## Downstream delivery constraints

- Complete the shared/OCCT extraction and prove existing OCCT behavior before the CGAL delivery
  consumes that boundary.
- Enforce the one-way shared-to-provider dependency rule structurally.
- Keep provider policies out of shared namespaces, assemblies, and package dependencies.
- Preserve OCCT public assembly, package, namespace, generation, ownership, exception, and Windows
  artifact behavior during the source move and semantic extraction.
- Define every CGAL Windows profile as deterministic inputs and publish its admitted and unsupported
  declaration inventories.
- Provide focused unit/component proof for shared semantic behavior and each provider policy, plus
  real Windows generation, native compilation, package-consumer, and native-call proof for OCCT and
  CGAL.
- Do not advertise universal CGAL coverage or silently substitute one kernel for another.

## Exit requirements

A replacement must preserve one model authority, provider-independent shared packages, truthful
finite CGAL surface claims, exact-match native artifacts, and independent verification of both
providers. Supersede this ADR before introducing provider conditionals into shared code or an
unbounded generated package claim.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Extract reusable semantic generation and reorganize source | TedToolkit maintainers | Before CGAL provider delivery | Open |
| Deliver and verify the default CGAL Windows profile | TedToolkit maintainers | After the shared extraction is complete | Open |
| Reassess CGAL Runtime responsibilities | TedToolkit maintainers | A proposed contract has no provider-specific runtime semantics | Open |
| Reassess the default profile | TedToolkit maintainers | Consumers require exact constructions, another kernel, or a materially different CGAL package family | Open |
| Reassess dependency packaging | TedToolkit maintainers | CGAL, GMP, MPFR, or vcpkg changes the supported native dependency model | Open |
