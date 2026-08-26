```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3
  Measured : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                    | 34.04 ns | 1.398 ns | 0.731 ns | 34.28 ns | 34.72 ns |  1.00 |    0.03 |         - |          NA |
| CachedDelegateToPInvoke          | 34.81 ns | 1.353 ns | 0.708 ns | 34.83 ns | 35.68 ns |  1.02 |    0.03 |         - |          NA |
| Kernel32ResolvedDelegates        | 43.33 ns | 3.463 ns | 2.291 ns | 43.12 ns | 46.75 ns |  1.27 |    0.07 |         - |          NA |
| Kernel32ResolvedFunctionPointers | 33.24 ns | 4.674 ns | 2.782 ns | 33.38 ns | 37.43 ns |  0.98 |    0.08 |         - |          NA |
| NativeLibraryFunctionPointers    | 33.00 ns | 1.456 ns | 0.762 ns | 33.15 ns | 33.71 ns |  0.97 |    0.03 |         - |          NA |
