```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256
  Measured : .NET Framework 4.8.1 (4.8.9337.0), X64 RyuJIT VectorSize=256

Job=Measured  IterationCount=10  WarmupCount=3  

```
| Method                | SlotCount | Mean       | Error     | StdDev    | Median     | P95        | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------- |---------- |-----------:|----------:|----------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| **ManagedArrayLoadAll**   | **16**        |   **1.855 μs** | **0.0804 μs** | **0.0421 μs** |   **1.864 μs** |   **1.905 μs** |  **1.00** |    **0.03** | **0.0229** |     **152 B** |        **1.00** |
| UnmanagedTableLoadAll | 16        |   1.977 μs | 0.1689 μs | 0.1005 μs |   1.963 μs |   2.120 μs |  1.07 |    0.06 |      - |         - |        0.00 |
|                       |           |            |           |           |            |            |       |         |        |           |             |
| **ManagedArrayLoadAll**   | **256**       |  **31.857 μs** | **4.6348 μs** | **3.0656 μs** |  **30.700 μs** |  **36.625 μs** |  **1.01** |    **0.13** | **0.3052** |    **2078 B** |        **1.00** |
| UnmanagedTableLoadAll | 256       |  33.026 μs | 5.5788 μs | 3.3198 μs |  33.080 μs |  37.869 μs |  1.04 |    0.13 |      - |         - |        0.00 |
|                       |           |            |           |           |            |            |       |         |        |           |             |
| **ManagedArrayLoadAll**   | **1536**      | **164.888 μs** | **4.0493 μs** | **2.6784 μs** | **164.352 μs** | **168.557 μs** |  **1.00** |    **0.02** | **1.9531** |   **12336 B** |        **1.00** |
| UnmanagedTableLoadAll | 1536      | 167.773 μs | 2.5825 μs | 1.5368 μs | 167.609 μs | 170.061 μs |  1.02 |    0.02 |      - |         - |        0.00 |
