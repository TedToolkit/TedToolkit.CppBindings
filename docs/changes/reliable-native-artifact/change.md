# Reliably produce a linkable OCCT native shared library

<!-- change-format: 3 -->
<!-- workflow-profile: standard -->
<!-- change-kind: bug-fix -->
<!-- change-status: draft -->

- Priority: P1
- Approval: None while Draft.

<!-- section: goal-rationale -->
## Goal and rationale

When generated C++ wrapper sources are valid and a supported CMake/compiler/vcpkg OCCT environment is available, Generator must configure, compile, and link a loadable `ted_toolkit_occt` shared library, and any tool failure must fail the pipeline. The 47 generated translation units from the current Console example can pass clang-cl syntax and object compilation with C++ exceptions enabled, but linking fails because every translation unit includes and exports the same `free_error` definition from `csharp_interop.h`. The generated build also does not explicitly express the exception semantics required by OCCT headers.

<!-- section: scope -->
## Scope and non-goals

- In scope: make the shared error bridge ODR-correct and export it once; enable required C++ exception semantics for supported toolchains; produce a deterministic source list; validate configure, build, and final artifact results; propagate command failures and cancellation.
- Non-goals: change wrapper ABI type mapping, complete managed calls, force Ninja or one Visual Studio version on every consumer, install a system toolchain, update OCCT, or redesign package publishing.
- Preserved behavior: use the caller's `VCPKG_ROOT`, triplet, C++ standard, and vcpkg CMake toolchain; retain the `ted_toolkit_occt` target name and the configured C++ output tree.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Multi-translation-unit link | Every object that includes `csharp_interop.h` defines and exports `free_error`, causing duplicate symbols | The shared error bridge has exactly one ODR-valid exported definition for any number of wrappers | Wrappers may continue to use header-level inline exception helpers |
| OB-02 | Compiler semantics | Generated CMake does not explicitly express the exception requirement; clang-cl defaults reject OCCT headers that throw | Each supported compiler configuration enables standard C++ exception semantics required by OCCT | The C++ standard still comes from `GenerationOptions.CppVersion` |
| OB-03 | External command result | This layer does not explicitly validate the command result or expected binary | Configure/build nonzero exit, cancellation, or missing output fails the module with stage and tool diagnostics | Successful builds still use CMake and vcpkg |
| OB-04 | Build-tool selection | Omitting a CMake generator uses a machine default; the current Visual Studio 18 2026 default stalls at compiler identification while Ninja plus clang-cl configures and compiles | The build boundary uses an explicitly selected supported generator/toolchain from the calling environment and remains cancellable | This change does not download or install toolchains |

<!-- acceptance-case: AC-01 -->
### AC-01 — Multiple wrappers link into one shared library

```gherkin
Scenario: Multiple translation units use the error bridge
  Given two or more valid wrappers include the common interop header
  When a supported CMake toolchain builds the shared library
  Then linking succeeds and produces a loadable ted_toolkit_occt artifact
  And the artifact exports exactly one free_error symbol
```

<!-- acceptance-case: AC-02 -->
### AC-02 — The exception bridge works with a real compiler

```gherkin
Scenario: An OCCT call throws a standard native exception
  Given the shared library was built by a supported compiler configuration
  When a test export invokes the exception bridge
  Then no exception crosses the native boundary
  And the error payload can be released by the same shared library
```

<!-- acceptance-case: AC-03 -->
### AC-03 — A failed build cannot report success

```gherkin
Scenario: CMake configuration or compilation exits nonzero
  Given a controlled build command that fails
  When Generator executes the native build
  Then GenerateCpp fails with the stage and tool diagnostic
  And it does not return a missing or stale artifact as success
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Toolchain selection does not rely on an uncontrolled default

```gherkin
Scenario: The caller environment selects an installed supported CMake generator
  Given the vcpkg triplet is compatible with that toolchain
  When Generator configures the transient native project
  Then CMake uses the selection and completes configuration and build
  And cancellation stops the wait and cancels the pipeline
```

## Constraints and risks

- An ODR-valid declaration plus one implementation is a known direction, but source organization remains private. Linker flags that tolerate duplicate symbols must not hide the defect.
- The current machine's Visual Studio default failure does not justify making Ninja or clang-cl the only consumer choice.
- If reliable toolchain selection requires a new or changed public `GenerationOptions` contract, upgrade this change to Controlled and obtain approval before changing the API.
- Whether the command library already throws for nonzero exit must be verified with a controlled failure before changing it; AC-03 is required regardless of the internal mechanism.
- Parallel source-list mutation is a source-confirmed risk but not a reproduced root cause. Modify it only if evidence shows lost or nondeterministic entries; the final source list must still be complete and deterministic.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target area: the native error bridge, transient CMake project, and command-result boundary produce one verified shared library.
- Prerequisites: supported CMake/compiler, valid `VCPKG_ROOT`, compatible triplet and OCCT, and a Generator test project runnable as a Microsoft Testing Platform executable. The ABI change is not a prerequisite for correcting duplicate symbols and command propagation.
- Likely touchpoints (non-binding): native error bridge declaration/implementation, `GenerateCppModule`, `CppCompileCoontext`, Generator test configuration, native build tests, and Generator README.
- Private choices left open: source organization, CMake property expression, command-result adaptation, artifact discovery, and fixture layout.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance, regression, boundary | Integration with real CMake/linker | At least two wrappers link, the library loads, and `free_error` is unique | `dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx` with valid `VCPKG_ROOT`, executing the native integration fixture |
| AC-02 | Boundary and regression | Integration | A thrown native exception becomes a releasable error and does not cross the boundary | Same native integration fixture |
| AC-03 | Acceptance and regression | Component | A controlled nonzero command fails the module and does not return a successful artifact | Same Generator test command |
| AC-04 | Acceptance and operational boundary | Integration | A selected supported generator builds successfully and cancellation terminates execution | Same native integration and controlled cancellation fixtures |

Structural evidence: `dotnet build src/core/TedToolkit.Occt.Generator/TedToolkit.Occt.Generator.csproj -c Release` must succeed. A real shared-library existence, load, and export check cannot be replaced by C++ syntax-only compilation.

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-04 pass; a representative multi-translation-unit project configures, compiles, links, and loads on a supported Windows toolchain; failure and cancellation do not return success; `free_error` has one exported definition; Generator README records supported toolchain selection and environment prerequisites. Managed calls into generated symbols are outside this change.
