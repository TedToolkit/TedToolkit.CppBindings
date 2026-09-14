```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3
  Measured : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                       | Mean     | Error   | StdDev  | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|---------:|---------:|------:|--------:|----------:|------------:|
| Kernel32LoadResolveFree      | 248.2 μs | 9.92 μs | 6.56 μs | 249.0 μs | 256.4 μs |  1.00 |    0.04 |     312 B |        1.00 |
| NativeLibraryLoadResolveFree | 244.6 μs | 5.66 μs | 3.74 μs | 243.8 μs | 250.2 μs |  0.99 |    0.03 |     312 B |        1.00 |
