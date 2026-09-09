# C++ bindings platform architecture

- Status: Active
- Owner: TedToolkit maintainers
- Scope and system boundary: Repository identity and the dependency, package, namespace, ownership,
  analyzer, and provider boundaries for generated object-oriented C++ bindings.
- Applicable product intent: None
- Governing principles: [Repository design principles](../principles/README.md)
- Related ADRs: [ADR-002](../adr/ADR-002-cpp-bindings-platform.md),
  [ADR-003](../adr/ADR-003-native-function-table-bootstrap.md),
  [ADR-004](../adr/ADR-004-provider-extension-and-cgal-profile.md),
  [ADR-005](../adr/ADR-005-provider-native-package-isolation.md),
  [ADR-006](../adr/ADR-006-shared-native-error-projection.md), and superseded
  [ADR-001](../adr/ADR-001-native-release-binding/README.md)
- Approval source: The maintainer explicitly approved this direction in the Codex task on
  2026-08-26 and approved the shared/provider source topology and finite EPICK-based CGAL provider
  extension on 2026-09-05.

## Current architecture

`TedToolkit.CppBindings` is the product and repository family. Generic mechanisms occupy the root
namespace and provider semantics occupy a provider segment:

```text
shared: TedToolkit.CppBindings.Generator + Runtime
├── OCCT: Generator + Runtime + SourceGenerators (internal)
│   └── TedToolkit.CppBindings.Occt.Windows
└── CGAL: Generator + Runtime + Generator.Tool (internal)
    └── TedToolkit.CppBindings.Cgal.Windows

consumer tool: TedToolkit.CppBindings.Analyzers
```

Source mirrors that dependency boundary: shared Generator and Runtime projects live below
`src/shared`, provider projects live below `src/providers/<provider>`, and provider-neutral
development tools live below `src/tools`.

The generic core has no OCCT- or CGAL-specific parsing/classification rules, concrete native-library
identity, or provider declaration-set knowledge. It owns compiler-facing type transport, the
normalized semantic model and dependency closure, layout and lifecycle evidence vocabulary,
provider-neutral metadata and diagnostics, paired managed/native emission, native-library bootstrap
and function-table emission, direct-storage `Owned<T>`, and proved Windows generation primitives.
A provider supplies declaration roots and finite template profiles, header discovery, classification
and lifetime rules, provider-specific native dependencies and local exceptions, generated declarations,
default artifact identity, and concrete platform packages.

Shared Runtime owns provider-neutral native-error diagnostic consumption and common exception
projection. Shared Generator fixes common standard C++ catches and error kinds 0 through 8 and 255.
Each Provider may interpret values 9 through 254 only within its matching Generator/Runtime pair;
local values may overlap across Providers. Provider Runtime packages therefore own only genuine
native-library failures, never copies of common argument, arithmetic, allocation, standard, or
unknown-native exception behavior.

Generated value categories are unmanaged structs rather than owner classes. Borrowing is an
operation-level fact represented directly by generated signatures, `ref T`, or an approved pointer;
there is no public `Borrowed<T>` wrapper. Consumer analyzers provide suppressible best-effort
guidance and are referenced directly as a standalone package. Provider source generators are build
tools and do not share an assembly or package responsibility with consumer diagnostics.

Namespaces follow responsibility: generic public APIs use `TedToolkit.CppBindings`; provider APIs
use `TedToolkit.CppBindings.Occt` or `TedToolkit.CppBindings.Cgal`. `.Windows` identifies a concrete
package and assembly, not a generated API namespace. The internal CGAL Generator Tool only hosts
repository builds and is not a package or consumer dependency.

## Current CGAL target

The CGAL provider delivers real `TedToolkit.CppBindings.Cgal.Generator`, `.Runtime`, and `.Windows`
packages below `src/providers/cgal`, with public APIs under
`TedToolkit.CppBindings.Cgal`. Empty symmetric packages remain forbidden.

The accepted default CGAL Windows artifact is profile-complete rather than universally
template-complete. It pins CGAL 6.2, `win-x64`, the proved MSVC toolchain, and EPICK, then attempts
every declaration in the finite profile's 2D/3D root and dependency closure. Every representable
declaration is emitted; every rejection names its narrow failed semantic or ABI proof. Generator
callers may select additional finite kernel and closed-template profiles without adding provider
knowledge to Shared.

This record governs product identity, package allocation, and generic/provider dependency direction.
The current generated-binding and analyzer-boundary records govern exact layout, generation, native
loading, ownership behavior, diagnostics, and failure boundaries under the same platform identity.

Each ready-to-use provider package is self-contained for its RID: it carries the exact generated
managed/native pair and only the binding module's recursively reachable non-system DLL imports.
Provider packages never copy a vcpkg runtime directory. When independently published providers
contain the same native filename, verification accepts the package set only when every copy is
byte-identical. The CGAL package additionally carries CGAL/GMP/MPFR notices; a notice does not make
an unreferenced native DLL part of the runtime closure. Its isolated consumer does not acquire
Generator, Clang, or OCCT packages.

## Constraints for change design

- Migrate repository, solution, project, assembly, package, namespace, diagnostic, documentation,
  CI, source-link, and GitHub identities as one recoverable breaking change.
- Keep Value, Owned, Handle, native layout, same-library cleanup, Provider-specific exception, and
  fail-closed OCCT behavior observable across the migration; common failures use the approved
  Shared `Native*Exception` family.
- Publish Analyzers separately and require direct consumer reference with `PrivateAssets="all"`;
  do not depend on transitive analyzer flow or embed the analyzer in Runtime.
- Keep provider projects dependent on generic contracts and prohibit the reverse dependency with a
  verifiable project/package graph rule.
- Preserve existing OCCT public behavior while extracting provider-neutral semantic machinery from
  its current physical location.
- Require deterministic admitted and unsupported inventories for every finite CGAL profile; do not
  claim or attempt an unbounded set of template instantiations.
- Give CGAL Runtime only real CGAL-specific contracts, including local check/exception and
  polymorphic-result semantics; keep common native-error projection in Shared Runtime and concrete
  generated declarations in the Windows artifact.
- Give every provider binding module a unique basename, package only its recursive app-local import
  closure, and fail package-set verification on differing same-name native assets.
- A GitHub rename is an external operational handoff with maintainer ownership, preflight,
  post-rename evidence, and recovery. It is not a development work item.

## Decision links and exceptions

[ADR-002](../adr/ADR-002-cpp-bindings-platform.md) selects the platform/provider organization.
[ADR-003](../adr/ADR-003-native-function-table-bootstrap.md) governs native loading and
function-table lifetime. [ADR-004](../adr/ADR-004-provider-extension-and-cgal-profile.md) selects
the shared/provider source topology, semantic provider contract, and finite CGAL Windows profile;
[ADR-006](../adr/ADR-006-shared-native-error-projection.md) governs the common native-error contract,
Shared managed projection, and Provider-local extension boundary;
ADR-001 remains historical evidence for the superseded loader decision.

## Review triggers

- A generic core project requires provider-specific declaration, symbol, layout, exception, or
  ownership knowledge.
- A Provider needs to reinterpret a Shared error kind or a global numbering rule is proposed for
  Provider-local extensions.
- A second provider cannot reuse the core without modifying a provider-neutral public contract.
- Shared code branches on a provider name, declaration, namespace, native dependency, or default
  artifact identity.
- A CGAL package silently omits a profile declaration or advertises an unbounded template surface.
- A public ownership category beyond Value, Owned, and provider-specific Handle is proposed.
- A provider package requires a native filename that conflicts byte-for-byte with another provider,
  or cannot resolve its app-local import closure from its pinned toolchain and installation.
- A new OS, ABI, architecture, RID, generic platform package, or analyzer distribution mechanism is
  proposed.
- A published compatibility baseline makes coordinated identity replacement unacceptable.
