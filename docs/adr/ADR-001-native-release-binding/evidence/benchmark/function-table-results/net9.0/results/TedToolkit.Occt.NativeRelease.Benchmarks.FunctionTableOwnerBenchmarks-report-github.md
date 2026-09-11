```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3
  Measured : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean      | Error     | StdDev    | Median   | P95       | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |----------:|----------:|----------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceFieldOwner            |  84.96 ns |  7.069 ns |  4.676 ns | 84.42 ns |  91.75 ns |  1.00 |    0.07 | 0.0025 |      32 B |        1.00 |
| StaticTypedFieldOwner         |  87.07 ns |  8.672 ns |  5.736 ns | 86.04 ns |  95.85 ns |  1.03 |    0.08 | 0.0019 |      24 B |        0.75 |
| StaticManagedTableOwner       | 104.21 ns | 34.727 ns | 22.970 ns | 98.15 ns | 140.69 ns |  1.23 |    0.27 | 0.0019 |      24 B |        0.75 |
| StaticUnmanagedTableOwner     |  82.63 ns |  4.400 ns |  2.301 ns | 83.68 ns |  84.82 ns |  0.98 |    0.06 | 0.0019 |      24 B |        0.75 |
| HexaStyleStaticTableOwner     |  98.30 ns | 21.691 ns | 14.347 ns | 96.82 ns | 119.11 ns |  1.16 |    0.17 | 0.0019 |      24 B |        0.75 |
| ModuleSafeTableReferenceOwner |  96.73 ns | 11.865 ns |  7.848 ns | 96.04 ns | 107.88 ns |  1.14 |    0.11 | 0.0031 |      40 B |        1.25 |
