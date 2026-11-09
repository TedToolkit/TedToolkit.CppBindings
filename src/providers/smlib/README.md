# SMLib provider

The provider uses the official [NVIDIA USD BRep 1.0.1 source tree](https://github.com/NVIDIA-Omniverse/usd-brep/tree/v1.0.1)
at `externals/SMLib`. Its native target compiles the standalone kernel and SM_API directly.

```powershell
git submodule update --init --recursive
if (-not (Test-Path .editorconfig)) { Copy-Item externals/TedToolkit/.editorconfig .editorconfig }
dotnet pack src/providers/smlib/TedToolkit.CppBindings.Smlib.Windows -c Release
```

The Windows package build uses Visual Studio 2026 with MSVC x64 and CMake. Use
[Smlib.Windows](TedToolkit.CppBindings.Smlib.Windows/README.md) from an application or
[Smlib.Generator](TedToolkit.CppBindings.Smlib.Generator/README.md) to generate bindings yourself.

Run `pwsh -NoProfile -File Build/VerifySmlibWindowsPackage.ps1` from the repository root to
verify a fresh source build, the NuGet native dependency closure, and an isolated consumer call.
