```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256
  Measured : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                          | Mean      | Error     | StdDev    | Median    | P95       | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------- |----------:|----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DirectPInvoke                   |  7.852 ns | 0.5779 ns | 0.3439 ns |  7.761 ns |  8.345 ns |  1.00 |    0.06 |         - |          NA |
| CachedDelegateToPInvoke         |  8.302 ns | 0.6807 ns | 0.4051 ns |  8.316 ns |  8.850 ns |  1.06 |    0.07 |         - |          NA |
| Kernel32ResolvedDelegate        | 14.510 ns | 1.4411 ns | 0.7537 ns | 14.464 ns | 15.564 ns |  1.85 |    0.12 |         - |          NA |
| Kernel32ResolvedFunctionPointer |  4.077 ns | 0.9725 ns | 0.6433 ns |  3.750 ns |  5.027 ns |  0.52 |    0.08 |         - |          NA |
