```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3
  Measured : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                       | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------- |---------:|---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| Kernel32LoadResolveFree      | 249.7 μs | 28.29 μs | 18.71 μs | 251.1 μs | 279.3 μs |  1.00 |    0.10 |     312 B |        1.00 |
| NativeLibraryLoadResolveFree | 235.5 μs |  5.13 μs |  3.05 μs | 236.1 μs | 239.0 μs |  0.95 |    0.07 |     312 B |        1.00 |
