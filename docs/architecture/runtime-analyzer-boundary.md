# Runtime analyzer boundary

- Status: Active
- Owner: TedToolkit.Occt maintainers
- Scope and system boundary: Compile-time tooling that distinguishes generated binding
  implementation code from handwritten consumers of `TedToolkit.Occt.Runtime` and generated OCCT
  packages.
- Applicable product intent: None
- Governing principles: [Repository design principles](../principles/README.md)
- Related architecture: [Generated binding system](generated-binding-system.md)
- Last approved revision: Uncommitted working tree approved by the maintainer on 2026-08-26.

## Current architecture

Enforcement follows this order:

```text
API accessibility and type shape
             |
             v
Generator model validation and generated API shape
             |
             v
Runtime analyzer diagnostics at consumer compilation
             |
             v
Runtime invariant validation before native access
```

Each layer has a different responsibility:

| Component | Runs for | Responsibility | Must not own |
| --- | --- | --- | --- |
| C# accessibility and type system | Every compilation | Make unsafe capabilities non-public when possible and expose safe semantic receiver types | Caller heuristics or native runtime validation |
| `TedToolkit.Occt.Analyzer` | Generator development/build | Generate the Generator's configured OCCT-header input | Consumer policy, Runtime ownership, or native declaration validation |
| Generator semantic validation | Binding generation | Reject unsupported layout, transport, ownership, and operation models before emitting partial bindings | Handwritten consumer behavior |
| `TedToolkit.Occt.Runtime.Analyzers` | Runtime and generated-package consumers | Report handwritten use of explicitly marked generated-only Runtime hooks and supported suspicious uses of non-owning owner `Value` references | OCCT declaration knowledge, complete alias or concurrency proof, security, or runtime correctness |
| `TedToolkit.Occt.Runtime` | Managed execution | Validate supported types, pointer and function inputs, owner state, module origin and lifetime, and disposed state when native access is obtained | Revoking an already-returned reference, preventing concurrent disposal, generated declaration catalogs, or analyzer implementation |

The Runtime analyzer is a separate Roslyn compiler asset, not an extension of the existing
OCCT-header source generator. Runtime declares the sealed, declaration-agnostic public
`GeneratedCodeOnlyAttribute` needed to mark an unavoidable public constructor, method, property, or
event as reserved for generated callers. Runtime does not reference the analyzer; the analyzer
resolves the marker from symbols and has no dependency on a generated wrapper.

The analyzer has two focused responsibilities. The metadata-driven reserved-API usage rule,
`TTOCCT001`, reports an error when handwritten code operationally references a marked constructor,
method, property, event, or other callable member, including indirect capture for later invocation.
The first required application is the raw-pointer `Handle<T>` constructor. The same rule can cover
future generated-only `Owned<T>` admission or unavoidable public module, fingerprint, native
invocation, and cleanup hooks.

A separate declaration-agnostic lifetime rule reports supported suspicious uses of the non-owning
`ref T` returned by `Handle<T>.Value` or `Owned<T>.Value`. Its initial analysis covers recognizable
reference escape, capture or use across `await` or `yield`, access through a temporary owner, use
after a known `Dispose()`, disposal through a known alias while the reference remains live, use as
the receiver of a routine generated owner operation, and unmanaged use without a corresponding
`GC.KeepAlive(owner)` after the last use. New analyzer rules are added only for similarly bounded
semantic policies with objective Roslyn evidence; this rule does not promise whole-program alias,
thread, or lifetime analysis.

The analyzer configures `GeneratedCodeAnalysisFlags.None` and uses the Roslyn analyzer driver's
generated-code classification. Source-generator output and independent generators using standard
generated filenames, generated-file headers, or `generated_code = true` configuration receive the
same treatment. Recognition never depends on a wrapper assembly name, namespace, package identity,
`InternalsVisibleTo`, or special treatment of `TedToolkit.Occt.Windows`. It exempts a call site from
developer guidance; it does not authenticate the caller.

Normal consumer APIs remain usable: generated factories and extension operations, owner
`Dispose()`, non-owning data access through owner `Value`, exception handling, and unmarked public
Runtime contracts. The lifetime rule diagnoses supported unsafe usage shapes around `Value`; it does
not prohibit the property or convert it into an ownership token. Exact-layout structs remain
non-owning and the analyzer does not require them to implement `IDisposable`.

The analyzer ships in the separate `TedToolkit.Occt.Runtime.Analyzers` package with no runtime
assets. Its `buildTransitive` contract injects the analyzer for direct and indirect Runtime
consumers. The Runtime package depends on that package; generated binding packages depend on
Runtime and do not embed another analyzer copy. Runtime validation remains mandatory because
analyzer diagnostics can be absent, suppressed, or bypassed through reflection, `dynamic`, emitted
IL, or generated-code configuration. Runtime can reject an owner already disposed when `Value` is
obtained, but it cannot revoke the returned reference or prevent a later explicit concurrent
`Dispose()`. Suppressing or bypassing a lifetime diagnostic therefore leaves the caller responsible
for owner liveness and use-after-free risk.

## Constraints for change design

- Prefer `private`, `internal`, a safe public abstraction, and precise receiver types over analyzer
  enforcement. Mark an API generated-only only when generated cross-assembly access makes public
  visibility unavoidable.
- Keep the marker and analyzer declaration-agnostic. Neither may name an OCCT declaration, native
  symbol, generated wrapper assembly, platform package, or closed specialization.
- `TTOCCT001` is an error by default and states that the referenced API is reserved for generated
  OCCT binding code. Its documentation provides the supported generated factory or operation when
  one exists. Normal Roslyn suppression remains available.
- Analyze every operational symbol-reference form that can use or defer use of a marked member;
  do not report documentation references or other non-operational symbol mentions.
- Keep `Value` lifetime diagnostics bounded to locally demonstrable patterns. Document false-negative
  boundaries and normal suppression; do not describe the rule as proof against use-after-free.
- Generated unmanaged calls that derive an address or reference from a finalizable owner must emit
  `GC.KeepAlive(owner)` immediately after the last unmanaged use and before managed error projection.
  The analyzer verifies supported generated and handwritten shapes but does not treat `KeepAlive` as
  synchronization with explicit `Dispose()`.
- Generated-call recognition must remain aligned with documented Roslyn classification and be
  tested for source-generator output, standard generated files, configurable generated files, and
  ordinary handwritten files.
- Every generated-only Runtime entry point retains complete runtime validation of inputs and state
  that can be checked at invocation time. A change must not rely on the analyzer to protect memory,
  ownership, module lifetime, or native correctness. Already-returned `Value` references remain an
  explicitly caller-owned unsafe boundary.
- Package proof must show that `buildTransitive` adds one compile-time analyzer instance for direct
  and transitive Runtime consumers and adds no analyzer implementation asset to runtime output.

## Decision links and exceptions

This record refines the diagnostics boundary in the generated binding architecture. No separate
historical decision record is required while the analyzer remains a suppressible compile-time
guardrail over unavoidable public implementation hooks. Requiring trusted caller identity,
unsuppressible enforcement, or a privileged first-party wrapper would be a different architecture
and requires an approved architecture revision before delivery.

## Review triggers

Reassess this boundary when:

- a generated-only operation cannot be represented by the shared marker contract;
- a reserved API can become non-public or a safe public abstraction replaces it;
- handwritten third-party wrappers must call reserved APIs without suppression;
- caller identity must be trusted or diagnostics must be unsuppressible;
- analyzer logic needs declaration, platform, wrapper, or native-symbol knowledge;
- a new diagnostic requires whole-program ownership or alias proof, security, runtime authorization,
  or generated output correctness as its responsibility;
- owner finalization is removed, `GC.KeepAlive` placement changes, or direct `Value` access is
  expected to tolerate explicit concurrent disposal; or
- analyzer assets cannot be delivered exactly once without runtime dependencies.
