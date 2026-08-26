```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                       | Mean     | Error    | StdDev  | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------- |---------:|---------:|--------:|---------:|---------:|------:|--------:|----------:|------------:|
| Kernel32LoadResolveFree      | 224.8 μs | 11.31 μs | 7.48 μs | 225.2 μs | 235.0 μs |  1.00 |    0.05 |     312 B |        1.00 |
| NativeLibraryLoadResolveFree | 230.1 μs | 12.35 μs | 7.35 μs | 227.8 μs | 243.0 μs |  1.02 |    0.05 |     312 B |        1.00 |
