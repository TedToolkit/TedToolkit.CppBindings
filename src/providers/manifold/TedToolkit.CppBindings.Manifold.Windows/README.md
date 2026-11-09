# TedToolkit.CppBindings.Manifold.Windows

Windows x64 bindings for the locked Manifold 3.5.2 finite profile.

Create an owned value with `Manifold.Create`, then use `Status`, `Boolean`, `Translate`, `NumTri`,
and `GetMesh`. Input spans are copied during the call and are never retained. Returned
`ManifoldMeshData` arrays are fresh provider-owned managed arrays. Dispose every `Owned<Manifold>`
value; references obtained from an owner remain valid only while that owner is alive and unchanged.

This package has no dependency on the OCCT, CGAL, or FCL provider families and supplies no
provider-to-provider conversions.

## Build from a local Manifold installation

Build and install upstream Manifold 3.5.2 for Windows x64 with the profile's MSVC toolset
`14.51.36231` and CMake `4.4.3`. A standalone build can use
`-DMANIFOLD_CROSS_SECTION=OFF -DMANIFOLD_PAR=OFF -DMANIFOLD_TEST=OFF -DMANIFOLD_DOWNLOADS=OFF`.
The install prefix must contain `include/manifold` and the installed `manifold::manifold` CMake
target. Place the upstream license in `share/manifold/copyright`. For a shared build, keep
its DLLs in the prefix's `bin` directory so the package can stage the binding DLL's runtime imports.

From the CppBindings repository root:

```powershell
dotnet build src/providers/manifold/TedToolkit.CppBindings.Manifold.Windows -c Release "-p:ManifoldInstallPrefix=C:\sdk\manifold"
```
