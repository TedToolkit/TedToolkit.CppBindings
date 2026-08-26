```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3
  Measured : .NET 9.0.19 (9.0.19, 9.0.1926.36724), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                        | Mean     | Error     | StdDev    | Median   | P95      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |---------:|----------:|----------:|---------:|---------:|------:|--------:|----------:|------------:|
| InstanceFieldFunctionPointer  | 6.105 ns | 0.8249 ns | 0.5456 ns | 6.095 ns | 6.896 ns |  1.01 |    0.12 |         - |          NA |
| StaticTypedFunctionPointer    | 5.268 ns | 1.1814 ns | 0.7814 ns | 5.207 ns | 6.268 ns |  0.87 |    0.14 |         - |          NA |
| ManagedArrayConstantIndex     | 5.130 ns | 2.0394 ns | 1.3490 ns | 4.587 ns | 7.115 ns |  0.85 |    0.22 |         - |          NA |
| ManagedArrayRuntimeIndex      | 4.549 ns | 0.6142 ns | 0.4062 ns | 4.451 ns | 5.214 ns |  0.75 |    0.09 |         - |          NA |
| UnmanagedTableConstantIndex   | 5.582 ns | 1.6895 ns | 1.1175 ns | 5.513 ns | 6.882 ns |  0.92 |    0.19 |         - |          NA |
| UnmanagedTableRuntimeIndex    | 4.493 ns | 1.0826 ns | 0.7161 ns | 4.239 ns | 5.594 ns |  0.74 |    0.13 |         - |          NA |
| HexaStyleIndexerConstantIndex | 3.977 ns | 0.6494 ns | 0.4295 ns | 3.856 ns | 4.606 ns |  0.66 |    0.09 |         - |          NA |
