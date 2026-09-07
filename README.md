# TedToolkit.CppBindings

TedToolkit.CppBindings is a .NET platform for generating C++ bindings. This repository provides
the Open CASCADE Technology (OCCT) provider: it reads OCCT headers installed by vcpkg, builds a
normalized semantic model, and emits a matched C++ ABI boundary and C# API.

> [!WARNING]
> The project is under active development. The complete Windows binding package can be built and
> verified locally, but it is not yet published to a remote package feed. The only currently proved
> ready-to-use artifact targets `win-x64`, OCCT 8.0.1, and `net8.0`.

## What this repository provides

- A provider-neutral generation pipeline, runtime contracts, and consumer analyzers.
- An OCCT generator that discovers transitive declaration dependencies from selected public headers.
- Exact-layout managed representations paired with compiler-matched native storage and invocation.
- Explicit projections for OCCT value types, inheritance, intrusive handles, RAII ownership,
  reference returns, and native exceptions.
- A ready-to-use Windows package that generates every representable declaration supported by the
  delivered OCCT DLLs.

## Start here

| Goal | Documentation |
| --- | --- |
| Understand the generated OCCT API and supported package | [OCCT Windows bindings](src/providers/occt/TedToolkit.CppBindings.Occt.Windows/README.md) |
| Generate OCCT bindings | [OCCT Generator](src/providers/occt/TedToolkit.CppBindings.Occt.Generator/README.md) |
| Use provider-neutral generation stages | [Generator](src/shared/TedToolkit.CppBindings.Generator/README.md) |
| Understand ownership and generated-code contracts | [Runtime](src/shared/TedToolkit.CppBindings.Runtime/README.md) |
| Understand OCCT intrusive ownership and error projection | [OCCT Runtime](src/providers/occt/TedToolkit.CppBindings.Occt.Runtime/README.md) |
| Configure consumer diagnostics | [Analyzers](src/tools/TedToolkit.CppBindings.Analyzers/README.md) |
| Review the system design | [Generated binding architecture](docs/architecture/generated-binding-system.md) |
| Run the generation benchmark workflow | [Benchmarks](benchmarks/README.md) |

## Architecture at a glance

The generation path is model-first. Parsing and normalization finish before either emitter runs;
the C# and C++ outputs therefore describe one semantic model and one exact-match artifact set.

```text
OCCT headers + generation options
                 |
                 v
       ClangSharp / libclang
                 |
                 v
      normalized semantic model
          /               \
         v                 v
generated C++ boundary   generated C# bindings
          \               /
           v             v
       matched artifact set
                 |
                 v
    optional native build and package
```

The native boundary exposes a deterministic function table generated from the same completed model.
Managed bindings load the package-owned module, resolve the table bootstrap, and call exact typed
slots. See the [architecture record](docs/architecture/generated-binding-system.md) for the full
layout, dispatch, lifetime, and packaging contracts.

## Semantic guarantees

- Generated bindings preserve C++ reference returns: `const T&` becomes `ref readonly T`, and
  `T&` becomes `ref T`. The original owner's lifetime and invalidation rules still apply.
- `Owned<T>` stores a non-transient RAII object in address-stable managed storage. Disposal invokes
  its C++ destructor; the garbage collector reclaims the backing memory.
- `Handle<T>` owns and releases one OCCT intrusive reference. Lowercase `handle<T>` is a
  pointer-sized, non-owning view used for fields, parameters, and borrowed references.
- C++ inheritance is projected through C# interfaces, while instance behavior and native lifetime
  remain in extension methods and separate owner types.
- Supported C++ exceptions are captured at the native boundary and projected as .NET exceptions.

These guarantees are governed by the [repository principles](docs/principles/README.md), not by
convenience policy in generated bindings.

## Supported profile

| Concern | Current support |
| --- | --- |
| Ready-to-use package and assembly | `TedToolkit.CppBindings.Occt.Windows` |
| Managed API namespace | `TedToolkit.CppBindings.Occt` |
| Runtime identifier | `win-x64` |
| OCCT version | 8.0.1 |
| Binding target framework | `net8.0` |
| Generator and development host | .NET 10 |
| Native toolchain | Visual C++, C++17, CMake 3.28 or later |

The repository does not currently contain a `vcpkg.json` manifest. Generation therefore uses the
OCCT installation under `VCPKG_ROOT`, and a new platform, architecture, compiler ABI, or header
scope requires fresh compiler and native-behavior proof.

## Build and generate locally

### Prerequisites

- .NET 10 SDK
- PowerShell 7
- CMake 3.28 or later
- Visual Studio with MSVC, CMake tools, and LLVM (`clang-cl`) components
- vcpkg with OCCT 8.0.1 installed for `x64-windows`
- `VCPKG_ROOT` set to the vcpkg installation directory

Confirm the OCCT installation:

```powershell
& "$env:VCPKG_ROOT\vcpkg.exe" list opencascade
```

Restore and build the solution:

```powershell
dotnet restore TedToolkit.CppBindings.slnx
dotnet build TedToolkit.CppBindings.slnx -c Release --no-restore
```

Run the development host to generate the public OCCT surface under `output/generated`:

```powershell
dotnet run --project tests/TedToolkit.CppBindings.Occt.Console/TedToolkit.CppBindings.Occt.Console.csproj -c Release
```

The generated tree contains managed sources under `output/generated/csharp` and native sources,
build files, and build outputs under `output/generated/cpp` and related generated directories.
Declarations that cannot be instantiated or are absent from the delivered OCCT DLLs are excluded
only after compiler or linker proof.

Use a short Windows checkout path. Deep worktrees combined with generated template names can exceed
MSVC object-path limits.

## Projects

| Project | Responsibility |
| --- | --- |
| `TedToolkit.CppBindings.Runtime` | Provider-neutral ownership and generated-code contracts |
| `TedToolkit.CppBindings.Generator` | Provider-neutral generation orchestration and source publication |
| `TedToolkit.CppBindings.Analyzers` | Consumer diagnostics for generated-only and borrowed-reference contracts |
| `TedToolkit.CppBindings.Occt.Runtime` | OCCT intrusive ownership and native error projection |
| `TedToolkit.CppBindings.Occt.Generator` | OCCT discovery, classification, finite provider policy, build metadata, and pipeline integration |
| `TedToolkit.CppBindings.Occt.SourceGenerators` | Build-time generation of the selectable OCCT header inventory |
| `TedToolkit.CppBindings.Occt.Windows` | Ready-to-use Windows binding artifact for the proved profile |
| `TedToolkit.CppBindings.Occt.Console` | Development host for generating the public OCCT surface |
| `Build` | Repository build, test, native fixture, and integration gates |

## Test and verification

Tests use TUnit with Microsoft Testing Platform. After building, run the managed suites through
their executable entry points:

```powershell
dotnet run --project tests/TedToolkit.CppBindings.Runtime.Tests/TedToolkit.CppBindings.Runtime.Tests.csproj -c Release --no-build -- --report-trx
dotnet run --project tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
```

Run the complete repository pipeline, including native handle fixtures and managed integration:

```powershell
dotnet run --project Build/Build.csproj
```

Focused build-gate checks are also available:

```powershell
pwsh -NoProfile -File Build/VerifyGenerationCache.ps1
dotnet build Build/Build.csproj -c Release
pwsh -NoProfile -File Build/VerifyManagedTestGate.ps1
```

The real OCCT boundary test requires a usable `VCPKG_ROOT`; only that environment-dependent test is
reported as skipped when the required installation is unavailable.

## Further documentation

- [C++ bindings platform architecture](docs/architecture/cpp-bindings-platform.md)
- [Runtime and analyzer boundary](docs/architecture/runtime-analyzer-boundary.md)
- [Architecture decisions](docs/adr)
- [Repository design principles](docs/principles/README.md)
- [Binding-generation performance evidence](docs/performance/binding-generation.md)

## License

This project is licensed under LGPL-3.0. See [COPYING](COPYING) and
[COPYING.LESSER](COPYING.LESSER). OCCT and other dependencies retain their respective licenses.
