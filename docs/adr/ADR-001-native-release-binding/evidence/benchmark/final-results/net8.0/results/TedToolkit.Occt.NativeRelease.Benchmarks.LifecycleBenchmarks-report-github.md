```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3
  Measured : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                    | 35.21 ns | 2.935 ns | 1.942 ns | 35.15 ns | 38.15 ns |  1.00 |    0.07 |         - |          NA |
| CachedDelegateToPInvoke          | 36.85 ns | 2.533 ns | 1.675 ns | 37.26 ns | 38.98 ns |  1.05 |    0.07 |         - |          NA |
| Kernel32ResolvedDelegates        | 48.27 ns | 7.953 ns | 5.260 ns | 46.20 ns | 56.47 ns |  1.37 |    0.16 |         - |          NA |
| Kernel32ResolvedFunctionPointers | 33.13 ns | 1.410 ns | 0.933 ns | 32.86 ns | 34.65 ns |  0.94 |    0.05 |         - |          NA |
| NativeLibraryFunctionPointers    | 31.74 ns | 0.948 ns | 0.627 ns | 31.65 ns | 32.70 ns |  0.90 |    0.05 |         - |          NA |
