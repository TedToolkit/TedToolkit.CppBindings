```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3
  Measured : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean      | Error     | StdDev    | Median    | P95       | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |----------:|----------:|----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceFieldOwner            |  96.86 ns | 17.068 ns | 11.290 ns |  94.56 ns | 113.82 ns |  1.01 |    0.16 | 0.0025 |      32 B |        1.00 |
| StaticTypedFieldOwner         |  96.69 ns | 14.546 ns |  9.621 ns |  95.95 ns | 112.35 ns |  1.01 |    0.15 | 0.0019 |      24 B |        0.75 |
| StaticManagedTableOwner       |  90.66 ns |  8.645 ns |  5.144 ns |  91.41 ns |  97.39 ns |  0.95 |    0.12 | 0.0019 |      24 B |        0.75 |
| StaticUnmanagedTableOwner     |  91.32 ns |  9.844 ns |  6.511 ns |  89.14 ns | 101.61 ns |  0.95 |    0.12 | 0.0019 |      24 B |        0.75 |
| HexaStyleStaticTableOwner     | 103.18 ns | 19.532 ns | 12.919 ns | 102.43 ns | 120.79 ns |  1.08 |    0.17 | 0.0019 |      24 B |        0.75 |
| ModuleSafeTableReferenceOwner | 113.01 ns | 20.925 ns | 13.840 ns | 113.90 ns | 131.40 ns |  1.18 |    0.19 | 0.0031 |      40 B |        1.25 |
