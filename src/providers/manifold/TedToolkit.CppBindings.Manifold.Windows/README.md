# TedToolkit.CppBindings.Manifold.Windows

Windows x64 bindings for the locked Manifold 3.5.2 finite profile.

Create an owned value with `Manifold.Create`, then use `Status`, `Boolean`, `Translate`, `NumTri`,
and `GetMesh`. Input spans are copied during the call and are never retained. Returned
`ManifoldMeshData` arrays are fresh provider-owned managed arrays. Dispose every `Owned<Manifold>`
value; references obtained from an owner remain valid only while that owner is alive and unchanged.

This package has no dependency on the OCCT, CGAL, or FCL provider families and supplies no
provider-to-provider conversions.

## Agent skill

The `preserve-cpp-binding-lifetimes` skill ships in `TedToolkit.CppBindings.Runtime`, which this package restores as a dependency. If your project references only this package, get the resolved `TedToolkit.CppBindings.Runtime` version from `dotnet package list --include-transitive` and import the skill with `dotnet-package-skills install --package TedToolkit.CppBindings.Runtime@<resolved-version>`.
