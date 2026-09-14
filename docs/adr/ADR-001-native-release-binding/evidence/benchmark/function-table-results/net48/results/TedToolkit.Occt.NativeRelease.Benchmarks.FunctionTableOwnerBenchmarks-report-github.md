```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256
  Measured : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean      | Error     | StdDev    | Median    | P95       | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |----------:|----------:|----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceFieldOwner            | 104.60 ns | 22.464 ns | 14.858 ns | 102.79 ns | 126.44 ns |  1.02 |    0.19 | 0.0050 |      32 B |        1.00 |
| StaticTypedFieldOwner         |  89.70 ns |  5.390 ns |  3.565 ns |  88.56 ns |  95.00 ns |  0.87 |    0.12 | 0.0038 |      24 B |        0.75 |
| StaticManagedTableOwner       |  90.20 ns |  5.054 ns |  3.343 ns |  90.80 ns |  94.61 ns |  0.88 |    0.12 | 0.0038 |      24 B |        0.75 |
| StaticUnmanagedTableOwner     |  88.07 ns |  3.649 ns |  2.172 ns |  87.30 ns |  91.28 ns |  0.86 |    0.11 | 0.0038 |      24 B |        0.75 |
| HexaStyleStaticTableOwner     |  90.50 ns |  7.018 ns |  4.642 ns |  88.33 ns |  97.98 ns |  0.88 |    0.12 | 0.0038 |      24 B |        0.75 |
| ModuleSafeTableReferenceOwner |  87.73 ns |  4.534 ns |  2.999 ns |  87.34 ns |  92.19 ns |  0.85 |    0.12 | 0.0063 |      40 B |        1.25 |
