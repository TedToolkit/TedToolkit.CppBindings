# API and Compatibility Analysis

## Required surface

The generated Windows binding needs to:

1. Load a DLL by an absolute or generated package path.
2. Resolve a named C ABI export.
3. Pass the address to `Handle<T>` as `delegate* unmanaged[Cdecl]<T*, void>`.
4. Keep the DLL loaded through every possible disposal or finalizer call.

## Candidate availability

| API | `netstandard2.0` compile | .NET Framework 4.8.1 run | .NET 8+ run |
|---|---:|---:|---:|
| Static `DllImport` | Yes | Yes | Yes |
| `kernel32` loader through `DllImport` | Yes | Yes | Yes on Windows |
| Unmanaged Cdecl function-pointer cast/call | Yes | Yes | Yes |
| `Marshal.GetDelegateForFunctionPointer<T>` | Yes | Yes | Yes |
| `NativeLibrary.Load/GetExport/Free` | No | No | Yes |

The `netstandard2.0` compile result is proven by
`compatibility/TedToolkit.Occt.NativeRelease.NetStandard20Probe.csproj`. The benchmark's `net48`
execution proves the legacy JIT can execute the same unmanaged function-pointer call.

`netstandard2.0` is an API specification, not a runtime, so it cannot be benchmarked directly.
.NET Framework 4.8.1 is the legacy runtime used for execution evidence. Microsoft recommends
.NET Framework 4.7.2 or later when consuming `netstandard2.0` libraries.

## Signature requirements

- Native exports use `extern "C"` to avoid C++ name mangling.
- The benchmark and selected binding use `cdecl` on both sides.
- The release export is non-throwing across the unmanaged boundary.
- `GetProcAddress` is resolved once; repeated symbol lookup is not part of cleanup.

## Module lifetime

An export address is valid only while its DLL remains loaded. Because `Handle<T>` is finalizable,
ordinary lexical scope is insufficient: a finalizer may call the address after the generated factory
returns. The safe initial policy is a process-lifetime cached module. Calling `FreeLibrary` earlier
requires an explicit lease held by every owner and cannot be inferred from managed wrapper scope.

## Sources

- Microsoft native interop best practices:
  <https://learn.microsoft.com/dotnet/standard/native-interop/best-practices>
- Microsoft native library loading documentation:
  <https://learn.microsoft.com/dotnet/standard/native-interop/native-library-loading>
- Microsoft .NET Standard overview:
  <https://learn.microsoft.com/dotnet/standard/net-standard>
- BenchmarkDotNet 0.15.8 package metadata:
  <https://www.nuget.org/packages/BenchmarkDotNet/0.15.8>
