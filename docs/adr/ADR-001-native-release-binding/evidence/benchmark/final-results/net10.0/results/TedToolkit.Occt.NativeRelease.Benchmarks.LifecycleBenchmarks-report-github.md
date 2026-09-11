```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                    | 35.07 ns | 3.863 ns | 2.555 ns | 33.90 ns | 39.07 ns |  1.00 |    0.10 |         - |          NA |
| CachedDelegateToPInvoke          | 37.33 ns | 3.400 ns | 2.023 ns | 37.41 ns | 40.14 ns |  1.07 |    0.09 |         - |          NA |
| Kernel32ResolvedDelegates        | 44.70 ns | 3.725 ns | 2.464 ns | 44.20 ns | 47.93 ns |  1.28 |    0.11 |         - |          NA |
| Kernel32ResolvedFunctionPointers | 32.98 ns | 1.015 ns | 0.671 ns | 32.84 ns | 33.91 ns |  0.94 |    0.06 |         - |          NA |
| NativeLibraryFunctionPointers    | 43.81 ns | 7.240 ns | 4.789 ns | 42.90 ns | 51.08 ns |  1.25 |    0.15 |         - |          NA |
