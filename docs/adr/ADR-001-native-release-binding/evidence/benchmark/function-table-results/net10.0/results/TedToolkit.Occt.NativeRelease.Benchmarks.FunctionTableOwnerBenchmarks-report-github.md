```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean      | Error     | StdDev    | Median    | P95       | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |----------:|----------:|----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| InstanceFieldOwner            |  88.37 ns | 10.483 ns |  6.934 ns |  88.72 ns |  97.68 ns |  1.01 |    0.11 | 0.0025 |      32 B |        1.00 |
| StaticTypedFieldOwner         |  83.21 ns |  6.910 ns |  4.112 ns |  81.59 ns |  88.94 ns |  0.95 |    0.08 | 0.0019 |      24 B |        0.75 |
| StaticManagedTableOwner       | 106.40 ns | 31.759 ns | 21.007 ns | 105.03 ns | 135.55 ns |  1.21 |    0.25 | 0.0019 |      24 B |        0.75 |
| StaticUnmanagedTableOwner     |  78.68 ns |  2.338 ns |  1.223 ns |  78.80 ns |  80.00 ns |  0.90 |    0.07 | 0.0019 |      24 B |        0.75 |
| HexaStyleStaticTableOwner     |  81.84 ns |  6.154 ns |  3.219 ns |  80.96 ns |  86.54 ns |  0.93 |    0.08 | 0.0019 |      24 B |        0.75 |
| ModuleSafeTableReferenceOwner |  87.49 ns |  7.100 ns |  4.225 ns |  85.91 ns |  94.29 ns |  1.00 |    0.09 | 0.0031 |      40 B |        1.25 |
