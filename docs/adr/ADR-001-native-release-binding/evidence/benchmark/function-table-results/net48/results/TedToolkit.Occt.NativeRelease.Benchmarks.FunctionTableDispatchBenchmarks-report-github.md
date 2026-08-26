```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256
  Measured : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| InstanceFieldFunctionPointer  | 3.434 ns | 0.2757 ns | 0.1824 ns | 3.352 ns | 3.732 ns |  1.00 |    0.07 |         - |          NA |
| StaticTypedFunctionPointer    | 4.127 ns | 0.4334 ns | 0.2867 ns | 4.190 ns | 4.504 ns |  1.20 |    0.10 |         - |          NA |
| ManagedArrayConstantIndex     | 3.597 ns | 0.4309 ns | 0.2564 ns | 3.507 ns | 3.993 ns |  1.05 |    0.09 |         - |          NA |
| ManagedArrayRuntimeIndex      | 3.982 ns | 0.6983 ns | 0.4619 ns | 3.993 ns | 4.615 ns |  1.16 |    0.14 |         - |          NA |
| UnmanagedTableConstantIndex   | 3.713 ns | 0.5839 ns | 0.3862 ns | 3.510 ns | 4.379 ns |  1.08 |    0.12 |         - |          NA |
| UnmanagedTableRuntimeIndex    | 4.011 ns | 0.7033 ns | 0.4652 ns | 4.127 ns | 4.579 ns |  1.17 |    0.14 |         - |          NA |
| HexaStyleIndexerConstantIndex | 3.877 ns | 0.5695 ns | 0.2978 ns | 3.798 ns | 4.354 ns |  1.13 |    0.10 |         - |          NA |
