# Manifold provider

The Manifold provider owns the finite `manifold-3.5.2-windows-v2` profile, its diagnostic Runtime,
and its independently consumable Windows package. The profile covers mesh construction, status,
Boolean operations, translation, triangle count, and mesh output for the maintained Windows scope.

| Project | Responsibility |
| --- | --- |
| `TedToolkit.CppBindings.Manifold.Generator` | Supply the locked profile and semantic facts to Shared plan construction and paired emission |
| `TedToolkit.CppBindings.Manifold.Runtime` | Expose the Manifold entry point over Shared diagnostic consumption and common exception projection; no local extension is currently defined |
| `TedToolkit.CppBindings.Manifold.Windows` | Carry the generated assembly, unique native module, exact DLL closure, and notices |

`Owned<Manifold>` is the only owning projection. Input spans are borrowed only during
`Manifold.Create`; output mesh arrays are fresh managed allocations. The provider neither references
other geometry providers nor supplies collection or geometry conversion APIs. Repository builds
materialize this provider through the shared
`src/tools/TedToolkit.CppBindings.Windows.Generation.Tool` host.
