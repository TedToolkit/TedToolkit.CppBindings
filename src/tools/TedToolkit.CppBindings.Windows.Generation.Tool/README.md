# Windows generation tool

This non-packable tool is the single repository entry point for Windows binding generation.
It references the OCCT, CGAL, Manifold, and FCL Generator projects directly, then selects one
provider with `--provider` for each invocation.

The tool owns source generation, the native CMake build, cache validation, dependency-closure
staging, and third-party notices. Provider `.Windows` projects invoke it before compilation. The
PowerShell files that remain in `Build/` are independent package, boundary, and CI verification
gates; they are not alternative generation implementations.

```powershell
dotnet run --project src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/TedToolkit.CppBindings.Windows.Generation.Tool.csproj -c Release -- --provider cgal --repository-root . --output-root output/providers/cgal --vcpkg-root $env:VCPKG_ROOT --configuration Release
```
