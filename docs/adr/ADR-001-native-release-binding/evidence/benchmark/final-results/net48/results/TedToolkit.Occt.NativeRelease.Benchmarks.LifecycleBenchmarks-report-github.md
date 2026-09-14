```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256
  Measured : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                           | Mean     | Error     | StdDev   | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |---------:|----------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                    | 56.33 ns | 11.514 ns | 7.616 ns | 56.12 ns | 67.06 ns |  1.02 |    0.19 |         - |          NA |
| CachedDelegateToPInvoke          | 51.88 ns |  9.492 ns | 6.279 ns | 50.43 ns | 61.42 ns |  0.94 |    0.16 |         - |          NA |
| Kernel32ResolvedDelegates        | 71.64 ns |  8.013 ns | 4.768 ns | 72.57 ns | 78.06 ns |  1.29 |    0.19 |         - |          NA |
| Kernel32ResolvedFunctionPointers | 45.37 ns |  9.520 ns | 6.297 ns | 44.93 ns | 54.01 ns |  0.82 |    0.15 |         - |          NA |
