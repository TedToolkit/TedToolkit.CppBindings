# Binding generation experiment

This directory contains isolated measurement tooling, not production optimizations. The comparative
experiment is in progress. No performance recommendation or speedup has been established.

## Resource preflight

Run from PowerShell 7.5 or later on the Windows benchmark machine, with a new report path for each attempt:

```powershell
./benchmarks/Get-BenchmarkEnvironment.ps1 -RepositoryRoot . -ReportPath ./out/benchmark/environment-01.json
./benchmarks/Verify-BenchmarkPreflight.ps1
```

The script records the Git revision, working-tree status, submodules, .NET/CMake tools, CPU, free
memory, artifact-drive space, and potentially competing builds. It fails closed when collection
fails, the destination already exists, or the resource checks fail. A rejected resource snapshot is
retained with its reasons. It does not stop other processes, alter the baseline, or run a build.
CIM access may require permission outside a restricted execution sandbox.

The initial screening gate requires 10 GiB free physical memory and 20 GiB free artifact-drive space.
These are preparation thresholds, not measured workload requirements or a peak-memory result. The
workload driver must separately declare and enforce its process-tree memory ceiling, reserve, and
12-machine-hour experiment budget. Idle persistent MSBuild and IDE service processes are excluded
from the competing-build heuristic; the snapshot cannot detect every source of CPU/I/O contention.

Before sampling, also pin MSVC, Windows SDK, the selected Ninja executable, OCCT/vcpkg versions,
input hashes, worker counts, cache state, and a complete exact source candidate. A revision alone
does not bind uncommitted edits. Full-workload sampling must not overlap migration builds.

## Process-stage measurement

`Measure-BenchmarkStage.ps1` executes one phase from a JSON specification. It records the exact
executable/argument array, specification and runner hashes, process elapsed time, exit code,
separate untruncated output/error logs, and sampled process-tree working set, CPU, and transfer
counters. Commands must stay within the approved experiment and must not contain secrets: command
arguments and child output are retained verbatim. Use a new output directory for each phase.

The specification has these required members:

| Member | Meaning |
| --- | --- |
| `Label` | Variant, workload, repetition, and phase identifier |
| `Executable` | Executable path or command resolved from PATH |
| `Arguments` | JSON string array, passed directly without a command shell |
| `WorkingDirectory` | Existing isolated working directory |
| `DeadlineUtc` | One shared UTC deadline for the complete experiment, at most 12 hours away |
| `TimeLimitSeconds` | Per-stage time ceiling, between 1 and 43,200 seconds |
| `MemoryLimitBytes` | Positive ceiling for the observed sum of process working sets |

```powershell
./benchmarks/Measure-BenchmarkStage.ps1 -SpecificationPath ./out/benchmark/phase.json -ReportDirectory ./out/benchmark/sample-01/compile
./benchmarks/Verify-BenchmarkStage.ps1
```

The driver must reuse the same experiment deadline, rather than grant each phase another 12 hours.
The stage runner terminates its own workload tree on observed memory excess, timeout, or failure;
it also cleans up recorded descendants whose process identity still matches. Disable build servers
and do not launch detached workloads. Polling cannot discover every very short-lived or orphaned
descendant; this is not an OS containment boundary or an OS-enforced memory quota.

CIM counters are sampled and introduce overhead. `ProcessElapsedSeconds` uses the process's recorded
start/exit times and excludes post-exit polling delay; `RunnerElapsedSeconds` also includes polling
and termination overhead. Working-set sums can double-count shared pages and miss transient peaks.
CPU and I/O sums omit work after the final observation or between samples. I/O counters are not
physical disk traffic. The raw report states these limitations; an uncertain memory result cannot
justify an optimization recommendation. Between-sample peaks require stronger measurement before
claiming compliance with a strict memory budget.

`Verify-BenchmarkStage.ps1` uses controlled PowerShell children to check argument boundaries,
UTF-8 output, large-log capture, nonzero exit propagation, timeout, observed memory excess,
descendant termination, and evidence preservation. Its outputs are harness verification, not OCCT
performance samples. No .NET project or production build path is modified.

## Artifact comparison

Capture sources before and after each sample, outside the timed interval and with all writers
stopped. Store manifests outside the measured roots. Keep category order identical in both runs:

```powershell
./benchmarks/Get-ArtifactManifest.ps1 -Roots @('./out/baseline/csharp', './out/baseline/cpp') -Files @('./out/baseline/unsupported-headers.txt') -ReportPath ./out/benchmark/before.json
./benchmarks/Get-ArtifactManifest.ps1 -Roots @('./out/candidate/csharp', './out/candidate/cpp') -Files @('./out/candidate/unsupported-headers.txt') -ReportPath ./out/benchmark/after.json -CompareTo ./out/benchmark/before.json
./benchmarks/Verify-ArtifactManifest.ps1
```

The manifest records sorted relative names, sizes, SHA-256 content hashes, modification times, total
bytes, and file count. Comparison separates added, removed, changed-content, and observed rewritten
files. Explicit files are ordered semantic categories after the directory roots and must be outside
those roots. An unchanged warm source set needs equal content and zero observed rewrites. Timestamp-based
rewrite detection is not a filesystem write trace; a writer that restores timestamps can hide an
identical rewrite. Byte equality supports unchanged-partition prototypes, not ABI/lifetime proof
for sharding or other repartitioning. Hashing itself warms the OS file cache: claims remain
artifact-cold, never OS-cache cold, unless a separate procedure establishes the latter.

## Paired matrix execution

`Invoke-BenchmarkMatrix.ps1` sequences one baseline/candidate pair across all five approved workloads.
It includes one warmup for each variant/workload, then at least three artifact-cold pairs and five
pairs for each other workload. Pair order alternates. Preparation and correctness verification are
outside the measured stage sum, but all phases consume the same experiment deadline and resource
budget. Warmup diagnostics are retained and excluded from statistics.

```powershell
./benchmarks/Invoke-BenchmarkMatrix.ps1 -SpecificationPath ./out/benchmark/matrix.json -ReportDirectory ./out/benchmark/plan-01 -PlanOnly
./benchmarks/Invoke-BenchmarkMatrix.ps1 -SpecificationPath ./out/benchmark/matrix.json -ReportDirectory ./out/benchmark/matrix-01
./benchmarks/Verify-BenchmarkMatrix.ps1
```

The matrix specification is JSON with these members:

| Member | Meaning |
| --- | --- |
| `RepositoryRoot` | Existing repository for environment snapshots |
| `Scope` | `screening` or `full`; this label does not prove workload coverage |
| `DeadlineUtc` | Original shared experiment deadline, unexpired and at most 12 hours away |
| `MemoryLimitBytes`, `MemoryReserveBytes` | Positive integer workload ceiling and free-memory reserve |
| `InputFiles` | Nonempty array of immutable `{ "Path": "...", "Sha256": "..." }` bindings |
| `Workloads` | Exactly `artifact-cold`, `unchanged`, `declaration-edit`, `generator-change`, and `missing-output` |

Each workload has `Name`, integer `Samples`, and `baseline` / `candidate` objects. Each variant
object has nonempty `Prepare`, `Measure`, and `Verify` arrays of stage-specification file paths.
Each stage file supplies `Executable`, string-array `Arguments`, existing `WorkingDirectory`, and
integer `TimeLimitSeconds`. The matrix supplies the label, common deadline, and memory ceiling.
Paths resolve against the invocation directory; prefer absolute paths in a frozen specification.
An argument that is exactly `{SampleRoot}`, `{Sequence}`, `{Workload}`, `{Variant}`, `{Repetition}`,
or `{IsWarmup}` is replaced with that sample's context. Placeholders embedded in a larger argument
and unknown whole-argument placeholders are rejected; pass paths and values as separate arguments.

Preparation must reset the disposable variant to the declared cache/edit state for every sample,
including artifact removal for independent cold samples. Verification commands must fail nonzero
unless expected source/export inventories, rewritten-file counts, missing/stale-output handling,
and the applicable native boundary checks pass. A successful command alone is not evidence that
those checks were adequate. Pin immutable source/toolchain/input manifests and guard scripts;
perform deliberate declaration/generator edits only in disposable working copies. A manifest hash
does not verify its referenced live files: supply a verifier that checks those files as well.

The runner binds the matrix, stage specifications, measurement/preflight scripts, and declared input
files; checks them before phases and after samples; and takes resource snapshots before and after
each sample. Any detected mismatch, resource rejection, failed phase, failed verification, or expired
deadline stops the matrix. Completed samples and failed-phase logs are retained under a fresh report
directory. Partial statistics are diagnostic only when `Succeeded` is false. A `-PlanOnly` run checks
the schedule and declared bindings but performs neither resource checks nor workloads.

The reported median/range sums only `Measure` phases, not end-to-end elapsed time. Inspect per-stage
reports to distinguish parse/model, emission/write, configure, compile, and link. This control layer
does not supply OCCT workload commands, native correctness assertions, per-file outliers, or TU counts.
Pre/post snapshots still cannot rule out transient contention during a sample. There is no automatic
recommendation: full-workload correctness and resource evidence must accompany any adoption proposal.
Do not reset the deadline for another candidate, a retry, or a subsequent invocation of the same
experiment. The coordinator retains the original deadline across invocations; the script is not a
cross-invocation machine-time ledger and has no automatic resume.

`Verify-BenchmarkMatrix.ps1` runs deterministic orchestration fixtures in a disposable copy of
the runner with stubbed process/resource tools. It checks paired ordering, warmup/statistics exclusion,
shared deadlines and ceilings, plan-only behavior, invalid specifications, immutable evidence, and
fail-closed resource/input/verification behavior. Its 56 synthetic samples are **not measurements**;
real process measurement remains covered separately by `Verify-BenchmarkStage.ps1`. Add
`-RealProcess` to also test the matrix with the actual stage runner and controlled PowerShell children:
an exit-17 verification must invalidate the entire sample. That optional integration check needs CIM
access, still uses fixture resource snapshots, and is never a performance sample.

## Native command accounting

`Get-NinjaBuildMetrics.ps1` reads complete Ninja v5/v7 log snapshots outside the timed interval.
For a warm sample, capture `.ninja_log` before and after exactly one invocation and supply both:

```powershell
./benchmarks/Get-NinjaBuildMetrics.ps1 -BeforeLogPath ./out/benchmark/before.ninja_log -LogPath ./out/benchmark/after.ninja_log -ReportPath ./out/benchmark/native-metrics.json
./benchmarks/Verify-NinjaBuildMetrics.ps1
```

Omit `BeforeLogPath` only for a fresh log containing one invocation. Stop writers before capture.
The parser rejects malformed/truncated snapshots, overwritten reports, duplicate outputs, and
non-prefix deltas caused by log replacement or recompaction. It cannot establish invocation
boundaries itself. An unchanged log yields zero commands; parallel completion rows may arrive out
of timestamp order. Output extensions identify compile/link commands. Multiple outputs with the
same command hash and timing, such as a DLL and its import library, count as one command.

The report retains all commands, the 20 longest compilations, link durations, aggregate command
duration, and logged execution span. Aggregate duration overlaps across workers and is **not wall
time**. Neither the longest file nor the logged span proves the dependency critical path; process
timing, build success, workload isolation, and resource evidence still come from the stage runner.
Custom multi-object commands need a separate source inventory. These diagnostics are not a speedup
claim, and historical verification builds are not benchmark samples.

## OCCT workload plan

`New-OcctBenchmarkPlan.ps1` freezes the approved five-workload OCCT matrix. It requires the exact
baseline commit `e94f10a9bb9490d47363cf43d8ce17600b435b8a`, candidate behavior commit
`9952e5a76358028c22c8ec215a23d7b82413ad4f`, clean repositories, a frozen harmless generator-change
patch, prebuilt original/changed hosts, equal-length isolated artifact and input roots, and a shared deadline.
The initial plan intentionally retains `commit:PENDING-FINAL-HARNESS-COMMIT`; regenerate it with
the final harness commit before execution. `Invoke-OcctBenchmarkWorkload.ps1` rejects that pending
binding, so a template plan cannot accidentally become a performance run.

The editable inputs are private physical copies of `installed/x64-windows/include` and the vcpkg
status file. The real vcpkg root is used only as the CMake toolchain and must be a different path.
Prepare and verify stages hash the live private-header inventory. Generator stages invoke the exact
Console DLL directly with `dotnet <Console.dll> --output-root <isolated-root>`; they never call
`Build/GenerateWindowsBindings.ps1` and therefore cannot be hidden by its generation cache.

The generated stage plan has these fixed semantics:

| Workload | Measured stages | Preparation and verification |
| --- | --- | --- |
| `artifact-cold` | generate, `cmake --fresh` configure, build | Remove only a marker-owned artifact root, then retain source and Ninja evidence |
| `unchanged` | generate, build | Compare source manifests and Ninja-log delta; candidate must rewrite zero sources |
| `declaration-edit` | generate, build | Apply one frozen header replacement in the private copy, verify a content delta, then restore canonical state outside timing |
| `generator-change` | changed-host generate, build | Use a prebuilt immutable changed host, verify unchanged output content, then restore with the original host outside timing |
| `missing-output` | generate, build | Delete one frozen `cpp/*.cpp`, verify restoration and the source/Ninja delta |

All native builds use the declared fixed parallelism. Every sample receives manifests and parsed
Ninja evidence through `{SampleRoot}`. `cmake --fresh` is rejected outside artifact-cold. Root
ownership markers, immutable file bindings, reparse-point rejection, private-input inventory checks,
and non-overwrite evidence make destructive or stale execution fail closed. This is experiment
infrastructure only and grants no production-adoption authority.

```powershell
./benchmarks/Verify-OcctBenchmarkPlan.ps1
```

The verifier creates only tiny header/host/tool fixtures, generates a plan, and exercises matrix
`-PlanOnly`; it does not run the OCCT generator or compile native code.

## Reference workload inventory

The 2026-09-04 manifest of the previously verified isolated build contains 14,514 source/support
files totaling 400,648,748 bytes: 7,521 managed files and 6,993 native/support files. The largest
managed file is `OpenGl_SetOfPrograms.g.cs` (22,443,193 bytes); `NativeFunctionTable.cpp` is
14,272,835 bytes. These are corpus measurements, not speed results. They justify including
per-file emission outliers in screening; size alone does not establish the critical path.

The isolated Generator, header source-generator, Console, and shared build/package properties were
compared with commit `ce469d5cfa1befc8a9466638836463daff916c92` without a content delta. This does not
bind all native/toolchain inputs or the current main worktree's uncommitted template changes.
Rebind the selected baseline and regenerate the manifest when preparing actual samples.

`WriteIfChangedProbe` is an experiment-only public-consumer probe. Point `GeneratorProjectPath` at
an isolated baseline or candidate project and give it a disposable output directory. It verifies
unchanged timestamps, selective replacement, stale and missing output handling, failed-render
rollback, and file/directory transitions. It is correctness evidence, not a timing sample.

## Remaining experiment work

The paired execution control and OCCT workload adapter are implemented; prepared full-workload inputs,
final harness binding, repeated measurements, native boundary comparison, generation per-file timings,
and the decision report remain unfinished. Resource reports and synthetic fixtures are not
timing samples. Do not count the earlier build-verification duration as a benchmark or recommend
production adoption from it.
