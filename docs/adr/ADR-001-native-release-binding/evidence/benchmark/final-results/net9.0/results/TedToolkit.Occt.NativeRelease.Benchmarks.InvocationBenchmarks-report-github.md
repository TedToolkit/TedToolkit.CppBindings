```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3
  Measured : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                          | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------- |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                   | 3.085 ns | 0.2165 ns | 0.1432 ns | 3.087 ns | 3.273 ns |  1.00 |    0.06 |         - |          NA |
| CachedDelegateToPInvoke         | 4.936 ns | 0.3581 ns | 0.2369 ns | 4.959 ns | 5.278 ns |  1.60 |    0.10 |         - |          NA |
| Kernel32ResolvedDelegate        | 9.255 ns | 0.7113 ns | 0.3720 ns | 9.180 ns | 9.777 ns |  3.01 |    0.18 |         - |          NA |
| Kernel32ResolvedFunctionPointer | 2.538 ns | 0.2236 ns | 0.1331 ns | 2.529 ns | 2.748 ns |  0.82 |    0.05 |         - |          NA |
| NativeLibraryFunctionPointer    | 2.699 ns | 0.2984 ns | 0.1776 ns | 2.750 ns | 2.910 ns |  0.88 |    0.07 |         - |          NA |
