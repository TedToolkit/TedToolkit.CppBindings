# ADR-002: Organize C++ bindings as a shared platform with library providers

- Status: Accepted
- Date: 2026-08-26
- Decision owner: TedToolkit maintainers
- Decision scope: Repository identity, package and namespace hierarchy, generic/provider dependency
  direction, ownership abstractions, analyzer distribution, and provider growth.
- Applicable product intent: None
- Applicable principles: [Repository design principles](../principles/README.md)
- Supersedes: None
- Superseded by: None
- Approval source: The maintainer explicitly approved the `TedToolkit.CppBindings` identity,
  generic/provider structure, standalone analyzer package, and no-`Borrowed<T>` direction in the
  Codex task on 2026-08-26.

## Decision at a glance

Use one `TedToolkit.CppBindings` repository and product family with provider-neutral Generator,
Runtime, and Analyzers packages beneath provider-specific OCCT and future CGAL packages.

## Context and decision question

The repository began as an OCCT-only generator, but its semantic generation pipeline, direct-storage
RAII owner, metadata, Windows defaults, and lifetime diagnostics are useful to other object-oriented
C++ libraries. Keeping them under `TedToolkit.Occt` would make a CGAL provider depend on OCCT names
or duplicate mechanisms. The decision is where shared mechanisms, provider semantics, platform
defaults, namespaces, and diagnostics belong before a second provider exists.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Generic code must have no OCCT, CGAL, generated-declaration, or native-library knowledge | [GEN-01 and GEN-04](../principles/README.md) | Must |
| Hard constraint | OCCT must retain exact layout, Value, Owned, Handle, same-library cleanup, and fail-closed behavior | [Generated binding architecture](../architecture/generated-binding-system.md) | Must |
| Decision driver | Another provider must reuse shared mechanisms without inheriting OCCT semantics | Planned CGAL adoption | High |
| Decision driver | Consumer diagnostics must be independently selectable and provider-neutral | [Runtime/analyzer boundary](../architecture/runtime-analyzer-boundary.md) | High |
| Decision driver | The public surface should stay close to C++ and avoid a wrapper for every borrowed reference | [GEN-03](../principles/README.md) | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep one OCCT-specific repository | Existing implementation; high confidence | No | Forces reuse through OCCT identities or duplication | Rejected |
| Create one independent repository per native library | Common package layout; medium confidence | Partly | Avoids provider coupling but duplicates generator, Runtime, analyzer, and Windows policy | Rejected |
| One shared platform with provider packages | Current mechanisms separate naturally by declaration knowledge; high confidence | Yes | Requires a coordinated breaking rename and stricter dependency boundaries | Selected |
| Put every ownership category in generic Runtime | Current OCCT `Handle<T>` depends on `Standard_Transient`; high confidence | No | Leaks provider semantics into the core | Rejected |

## Decision

- Rename the product and GitHub repository to `TedToolkit.CppBindings`.
- Publish provider-neutral `TedToolkit.CppBindings.Generator`,
  `TedToolkit.CppBindings.Runtime`, and `TedToolkit.CppBindings.Analyzers` packages.
- Put OCCT parsing, classification, intrusive `Handle<T>`, exceptions, generated declarations, and
  ready platform artifacts under `TedToolkit.CppBindings.Occt.*`.
- Use `TedToolkit.CppBindings` for generic managed APIs and
  `TedToolkit.CppBindings.Occt` for OCCT managed APIs. Platform suffixes identify packages and
  assemblies, not generated API namespaces.
- Keep Value as generated unmanaged structs, place declaration-agnostic direct-storage `Owned<T>`
  in generic Runtime, and keep OCCT intrusive `Handle<T>` in OCCT Runtime.
- Do not add a public `Borrowed<T>` abstraction. Borrowing is represented by generated operation
  signatures, `ref T`, or an approved pointer, with suppressible analyzer guidance.
- Ship analyzers as a direct, standalone consumer dependency. Keep provider source generators
  separate from consumer diagnostics.
- Provide the proved Windows toolchain profile as a generic Generator default. Create concrete
  provider/platform packages only when they contain real bindings; do not scaffold empty symmetry.

## Why this decision now

The shared/provider boundary is clear while only one provider exists and before public CGAL or OCCT
binding packages establish incompatible identities. It follows the existing one-model,
exact-layout, representation/ownership separation, and declaration-agnostic Runtime principles.

## Evidence and links

- [Repository design principles](../principles/README.md)
- [Generated binding architecture](../architecture/generated-binding-system.md)
- [Runtime/analyzer boundary](../architecture/runtime-analyzer-boundary.md)
- [ADR-003 native function-table bootstrap](ADR-003-native-function-table-bootstrap.md)
- [Superseded ADR-001 native release binding](ADR-001-native-release-binding/README.md)

## Consequences and accepted trade-offs

The migration breaks current unreleased project, assembly, package, namespace, diagnostic, and
repository identities. Generic packages become reusable and provider packages remain semantically
honest, at the cost of more explicit projects and dependencies. Consumers must reference the
analyzer package directly when they want guidance. CGAL may use Value and Owned without inventing
an intrusive Handle category.

## Downstream delivery constraints

- Dependencies point from providers to the generic core, never from core to a provider.
- Each public contract has one package and namespace owner; compatibility aliases are not created
  for the unreleased old identity.
- Provider-specific source generation and provider-neutral consumer diagnostics remain separate.
- Repository rename, internal identity migration, validation, and recovery form one controlled
  migration with an explicit external operational handoff.
- A new provider, platform claim, public ownership category, or generic Windows package requires a
  separate approved decision or change.

## Exit requirements

A replacement must preserve an exportable provider boundary, explicit ownership and cleanup, and a
recoverable repository/package identity migration. Supersede this ADR rather than silently reversing
dependency direction or adding provider knowledge to the core.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Deliver the coordinated platform migration | TedToolkit maintainers | Approved migration change | Open |
| Reassess provider abstractions | TedToolkit maintainers | A second provider cannot use the core without provider-specific changes | Open |
| Reassess platform packaging | TedToolkit maintainers | A second OS, ABI, RID, or shared platform package has a concrete consumer | Open |
