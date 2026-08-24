# Make target-scoped OCCT parsing trustworthy

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: bug-fix -->
<!-- change-status: completed -->

- Priority: P1
- Approval: Approved by the repository owner on 2026-08-24 for Draft SHA-256 `BB8E9D382F4959A781E203E0AA3CADC821ED649937407E7D50AA01D5D14514D7`; this authorizes the single Controlled delivery below.

<!-- section: goal-rationale -->
## Goal and rationale

When a caller selects one or more OCCT record targets, the pipeline must parse only the corresponding public headers and must start both generators only after parsing has produced a complete model for every target. [`VcpkgEnvironment`](../../../src/core/TedToolkit.Occt.Generator/Services/VcpkgEnvironment.cs) currently aggregates every non-deprecated `.hxx` under the selected vcpkg OCCT include directory. A real `Geom2d_BSplineCurve` run therefore parsed unrelated headers, reported unresolved `BRepGProp_Face` declarations and a missing private `GeomBndLib_InfiniteHelpers.pxx`, yet [`ParseModule`](../../../src/core/TedToolkit.Occt.Generator/Modules/ParseModule.cs) still returned success. The same run showed that a Clang warning containing `[-Wpragma-once-outside-header]` caused a secondary logger markup error, while [`GenerateCSharpModule`](../../../src/core/TedToolkit.Occt.Generator/Modules/GenerateCSharpModule.cs) completed before Parse with an empty model. A successful module result therefore does not currently prove that the selected targets were parsed or generated.

<!-- section: scope -->
## Scope and non-goals

- In scope: interpret each `DeclOptions.FileName` as both a top-level OCCT record name and its public header stem; validate and de-duplicate that target set; include only `<FileName>.hxx`; let selected headers bring their own transitive dependencies; validate every requested record definition before adding any target to the shared model; make Clean and Parse independent prerequisites of both Generate modules; fail on Clang Error/Fatal diagnostics; safely preserve diagnostic text that contains markup characters.
- Non-goals: add direct enum or namespaced-record targets, support a target whose declaration and header stem differ, change recursive dependency discovery after a target is accepted, change the interop ABI, complete managed invocation, fix native linking, decide `Standard_Transient` ownership, or update the vcpkg OCCT package.
- Preserved behavior: caller-supplied Clang arguments, triplet selection, C++ language version, and vcpkg include roots remain effective. Warnings remain visible but do not fail parsing by themselves. Successful target parsing may still recursively collect related records and enums required by generation. Clean and Parse may run concurrently because they do not share output state; the two generators remain independent after both prerequisites complete.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Relay translation unit | Includes every non-deprecated OCCT `.hxx`, including unrelated broken headers | Includes only the selected declarations' public headers; those headers supply transitive dependencies | vcpkg include roots and triplet rules remain unchanged |
| OB-02 | Clang diagnostics and module result | Error/Fatal is logged but Parse still succeeds; some diagnostic text causes a second logger markup error | Diagnostics remain readable; any Error/Fatal fails Parse without corrupting the original message | Warning alone does not fail Parse |
| OB-03 | Pipeline scheduling | C# generation depends only on Clean and can run before Parse | C++ and C# generation both require successful Clean and Parse; neither runs after either prerequisite fails | Clean and Parse may run concurrently, and the two generators remain independent after both finish |
| OB-04 | Target contract | Empty, malformed, missing, or duplicate target values can produce all-header parsing, malformed relay source, or a false empty success | At least one valid header stem is required; exact duplicates are coalesced; missing headers and unresolved record definitions fail with the target name | Target names continue to come from `DeclOptions.FileName` |

<!-- acceptance-case: AC-01 -->
### AC-01 — An unrelated broken header does not affect the target

```gherkin
Scenario: Parse only the requested public header
  Given one parseable target header and one unrequested header that cannot be parsed
  When the pipeline requests the target declaration
  Then Parse succeeds and the model contains that declaration
  And the unrelated header is absent from the relay translation unit
```

<!-- acceptance-case: AC-02 -->
### AC-02 — A target parsing error stops the pipeline

```gherkin
Scenario: The requested header contains a compiler error
  Given the requested OCCT header produces a Clang Error or Fatal diagnostic
  When the pipeline executes Parse
  Then the pipeline fails with the original diagnostic
  And neither C++ nor C# generation runs
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Successful Parse precedes both generators

```gherkin
Scenario: Every target declaration is parseable
  Given every requested declaration can be parsed
  When the pipeline resolves and executes its module dependencies
  Then Clean and Parse may execute independently
  And both generators start only after both Clean and Parse succeed
  And both generators read the same completed model containing every requested declaration

Scenario: Cleaning fails
  Given Parse can complete but Clean fails
  When the pipeline resolves and executes its module dependencies
  Then neither C++ nor C# generation runs
```

<!-- acceptance-case: AC-04 -->
### AC-04 — A requested declaration must exist

```gherkin
Scenario: One target resolves and another target does not
  Given one selected public header defines its target and another parseable selected header does not define its target
  When the pipeline executes Parse
  Then it fails with an error that names the unresolved declaration
  And neither target has been added to the shared model
  And it emits no partial generated output
```

<!-- acceptance-case: AC-05 -->
### AC-05 — The target set is validated before Clang parsing

```gherkin
Scenario: No target is configured
  Given the target collection is null or empty
  When the pipeline prepares the relay translation unit
  Then it fails with a reason that at least one target is required
  And it does not invoke Clang or either generator

Scenario: A target value is malformed
  Given a target entry is null, blank, or outside the approved header-stem grammar
  When the pipeline prepares the relay translation unit
  Then it fails with the target index, display value, and expected header-stem format
  And it does not invoke Clang or either generator

Scenario: The selected public header is absent
  Given a valid target stem whose `<stem>.hxx` file does not exist below the active OCCT include root
  When the pipeline prepares the relay translation unit
  Then it fails with the target stem and resolved include root
  And it does not invoke Clang or either generator
```

<!-- acceptance-case: AC-06 -->
### AC-06 — Duplicate targets and warnings remain deterministic

```gherkin
Scenario: A valid target is repeated and Clang emits a warning containing markup characters
  Given the same target stem appears more than once
  When the target header is parsed successfully
  Then the relay contains one include for that target
  And the warning is logged literally without failing Parse
  And the target enters the model once
```

## Constraints and risks

- Target selection must not be implemented by suppressing diagnostics or hard-coding an include order for the current OCCT release.
- Parsing must continue to exercise real Clang/OCCT semantics; mocked module ordering alone is insufficient proof.
- A valid target stem matches the ASCII C++ identifier grammar `[A-Za-z_][A-Za-z0-9_]*` and therefore contains no path, extension, whitespace, quote, or directive text. The selected include is exactly `<stem>.hxx` below the active OCCT include root. Target matching is ordinal and duplicate target values are coalesced without changing declaration meaning.
- Forward declarations and the definition of the same canonical record count as one target. Parse validates that every target has a definition before the shared model is committed for generation.
- If a declaration cannot be located through this public-header convention, expand an explicit configuration contract in a separately approved change rather than falling back to all-header aggregation.
- Failure retains useful diagnostics but must not leave output that appears to be a successful current generation.

### Compatibility disposition and alternatives

- `DeclOptions` is public, so rejecting formerly constructible values is a deliberate compatibility change and makes this a Controlled delivery. Existing enum-based targets and string targets that use an exact OCCT record/header stem remain valid. Empty collections, malformed stems, missing headers, and stems without a same-named top-level record change from false success or downstream compiler failure to an early deterministic failure.
- A caller currently passing `Foo.hxx` migrates to `Foo`. A caller that genuinely needs different header and declaration names is not silently guessed; it requires a separately approved explicit mapping contract.
- Continuing all-header aggregation was rejected because unrelated headers can invalidate a selected target. Discovering declarations from a generated global index was deferred because no current requirement justifies another persisted index. Adding separate public header and declaration properties now was rejected as unnecessary API expansion for the supported OCCT naming convention.
- The rule is supported by the verified vcpkg OCCT 8.0.1 inventory: 6,878 `.hxx` files were inspected, and only the dotted stem `step.tab` falls outside the grammar. The current analyzer derives enum members from `.hxx` stems and excludes dotted stems; it does not itself enforce the full grammar. A future OCCT release that exposes an analyzer-generated target outside the approved grammar triggers compatibility reassessment rather than silent exclusion.

<!-- section: delivery-brief -->
## Delivery brief

- Delivery disposition: one Controlled delivery. Header selection, atomic target commitment, diagnostic failure propagation, and module scheduling must be implemented and proved together because the failure contracts cross those boundaries.
- Outcome and target area: repair Generator header discovery, Parse success semantics, and pipeline dependencies so one target-scoped parse is a reliable generation prerequisite.
- Start conditions and supplied inputs: the Generator test-project Release build reaches test-host compilation with zero diagnostics; controlled OCCT-style headers may be created as test fixtures; for required real-boundary proof, this workstation has OCCT under `C:\vcpkg\installed\x64-windows` and the command must set `VCPKG_ROOT=C:\vcpkg`.
- Likely touchpoints (non-binding): `DeclOptions`, `VcpkgEnvironment`, `IVcpkgEnvironment`, `ParseModule`, `GenerateCSharpModule`, pipeline registration/dependency metadata, Generator test project configuration, pipeline/module tests, and Generator README.
- Private choices left open: target-set representation, diagnostic exception type, test fixtures, and how tests observe module execution.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance and regression | Component with controlled headers and real Clang | The target succeeds and the unrelated broken header is not included | `dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release -- --report-trx` |
| AC-02 | Acceptance and regression | Component | Error/Fatal fails the pipeline, skips both generators, and logs without a secondary formatting error | Same Generator test command |
| AC-03 | Acceptance and structural verification | Component through actual module dependency metadata | Clean and Parse remain independent; both generators wait for both, are skipped when Clean fails, and observe the completed shared model after success | Same Generator test command |
| AC-04 | Acceptance and regression | Component with controlled headers and real Clang | With one valid and one unresolved target, Parse names the unresolved target while the shared model and generated output remain empty | Same Generator test command |
| AC-05 | Acceptance and regression | Component through target validation and the actual pipeline | Every invalid-input class produces its distinct diagnostic before Clang, and neither generator is invoked | Same Generator test command |
| AC-06 | Acceptance and regression | Component with controlled headers and real Clang | Duplicate targets produce one include/model entry; a bracketed warning remains literal and non-fatal | Same Generator test command |

Required boundary evidence: set `VCPKG_ROOT=C:\vcpkg`, execute the real-OCCT Parse boundary for `Geom2d_BSplineCurve`, and verify that the relay contains only `Geom2d_BSplineCurve.hxx`, the target reaches the model, and unrelated `BRepGProp_Gauss.hxx` or `GeomBndLib_Line.hxx` diagnostics are absent. This supplements rather than replaces the controlled regression proof and does not require the generated native library to link.

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-06 pass; the Generator and Generator test-project Release builds are clean; the required real vcpkg/OCCT boundary parses `Geom2d_BSplineCurve` without unrelated-header diagnostics; and README pipeline documentation matches the actual target convention, diagnostic threshold, and module dependencies. This change does not require the generated native library to link.

### Implementation evidence

- Completed on 2026-08-24. The Generator test-project Release build completed with zero warnings and zero errors.
- The 19 focused acceptance and regression tests passed: 10 target validation and relay tests, 5 Parse tests, 1 dependency metadata test, and 3 environment-driven real-boundary gate tests.
- With `VCPKG_ROOT=C:\vcpkg` supplied by the invoking process, the real OCCT boundary executed and passed with zero skipped tests. Without that environment prerequisite, it reports one explicit skip instead of assuming a workstation path.
- The approved full Generator command with the real boundary enabled executed 67 tests: 59 passed and the same 8 unrelated baseline tests failed before and after this change; no new regression failure was introduced.
- Independent implementation review concluded Ready to merge with complete AC-01 through AC-06 traceability, no blocking or important findings, and no design deviation.
