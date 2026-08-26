# Benchmark Report: Native Release Binding

## Outcome

Use `kernel32` only to load the module and resolve the export, then call the cached export as an
unmanaged Cdecl function pointer. This supports the tested `netstandard2.0` consumer runtime without
adding meaningful steady-state overhead relative to `NativeLibrary`.

Do not marshal the export to a managed delegate for `Handle<T>` cleanup. The delegate thunk is the
measurable cost, not the Windows loader.

## Pure invocation

Mean nanoseconds per call to the same native no-inline export:

| Runtime | Direct P/Invoke | Cached delegate to P/Invoke | Kernel32 delegate | Kernel32 function pointer | NativeLibrary function pointer |
|---|---:|---:|---:|---:|---:|
| .NET Framework 4.8.1 | 7.852 | 8.302 | 14.510 | 4.077 | N/A |
| .NET 8.0.30 | 5.528 | 4.837 | 9.156 | 3.170 | 3.131 |
| .NET 9.0.19 | 3.085 | 4.936 | 9.255 | 2.538 | 2.699 |
| .NET 10.0.11 | 2.693 | 4.846 | 7.111 | 2.348 | 2.728 |

The .NET 8 direct-P/Invoke result was bimodal because the machine changed power/thermal state during
that method; its median was 6.438 ns with a 1.838 ns standard deviation. It is included for
transparency but does not affect the loader-to-function-pointer comparison.

## Representative native lifecycle

Mean nanoseconds for allocating and deleting the same small native C++ owner:

| Runtime | Direct P/Invoke | Cached delegate to P/Invoke | Kernel32 delegates | Kernel32 function pointers | NativeLibrary function pointers |
|---|---:|---:|---:|---:|---:|
| .NET Framework 4.8.1 | 56.33 | 51.88 | 71.64 | 45.37 | N/A |
| .NET 8.0.30 | 35.21 | 36.85 | 48.27 | 33.13 | 31.74 |
| .NET 9.0.19 | 34.04 | 34.81 | 43.33 | 33.24 | 33.00 |
| .NET 10.0.11, full run | 35.07 | 37.33 | 44.70 | 32.98 | 43.81 |
| .NET 10.0.11, confirmation | N/A | N/A | N/A | 36.44 | 37.16 |

The full .NET 10 run was thermally unstable: an earlier full run measured the NativeLibrary
function-pointer lifecycle at 33.44 ns, while the corrected full run measured 43.81 ns. The
predeclared rule required a rerun. The confirmation used three launches, five warmups, and fifteen
measured iterations; the two paths differed by 1.9%, within the 5% threshold, with overlapping
distributions.

All invocation and lifecycle candidates reported zero managed bytes allocated per operation.

## Load, resolve, and free

Mean microseconds for a complete load-resolve-free cycle:

| Runtime | Kernel32 | NativeLibrary |
|---|---:|---:|
| .NET Framework 4.8.1 | 364.1 | N/A |
| .NET 8.0.30 | 248.2 | 244.6 |
| .NET 9.0.19 | 249.7 | 235.5 |
| .NET 10.0.11 | 224.8 | 230.1 |

Both modern paths allocated 312 managed bytes per repeated setup operation in this harness. The
.NET Framework kernel32 path allocated 968 bytes. Production resolves once and retains the module,
so these values are not per-owner costs.

## Interpretation

- `NativeLibrary` and `kernel32` return a native address. Calling either address through the same
  unmanaged function-pointer type has the same mechanism and effectively the same cost.
- `Marshal.GetDelegateForFunctionPointer` introduces a delegate invocation thunk and is consistently
  slower for the pure call.
- Static P/Invoke is fast, but the current generic `Handle<T>` needs an address, not a fixed extern
  method. A managed wrapper delegate changes both the representation and hot path.
- The tested native lifecycle is intentionally much cheaper than an OCCT destructor. Real OCCT work
  will make these few-nanosecond differences a smaller fraction of total cleanup time.

## Limitations

- One Windows x64 laptop was measured; power and thermal transitions were observable.
- The fixture proves interop mechanics, not OCCT workload distribution.
- .NET Framework 4.8.1 is the tested legacy runtime; other `netstandard2.0` implementations were not
  executed.
- Loader measurements repeatedly unload and reload a DLL, unlike the selected process-lifetime cache.

Full BenchmarkDotNet JSON and Markdown output is retained under `final-results/` and
`confirmation/net10.0/`.
