```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3
  Measured : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                          | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------- |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| DirectPInvoke                   | 5.528 ns | 2.7793 ns | 1.8383 ns | 6.438 ns | 7.302 ns |  1.13 |    0.56 |         - |          NA |
| CachedDelegateToPInvoke         | 4.837 ns | 0.8177 ns | 0.5409 ns | 4.859 ns | 5.557 ns |  0.99 |    0.37 |         - |          NA |
| Kernel32ResolvedDelegate        | 9.156 ns | 0.3631 ns | 0.2161 ns | 9.172 ns | 9.447 ns |  1.87 |    0.68 |         - |          NA |
| Kernel32ResolvedFunctionPointer | 3.170 ns | 0.6513 ns | 0.3876 ns | 2.989 ns | 3.836 ns |  0.65 |    0.25 |         - |          NA |
| NativeLibraryFunctionPointer    | 3.131 ns | 0.2533 ns | 0.1325 ns | 3.175 ns | 3.284 ns |  0.64 |    0.23 |         - |          NA |
