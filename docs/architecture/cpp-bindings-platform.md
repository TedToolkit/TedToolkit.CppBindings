# C++ bindings platform architecture

- Status: Active
- Owner: TedToolkit maintainers
- Scope and system boundary: Repository identity and the dependency, package, namespace, ownership,
  analyzer, and provider boundaries for generated object-oriented C++ bindings.
- Applicable product intent: None
- Governing principles: [Repository design principles](../principles/README.md)
- Related ADRs: [ADR-002](../adr/ADR-002-cpp-bindings-platform.md),
  [ADR-003](../adr/ADR-003-native-function-table-bootstrap.md), and superseded
  [ADR-001](../adr/ADR-001-native-release-binding/README.md)
- Approval source: The maintainer explicitly approved this direction in the Codex task on
  2026-08-26.

## Current architecture

`TedToolkit.CppBindings` is the product and repository family. Generic mechanisms occupy the root
namespace and provider semantics occupy a provider segment:

```text
TedToolkit.CppBindings.Generator
TedToolkit.CppBindings.Runtime
TedToolkit.CppBindings.Analyzers
              ^
              |
TedToolkit.CppBindings.Occt.Generator
TedToolkit.CppBindings.Occt.Runtime
TedToolkit.CppBindings.Occt.SourceGenerators (internal build component)
              ^
              |
TedToolkit.CppBindings.Occt.Windows
```

The generic core has no OCCT, CGAL, native-library, or generated-declaration knowledge. It owns the
semantic-generation framework, provider-neutral metadata and diagnostics, direct-storage
`Owned<T>`, and the proved Windows generation defaults. A provider owns parsing and classification
rules, provider-specific lifetimes such as OCCT `Handle<T>`, exceptions, generated declarations,
and concrete platform packages.

Generated value categories are unmanaged structs rather than owner classes. Borrowing is an
operation-level fact represented directly by generated signatures, `ref T`, or an approved pointer;
there is no public `Borrowed<T>` wrapper. Consumer analyzers provide suppressible best-effort
guidance and are referenced directly as a standalone package. Provider source generators are build
tools and do not share an assembly or package responsibility with consumer diagnostics.

Namespaces follow responsibility: generic public APIs use `TedToolkit.CppBindings`; OCCT public
APIs use `TedToolkit.CppBindings.Occt`. `.Windows` identifies a concrete package and assembly, not a
generated API namespace. CGAL and other providers are added only by separate changes with real
deliverables; symmetric empty projects are not architecture.

This record governs product identity, package allocation, and generic/provider dependency direction
where older OCCT-specific architecture records still describe the pre-migration implementation.
Those records continue to govern exact layout, generation, native loading, ownership behavior, and
failure boundaries until the migration updates their terminology.

## Constraints for change design

- Migrate repository, solution, project, assembly, package, namespace, diagnostic, documentation,
  CI, source-link, and GitHub identities as one recoverable breaking change.
- Keep Value, Owned, Handle, native layout, same-library cleanup, exception, and fail-closed OCCT
  behavior observable across the migration.
- Publish Analyzers separately and require direct consumer reference with `PrivateAssets="all"`;
  do not depend on transitive analyzer flow or embed the analyzer in Runtime.
- Keep provider projects dependent on generic contracts and prohibit the reverse dependency with a
  verifiable project/package graph rule.
- A GitHub rename is an external operational handoff with maintainer ownership, preflight,
  post-rename evidence, and recovery. It is not a development work item.

## Decision links and exceptions

[ADR-002](../adr/ADR-002-cpp-bindings-platform.md) selects the platform/provider organization.
[ADR-003](../adr/ADR-003-native-function-table-bootstrap.md) governs native loading and
function-table lifetime; ADR-001 remains historical evidence for the superseded loader decision.

## Review triggers

- A generic core project requires provider-specific declaration, symbol, layout, exception, or
  ownership knowledge.
- A second provider cannot reuse the core without modifying a provider-neutral public contract.
- A public ownership category beyond Value, Owned, and provider-specific Handle is proposed.
- A new OS, ABI, architecture, RID, generic platform package, or analyzer distribution mechanism is
  proposed.
- A published compatibility baseline makes coordinated identity replacement unacceptable.
