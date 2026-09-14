# ADR-001 Evidence Index

| Evidence | Purpose |
|---|---|
| [API analysis](api-analysis.md) | Compatibility and lifecycle constraints |
| [Benchmark plan](benchmark/benchmark-plan.md) | Predeclared workloads and decision rules |
| [Benchmark report](benchmark/report.md) | Consolidated measurements and interpretation |
| [Function-table report](benchmark/function-table-report.md) | Cross-runtime dispatch, initialization, and owner-storage results |
| [Hexa source analysis](hexa-function-table-analysis.md) | What Hexa actually generates and which constraints transfer |
| [Run manifest](benchmark/run-manifest.md) | Reproduction environment and commands |
| [Evaluation matrix](evaluation-matrix.md) | Candidate comparison and recommendation |
| `benchmark/final-results/` | BenchmarkDotNet Markdown and full JSON reports |
| `benchmark/confirmation/net10.0/` | Longer .NET 10 function-pointer confirmation |
| `benchmark/function-table-results/` | Function-table Markdown and full JSON reports for four runtimes |

The benchmark source and native fixture live in the sibling `benchmark/` directory. The
`compatibility/` project is a compile-only `netstandard2.0` API probe.
