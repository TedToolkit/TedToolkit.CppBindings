# Binding generation performance

## Status and authority

The bounded 2026-09-05 write-if-changed screening is **inconclusive**. No complete
baseline/candidate workload pair was accepted, so the experiment supplies no speedup,
regression, or production-adoption evidence.

- Scope: screening, exactly one recorded pair for each of five workloads, with no warmups.
- Result: incomplete; zero accepted samples.
- Recommendation sampling: not satisfied. The 20% improvement and 5% regression thresholds
  apply only to a successful full matrix and were not evaluated.
- Production adoption: not authorized. Write-if-changed generation remains an external
  experiment candidate, not the repository default.

## Bound comparison

| Binding | Value |
| --- | --- |
| Baseline revision | `e94f10a9bb9490d47363cf43d8ce17600b435b8a` |
| Candidate behavior revision | `9952e5a76358028c22c8ec215a23d7b82413ad4f` |
| Final harness and original candidate host | `bde7b7eae61819710f9bbfa54a4a1aa7939aba9f` |
| Changed-declaration candidate host | `f6bef902ed4df2870663436e8c298cfb174d9693` |
| Frozen generator patch SHA-256 | `2BA94EFA37793FCC3F0FB995FBF6AAD558E2682D2B77A30D44577EBC4BCFBDC9` |
| Screening plan ID | `fd389377524c43b2bf35630939ea17dc` |
| Shared deadline | `2026-09-05T12:32:40Z` |
| Parallelism | 4 |
| Process-tree limit / system reserve | 10 GiB / 4 GiB |

The final plan is `out/benchmark/formal/screening-plan-02/matrix.json`, SHA-256
`8A8537420FAFF030B6FADB9DD6DEC24115AD1F15F5296603EA7C39F3EFA7472E`. Its OCCT plan is
SHA-256 `5A6F3B9417570B7316712F0A6F5BDA8EFF4A0B11DD78CEB0285328B8F98C2903`. The last
strict preflight is `out/benchmark/formal/evidence/screening-preflight-10.json`, SHA-256
`713F5FDC157BFB216C4E8F42F67BB7DACE2DF4BA9E3F5AD880563B488E29CB62`; it recorded no
competing build and at least 14 GiB free physical memory.

The plan freezes .NET 10.0.400, CMake 4.4.3, Visual Studio 2026 MSVC 14.51.36231,
Clang 22.1.3, Windows SDK 10.0.26100.0, the Visual Studio Ninja executable, complete private
copies of the `x64-windows` vcpkg input, all four published generator hosts, and every harness,
canonical-oracle, export-inventory, and native-gate input. Refer to `benchmarks/README.md` and the
plan itself for the complete reproducible command contract.

## Screening outcome

No attempt produced an accepted sample:

| Attempt | Accepted samples | Disposition |
| --- | ---: | --- |
| `screening-results` | 0 | Harness cleanup failed after a successful cold baseline build because an archived CIM `CreationDate` was null. Result SHA-256: `615EAC644DFFB9BC21C0EBFD07BCEC649D7AC8018ADF772244CCFC3776A8D821`. |
| `screening-results-02` | 0 | Invalidated immediately after an external Step21 build overlapped the measured native build. |
| `screening-results-03` | 0 | The sample could not simultaneously fit the 10 GiB limit and 4 GiB reserve. Result SHA-256: `AEADF9E014ABD2464329C5EE93F8DA96D2230B71BE871A8CD5DC90F87AC5BE1A`. |
| `screening-results-04` | 0 | Coordinator recovery omitted the plan-owner marker; preparation failed before measurement. Result SHA-256: `CC6790A94E626FBBE4FF30D4D6950FAD576E26C0EE4384B9974C04F6C4370940`. |
| `screening-results-05` | 0 | Invalidated when a user-authorized Step21 build overlapped the measured cold baseline native build. |

Partial stage timings are diagnostic only and must not be compared or combined across attempts.
The first attempt did prove that its cold baseline workload generated and configured successfully,
then built and linked all 6,989 Ninja targets with exit code 0 before the old cleanup routine failed.
The cleanup defect was fixed by using the process identity captured during sampling rather than
re-reading a nullable archived timestamp. Regression coverage also proves that a stale identity
cannot terminate a process that merely reused the same PID. All seven harness verifier scripts pass
at the final harness revision.

The canonical correctness preparation remains usable independently of timing: 14,333 generated
files (296,126,257 bytes), exactly 97,913 case-sensitive unique native exports in the same order,
one expected managed-file content change for the declaration edit, and a passing native semantic
gate against the canonical DLL. These facts validate the oracle and harness inputs; they do not
validate the write-if-changed candidate's performance or authorize production use.

## Strategy disposition

| Strategy | Disposition |
| --- | --- |
| Write-if-changed generation with stale-output reconciliation | Inconclusive; do not adopt. A new isolated full experiment is required before a production design. |
| Shared bounded rendering budget | Deferred; unmeasured. |
| Reduced native header dependencies | Deferred; unmeasured. |
| Bounded native compilation | Used as a four-worker experiment control, not evaluated as a candidate; no adoption claim. |
| PCH or unity batches | Deferred; unmeasured. |
| Function-table sharding | Deferred; unmeasured. |

Any future experiment must run without competing builds, use fresh artifact roots and report
directories, freeze its own single shared deadline, and bind rebuilt hosts to the exact final
harness. Screening can establish directional feasibility only. A production proposal still needs
a separate approved change and successful full sampling.

## Limitations

- Artifact-cold does not mean OS-cache cold; hashing and prior attempts warm the file cache.
- CIM polling perturbs the workload. Working-set samples may double-count shared pages and miss
  between-sample peaks; process I/O counters are not physical disk traffic.
- Very short-lived or detached descendants can escape observation.
- Point-in-time preflight and pre/post manifests cannot prove that no unrecognized external
  contention occurred throughout a sample.
- Per-file emission timing, critical-path outliers, and a completed baseline/candidate timing pair
  are unavailable.
- Raw evidence contains machine-local absolute paths and remains under `out/benchmark/formal`; the
  committed harness, plan contract, hashes above, and this disposition are the durable record.
