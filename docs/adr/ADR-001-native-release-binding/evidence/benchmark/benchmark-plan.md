# Benchmark Plan: Native Release Binding

## Decision question

Which Windows native binding mechanism should generated owners use to invoke an exported C ABI
`Release` function while supporting both `netstandard2.0` consumers and modern .NET?

An extension requested before ADR approval adds a second question: should generated bindings store
resolved exports in a static indexed function table, following HexaGen's design, instead of storing
the release pointer in each owner or using one static typed field per export?

## Candidates

1. `NativeLibrary.Load` / `NativeLibrary.GetExport` plus an unmanaged `cdecl` function pointer.
2. `LoadLibraryW` / `GetProcAddress` / `FreeLibrary` imported from `kernel32`, with the export
   used either as an unmanaged `cdecl` function pointer or converted once to a cached managed
   delegate.
3. A static `DllImport` entry point. The representative callback case also measures a cached
   managed delegate whose body calls that entry point.

The loader and invocation questions are measured separately. Static `DllImport` has no public
equivalent of a repeatable explicit load-and-resolve operation, so it is included in steady-state
invocation and lifecycle benchmarks, not the loader benchmark.

## Compatibility matrix

| Candidate | .NET Framework 4.8 (`netstandard2.0` consumer) | .NET 8+ |
|---|---:|---:|
| `NativeLibrary` + function pointer | No | Yes |
| `kernel32` loader + function pointer | Yes, Windows only | Yes, Windows only |
| `kernel32` loader + cached delegate | Yes, Windows only | Yes, Windows only |
| Static `DllImport` | Yes | Yes |

## Workloads

| Benchmark | What it isolates | Frequency in production |
|---|---|---|
| `InvocationBenchmarks` | One call to the same native `cdecl` export | Every release |
| `LifecycleBenchmarks` | Native allocation followed by release | Every owner lifecycle |
| `LoaderBenchmarks` | Module load, symbol lookup, and module release | Initialization / binding |

The native fixture is deliberately small and exports identical C ABI functions for every candidate.
Its lifecycle workload allocates and deletes a small C++ object. It is a faithful interop-path
surrogate, not a benchmark of OCCT's own destructor cost.

## Runtime coverage

- .NET Framework 4.8.1 using target framework `net48`.
- .NET 8, .NET 9, and .NET 10 using `net8.0`, `net9.0`, and `net10.0`.
- Windows x64, Release build, the same native DLL, and the same machine for all runs.

`netstandard2.0` is a compile-time API specification rather than an executable runtime. The `net48`
run supplies the relevant legacy runtime evidence because .NET Framework 4.7.2 and later can consume
`netstandard2.0` libraries.

## Metrics and decision rules

- Record mean, median, standard deviation, ratio to direct `DllImport`, and managed allocations.
- Treat runtime/API compatibility as a hard constraint.
- Use the representative lifecycle benchmark as the primary performance metric.
- A compatible candidate is acceptable when its lifecycle mean is within 5% of the modern
  `NativeLibrary` function-pointer path and it adds no managed allocation per operation.
- Invocation-only and loader measurements are diagnostic. They explain a lifecycle result but do
  not override compatibility or lifecycle behavior by themselves.
- If noise or confidence intervals make the 5% comparison inconclusive, rerun before deciding.

### Static function-table extension

The dispatch comparison measures:

1. An unmanaged function pointer stored in an instance field (current `Handle<T>` baseline).
2. A static typed unmanaged function-pointer field.
3. A static managed `IntPtr[]`, using constant and runtime indices.
4. A static unmanaged pointer table, using constant and runtime indices.
5. A Hexa-style class whose indexer reads a slot from an unmanaged `void**` table.

The owner comparison measures both lifecycle time and managed bytes allocated for:

- the current per-instance release pointer;
- a process-global typed pointer;
- process-global managed and unmanaged indexed tables; and
- a module-safe owner that stores a table reference and index.

The table candidate passes the hot-path criterion when its mean is within 5% of the instance-field
function pointer and adds no per-call managed allocation. An owner-storage claim requires a measured
reduction in managed bytes per owner. Compatibility, module isolation, and safe finalizer-time
module lifetime remain hard constraints even if a globally static table is faster.

Table initialization is measured separately at 16, 256, and 1,536 slots. It is diagnostic because
production initializes once and retains the table for process lifetime.

## Execution

Build the native fixture first, set `TED_OCCT_BENCH_NATIVE` to its absolute DLL path, then run each
target framework separately:

```powershell
cmake -S docs/adr/ADR-001-native-release-binding/benchmark/native -B docs/adr/ADR-001-native-release-binding/benchmark/native/build -A x64
cmake --build docs/adr/ADR-001-native-release-binding/benchmark/native/build --config Release
$env:TED_OCCT_BENCH_NATIVE = (Resolve-Path docs/adr/ADR-001-native-release-binding/benchmark/native/build/Release/ted_occt_release_benchmark.dll)
dotnet run --project docs/adr/ADR-001-native-release-binding/benchmark/TedToolkit.Occt.NativeRelease.Benchmarks.csproj -c Release -f net8.0 -- --filter '*'
```

Repeat the last command for `net9.0`, `net10.0`, and `net48`. BenchmarkDotNet artifacts are copied
to `evidence/benchmark/results/<tfm>` after each run so subsequent runs cannot overwrite them.
