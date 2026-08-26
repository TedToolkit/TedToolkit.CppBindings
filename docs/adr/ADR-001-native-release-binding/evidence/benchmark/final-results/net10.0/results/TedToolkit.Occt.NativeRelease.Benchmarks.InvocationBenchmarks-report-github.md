```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                          | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------- |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                   | 2.693 ns | 0.2173 ns | 0.1437 ns | 2.710 ns | 2.881 ns |  1.00 |    0.07 |         - |          NA |
| CachedDelegateToPInvoke         | 4.846 ns | 0.3732 ns | 0.2468 ns | 4.852 ns | 5.189 ns |  1.80 |    0.13 |         - |          NA |
| Kernel32ResolvedDelegate        | 7.111 ns | 0.6392 ns | 0.4228 ns | 7.107 ns | 7.694 ns |  2.65 |    0.20 |         - |          NA |
| Kernel32ResolvedFunctionPointer | 2.348 ns | 0.2680 ns | 0.1772 ns | 2.345 ns | 2.587 ns |  0.87 |    0.08 |         - |          NA |
| NativeLibraryFunctionPointer    | 2.728 ns | 0.1760 ns | 0.0920 ns | 2.717 ns | 2.850 ns |  1.02 |    0.06 |         - |          NA |
