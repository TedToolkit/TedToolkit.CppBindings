```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256
  Measured : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                  | Mean     | Error    | StdDev   | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |---------:|---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| Kernel32LoadResolveFree | 364.1 μs | 54.07 μs | 35.76 μs | 368.9 μs | 399.6 μs |  1.01 |    0.15 |     968 B |        1.00 |
