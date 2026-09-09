# CGAL provider

The CGAL provider owns finite-profile generation, CGAL-specific runtime semantics, and ready-to-use
platform packages without adding CGAL policy to Shared.

The first maintained profile is `epick-windows-v1`. Its Generator discovers CGAL public headers
from the selected vcpkg installation and explicitly closes the selected EPICK class and
free-function templates.
Every installed header and compiler-observed source declaration receives a source disposition.
Reachable direct non-template declarations and the explicitly closed profile instances form the
finite candidate set; each is admitted or rejected with a stable proof. Open templates and their
dependent members remain source-visible but are not candidates. This is deliberately not a claim
over CGAL's unbounded template space.

| Project | Responsibility |
| --- | --- |
| `TedToolkit.CppBindings.Cgal.Generator` | Resolve the locked vcpkg installation and emit deterministic source, candidate, admission, managed, and native inventories through Shared |
| `TedToolkit.CppBindings.Cgal.Runtime` | Project CGAL failures and finite polymorphic native results without OCCT semantics |
| `TedToolkit.CppBindings.Cgal.Windows` | Carry the generated `win-x64` EPICK assembly, matched native wrapper, recursively resolved dependencies, and notices |
| `TedToolkit.CppBindings.Cgal.Generator.Tool` | Provide the repository-local executable host used to materialize a Generator plan; not a package |

The Generator and Runtime packages are independently consumable. The Windows package depends on
Runtime but does not carry or execute Generator tooling on consumer machines.
