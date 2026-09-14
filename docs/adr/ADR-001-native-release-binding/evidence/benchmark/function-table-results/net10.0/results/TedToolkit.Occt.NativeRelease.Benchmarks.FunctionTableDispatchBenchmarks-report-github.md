```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| InstanceFieldFunctionPointer  | 2.971 ns | 0.3442 ns | 0.2277 ns | 2.998 ns | 3.216 ns |  1.01 |    0.10 |         - |          NA |
| StaticTypedFunctionPointer    | 2.607 ns | 0.2189 ns | 0.1303 ns | 2.593 ns | 2.811 ns |  0.88 |    0.08 |         - |          NA |
| ManagedArrayConstantIndex     | 2.397 ns | 0.2230 ns | 0.1475 ns | 2.365 ns | 2.608 ns |  0.81 |    0.08 |         - |          NA |
| ManagedArrayRuntimeIndex      | 2.775 ns | 0.5154 ns | 0.3067 ns | 2.702 ns | 3.283 ns |  0.94 |    0.12 |         - |          NA |
| UnmanagedTableConstantIndex   | 2.352 ns | 0.4591 ns | 0.3036 ns | 2.229 ns | 2.827 ns |  0.80 |    0.11 |         - |          NA |
| UnmanagedTableRuntimeIndex    | 2.518 ns | 0.1933 ns | 0.1279 ns | 2.562 ns | 2.671 ns |  0.85 |    0.08 |         - |          NA |
| HexaStyleIndexerConstantIndex | 2.793 ns | 0.2835 ns | 0.1687 ns | 2.748 ns | 3.084 ns |  0.95 |    0.09 |         - |          NA |
