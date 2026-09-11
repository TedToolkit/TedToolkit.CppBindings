# TedToolkit.CppBindings.Manifold.Windows

Windows x64 bindings for the locked Manifold 3.5.2 finite profile.

Create an owned value with `Manifold.Create`, then use `Status`, `Boolean`, `Translate`, `NumTri`,
and `GetMesh`. Input spans are copied during the call and are never retained. Returned
`ManifoldMeshData` arrays are fresh provider-owned managed arrays. Dispose every `Owned<Manifold>`
value; references obtained from an owner remain valid only while that owner is alive and unchanged.

This package has no dependency on the OCCT, CGAL, or FCL provider families and supplies no
provider-to-provider conversions.
