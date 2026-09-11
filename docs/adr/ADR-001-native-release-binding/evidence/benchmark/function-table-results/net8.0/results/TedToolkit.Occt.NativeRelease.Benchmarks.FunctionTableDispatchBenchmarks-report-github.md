```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3
  Measured : .NET 8.0.30 (8.0.30, 8.0.3026.36720), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| InstanceFieldFunctionPointer  | 3.558 ns | 0.3358 ns | 0.2221 ns | 3.557 ns | 3.896 ns |  1.00 |    0.08 |         - |          NA |
| StaticTypedFunctionPointer    | 4.362 ns | 1.3300 ns | 0.8797 ns | 4.164 ns | 5.710 ns |  1.23 |    0.25 |         - |          NA |
| ManagedArrayConstantIndex     | 4.426 ns | 1.2631 ns | 0.8355 ns | 4.521 ns | 5.402 ns |  1.25 |    0.24 |         - |          NA |
| ManagedArrayRuntimeIndex      | 3.695 ns | 0.3514 ns | 0.2324 ns | 3.774 ns | 3.932 ns |  1.04 |    0.09 |         - |          NA |
| UnmanagedTableConstantIndex   | 4.691 ns | 2.0953 ns | 1.3859 ns | 4.339 ns | 6.468 ns |  1.32 |    0.38 |         - |          NA |
| UnmanagedTableRuntimeIndex    | 3.314 ns | 0.2536 ns | 0.1678 ns | 3.224 ns | 3.581 ns |  0.93 |    0.07 |         - |          NA |
| HexaStyleIndexerConstantIndex | 4.262 ns | 1.3064 ns | 0.8641 ns | 4.355 ns | 5.426 ns |  1.20 |    0.24 |         - |          NA |
