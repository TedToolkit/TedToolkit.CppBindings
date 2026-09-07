# CGAL provider

The CGAL provider owns finite-profile generation, CGAL-specific runtime semantics, and ready-to-use
platform packages without adding CGAL policy to Shared.

The first maintained profile is `epick-windows-v1`. Its Generator package embeds the CGAL 6.2
public-header snapshot and explicitly closes the selected EPICK class and free-function templates.
Every installed header and compiler-observed source declaration receives a source disposition, and
every explicit finite candidate is admitted or rejected with a stable proof. Open templates remain
source-visible but are not candidates. This is deliberately not a claim over CGAL's unbounded
template space.

| Project | Responsibility |
| --- | --- |
| `TedToolkit.CppBindings.Cgal.Generator` | Resolve the locked vcpkg installation and emit deterministic source, candidate, admission, managed, and native inventories through Shared |
| `TedToolkit.CppBindings.Cgal.Runtime` | Project CGAL failures and finite polymorphic native results without OCCT semantics |
| `TedToolkit.CppBindings.Cgal.Windows` | Carry the generated `win-x64` EPICK assembly, native wrapper, imported dependencies, and notices |

Only the Generator project exists until the corresponding independently verified delivery adds the
Runtime and Windows projects.
