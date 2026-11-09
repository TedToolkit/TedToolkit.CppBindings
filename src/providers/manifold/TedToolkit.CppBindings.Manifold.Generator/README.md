# TedToolkit.CppBindings.Manifold.Generator

Generates finite bindings for Manifold 3.5.2 on `win-x64` and `linux-x64`. The profiles cover owned
triangle meshes, Boolean operations, translation, status, triangle count, and mesh exchange. It
does not claim coverage of the complete Manifold API.

The Generator owns only the finite Manifold profile, native algorithm bodies, dependency policy, and
header discovery. It supplies those facts through Shared's `SemanticGenerationProvider`; Shared
constructs the immutable `GenerationPlan` and emits the common managed/native transport, ownership,
bootstrap, and publication machinery through `AddCppGenerators`.

The package includes the vcpkg manifest and registry configuration needed to reproduce its native
inputs. Generation reads the complete Manifold public header inventory from the selected vcpkg
installation and verifies it against the installed package list. The locked `3.5.2` identity
represents vcpkg port revision zero, so a nonzero `Port-Version` is rejected. Repository generation
passes the vcpkg root explicitly. The parameterless provider constructor resolves `VCPKG_ROOT` and
fails before plan creation when it is not configured. Generated managed and native sources are a
matched pair and must be built together.

Set `GenerationOptions.NativeLibraryVersion = new Version(3, 5, 2)` to require that primary
Manifold version exactly; generated CMake then uses `find_package(manifold 3.5.2 EXACT ...)`. A
different version is rejected by the finite profile before publication. Leaving the option unset
retains the existing unversioned vcpkg package lookup.

Use `ManifoldGenerationProvider.FromInstallation(prefix)` to generate against a local Manifold
installation. The provider verifies `include/manifold/version.h` against the finite profile and
the resulting CMake project requires exactly that version. Pass the same prefix as
`-DCPPBINDINGS_MANIFOLD_PREFIX=<path>` when configuring the generated project.

Both entry points resolve headers through the shared `INativeSourceProvider` contract before
classifying the finite profile's header inventory.

For Linux x64, select `FromInstallation(prefix)` and set
`GenerationOptions.RuntimeIdentifier = "linux-x64"`. The generated project builds an ELF shared
library with the function-table bootstrap exposed to the managed loader. Use the same local
Manifold installation when configuring and running the bridge. To verify generation, compilation,
and native calls on Linux, run the following from the repository root with .NET 8 and 10, CMake
3.28 or later, Ninja, a C++20 compiler, binutils, and Python 3 installed.

```bash
bash Build/VerifyManifoldLinux.sh /path/to/manifold-install
```
