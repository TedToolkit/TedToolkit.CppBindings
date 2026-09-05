# Measure scalable binding-generation and native-build strategies

<!-- change-format: 3 -->
<!-- workflow-profile: standard -->
<!-- change-kind: experiment -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->

- Priority: P2
<!-- approval-source: user-approved-and-continue-in-thread-2026-09-04 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

Determine which optimization materially reduces full OCCT binding build cost without changing
binding semantics. An inspected output contained 6,991 C++ files (about 150.6 MB), 97,941 function
table entries, and a 14.3 MB function-table translation unit. These are workload observations, not
performance measurements. The generator currently clears outputs and rewrites all files; C++ uses
unbounded Task.WhenAll while C# uses separate bounded loops. More tasks alone is not a proven fix.

<!-- section: scope -->
## Scope and non-goals

- In scope: isolated benchmark harness, repeatable workload, stage timings, resource measurements,
  baseline/candidate comparison, and a ranked adoption or rejection report.
- Candidate strategies: write-if-changed generation with stale-output reconciliation; a shared
  bounded rendering budget; reduced native header dependencies; bounded native compilation;
  experimental PCH/unity batches and function-table sharding only if measurements justify them.
- Evaluate candidates incrementally, not as one inseparable optimization bundle. Unmeasured
  strategies remain explicitly deferred rather than being recommended as proven improvements.
- Non-goals: production adoption, ABI/API or ownership changes, fewer supported declarations,
  changing the supported Windows x64 matrix, package publication, or the CppBindings migration.
- Preserve exports and their exact order, generated layouts/signatures, native lifetime behavior,
  deterministic output, failure propagation, and cancellation. Source partitioning may differ only
  inside disposable prototypes and must preserve the resulting boundary contract.

<!-- section: experiment-contract -->
## Experiment contract

<!-- experiment: EXP-01 -->
- Question: which measured strategy gives the best cold-build and edit/rebuild improvement under
  a documented memory budget while preserving the complete binding contract?
- Method: pin the exact code/input candidate, OCCT/vcpkg revision, SDK/compiler/CMake versions,
  CPU, RAM, disk, worker counts, and cache state. Separate parse/model, managed emission, native
  emission/write, configure, compile, and link timings. Record wall time, peak process-tree memory,
  output bytes, rewritten files, compiled translation units, emission outliers by file size/time,
  and available CPU/I/O counters. Distinguish a single-file critical path from aggregate worker load.
- Workloads: full cold artifact build; unchanged warm rebuild; one representative declaration edit;
  a generator change; and deletion of one generated output. Distinguish artifact-cold from OS-cache
  cold and report unavailable measurements. Do not run competing builds during samples.
- Sampling: screening runs exactly one recorded baseline/candidate pair, no warmup, for each of all
  five full-public-header workloads; alternate which variant runs first across workloads. Screening
  supports correctness, resource, and directional-feasibility decisions only. Full sampling adds
  one unrecorded warmup per variant/workload, at least five warm samples, and at least three
  independent artifact-cold samples. Alternate baseline/candidate order and retain raw samples,
  medians, ranges, failures, and exact commands.
- Decision signal: apply the 20% improvement and 5% regression thresholds only to a successful full
  sampling result. Recommend further production design only for a repeatable median improvement of
  at least 20% in a declared primary workload, no correctness failures, no greater than 5%
  regression in another measured workload beyond observed noise, and peak memory within the
  recorded machine budget. Screening and failed/partial matrices never satisfy recommendation
  sampling requirements. An unchanged warm generation must rewrite zero sources; a missing or stale
  output must be restored or fail closed. Report trade-offs and inconclusive results honestly.
- Stop condition: stop at 12 machine-hours, a correctness failure, or a resource-budget violation;
  stop a failing variant rather than silently changing the workload. Rebaseline if code, native
  inputs, or toolchain changes. Unconfirmed results remain inconclusive.
- Evidence owner: the repository maintainer's experiment delivery agent. Record the reproducible
  method and results in a current performance guide before cleaning this temporary change record.
- Production authority: none. The maintainer approved execution of this bounded experiment on
  2026-09-04. A successful experiment still requires a separate approved implementation change.

## Constraints and risks

Use isolated output/cache locations and preserve user edits. Do not weaken tests to improve times.
Freeze models and the function-table ordering before parallel rendering; do not assume Clang-backed
mutable model traversal is thread-safe. Bound both generation and compiler concurrency. Any public
contract change, new platform, external service, or production adoption requires renewed design.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

No cross-change prerequisite. Before execution select one reproducible baseline with known test
outcomes and the pinned Windows toolchain; identify existing failures separately from prototype
regressions. Do not compare different Binding migration states as a performance experiment.

<!-- section: delivery-brief -->
## Delivery brief

One isolated comparative experiment produces the harness, raw measurements, reproducible commands,
and a ranked decision report. Prototype algorithms, worker counts, screening subsets, and report
layout are private choices; neither a specific speedup nor production adoption is guaranteed.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: EXP-01 purpose=decision shape=benchmark -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| EXP-01 | Primary | Repeated baseline/candidate samples support a measured recommendation, rejection, or inconclusive result under the stated budget and correctness constraints | Run the pinned cold/warm/edit/missing-output matrix above; retain exact harness commands, raw measurements, output-contract comparison, and environment with the report |
| Correctness | Conditional | Accepted prototypes preserve exports/order, generated managed contracts, native lifetimes, and failure behavior | Compare deterministic manifests and run existing Generator, Runtime, Analyzer TUnit projects plus native integration against each recommended full-workload prototype |

<!-- section: completion-criteria -->
## Completion

The bounded experiment has a reproducible decision report with raw evidence, limitations, and an
explicit disposition for every attempted or deferred strategy. No prototype becomes the production
default. Enduring methodology and validated conclusions live outside docs/changes before cleanup.

## Result

The final screening plan bound exactly ten executions across the five approved workloads, but no
complete baseline/candidate pair was accepted before the shared deadline. Two runs were invalidated
by competing native builds, and the remaining attempts stopped on harness, recovery, or resource
conditions. Recommendation sampling was therefore not satisfied, the full-sampling thresholds were
not evaluated, and write-if-changed generation is not recommended for production adoption.

The reproducible harness and its seven verifier scripts pass at
`bde7b7eae61819710f9bbfa54a4a1aa7939aba9f`. Canonical preparation established identical ordered
exports, the expected single managed declaration delta, and a passing native semantic gate. Those
checks validate the experiment inputs and correctness oracle, not candidate performance. The durable
decision, bindings, evidence hashes, attempt dispositions, deferred strategies, and limitations are
recorded in `docs/performance/binding-generation.md`.
