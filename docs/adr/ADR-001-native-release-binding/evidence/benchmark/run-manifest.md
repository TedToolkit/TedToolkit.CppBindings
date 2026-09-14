# Benchmark Run Manifest

## Source

- Repository commit at start of final evidence capture:
  `d2d3c03081e684709b867f586d0a419335e025d5`
- BenchmarkDotNet: 0.15.8
- Native fixture SHA-256:
  `606DC2BFC6387AF88806AABC932E57D108A009FE27E734A51F55335B2D8632C8`
- Date: 2026-08-26

The worktree contained unrelated user changes. This experiment added only the ADR-local solution,
projects, evidence, and generated benchmark reports.

## Machine

- OS: Windows 11 10.0.26200.9168, x64
- CPU: 12th Gen Intel Core i7-12700H, 14 physical / 20 logical cores
- .NET SDK: 10.0.400
- Runtimes: .NET 8.0.30, .NET 9.0.19, .NET 10.0.11
- .NET Framework: 4.8.1 (`4.8.09221`, release key `533509`)
- Native compiler: clang-cl 22.1.3, x86_64-pc-windows-msvc
- Benchmark GC: concurrent workstation
- Benchmark job: three warmups and ten measured iterations unless stated otherwise

BenchmarkDotNet selected the Windows high-performance power plan during each run. Thermal/power
drift was still visible in some complete runs and is called out in the report.

## Native build

The installed Visual Studio instance did not include the default MSVC C++ toolset integration, so
the native fixture was compiled directly with the installed clang-cl:

```powershell
New-Item -ItemType Directory -Force -Path docs/adr/ADR-001-native-release-binding/benchmark/native/direct-build
& (Get-Command clang-cl).Source /nologo /LD /O2 /EHsc docs/adr/ADR-001-native-release-binding/benchmark/native/release_benchmark.cpp /Fe:docs/adr/ADR-001-native-release-binding/benchmark/native/direct-build/ted_occt_release_benchmark.dll
```

`CMakeLists.txt` is retained for environments with an installed CMake C++ toolset.

## Managed build and compatibility proof

```powershell
dotnet build docs/adr/ADR-001-native-release-binding/benchmark/TedToolkit.Occt.NativeRelease.Benchmarks.csproj -c Release
dotnet build docs/adr/ADR-001-native-release-binding/compatibility/TedToolkit.Occt.NativeRelease.NetStandard20Probe.csproj -c Release
```

Both commands completed with zero warnings and zero errors.

## Benchmark execution

For each TFM, `TED_OCCT_BENCH_NATIVE` was set to the absolute native DLL path and the following form
was run outside the network-restricted sandbox because BenchmarkDotNet builds a generated project:

```powershell
$env:TED_OCCT_BENCH_NATIVE = (Resolve-Path docs/adr/ADR-001-native-release-binding/benchmark/native/direct-build/ted_occt_release_benchmark.dll).Path
dotnet run --project docs/adr/ADR-001-native-release-binding/benchmark/TedToolkit.Occt.NativeRelease.Benchmarks.csproj -c Release -f <TFM> --no-build -- --filter '*InvocationBenchmarks*' '*LifecycleBenchmarks*' --artifacts <TFM-RESULT-DIRECTORY>
```

`<TFM>` was `net48`, `net8.0`, `net9.0`, and `net10.0`. Loader results came from the corresponding
complete suite runs.

The .NET 10 confirmation used:

```powershell
dotnet run --project docs/adr/ADR-001-native-release-binding/benchmark/TedToolkit.Occt.NativeRelease.Benchmarks.csproj -c Release -f net10.0 --no-build -- --filter '*LifecycleBenchmarks.Kernel32ResolvedFunctionPointers*' '*LifecycleBenchmarks.NativeLibraryFunctionPointers*' --launchCount 3 --warmupCount 5 --iterationCount 15
```

The static function-table extension used the same environment and native fixture on every TFM:

```powershell
dotnet run --project docs/adr/ADR-001-native-release-binding/benchmark/TedToolkit.Occt.NativeRelease.Benchmarks.csproj -c Release -f <TFM> --no-build -- --filter '*FunctionTable*' --artifacts docs/adr/ADR-001-native-release-binding/evidence/benchmark/function-table-results/<TFM>
```

The four executions completed 19 benchmarks each: seven dispatch shapes, six owner shapes, and
managed/unmanaged initialization at three table sizes.
