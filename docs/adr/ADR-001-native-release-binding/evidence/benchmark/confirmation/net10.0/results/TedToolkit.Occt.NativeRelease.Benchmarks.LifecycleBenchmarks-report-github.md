```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700H 2.30GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Measured : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Measured  IterationCount=15  LaunchCount=3  
WarmupCount=5  

```
| Method                           | Mean     | Error    | StdDev   | Median   | P95      | Allocated |
|--------------------------------- |---------:|---------:|---------:|---------:|---------:|----------:|
| Kernel32ResolvedFunctionPointers | 36.44 ns | 1.902 ns | 3.478 ns | 36.01 ns | 42.88 ns |         - |
| NativeLibraryFunctionPointers    | 37.16 ns | 1.987 ns | 3.683 ns | 36.59 ns | 43.57 ns |         - |
