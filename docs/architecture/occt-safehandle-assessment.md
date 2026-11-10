# SafeHandle assessment for OCCT intrusive owners

**Recommendation.** Keep `Handle<T>` as the public owner and defer a runtime migration. The
incremental value of a base-class change is low under the current ownership contract. There is a
meaningful safety benefit if a consumer needs a generated synchronous call to finish while
another thread disposes its owner. That requires a call-scoped lifetime guard in generated code
and a defined disposal contract.

This assessment is based on `development` at `c9e8b2a8703e1cb4104bff4a63ffbfc61414bdca`.

## Current behavior

- `Handle<T>` adopts one OCCT intrusive reference and saves the producing module's release
  callback. `Dispose` and the
  finalizer share an atomic pointer exchange, so aliases release that reference at most once.
  Runtime tests cover repeated disposal, abandoned owners, access after disposal, and selecting
  the matching release callback for each wrapper.
  [Handle implementation](../../src/providers/occt/TedToolkit.CppBindings.Occt.Runtime/Handle.cs)
  · [Handle tests](../../tests/TedToolkit.CppBindings.Runtime.Tests/HandleTests)
- Generated wrappers invoke unmanaged Cdecl function pointers from the native function table.
  They keep owners reachable with `GC.KeepAlive` after the last native use. An explicit
  `Dispose` can still release the reference during that use. The generated binding keeps the
  native module loaded for the process lifetime.
  [Generated binding architecture](generated-binding-system.md)
- Lowercase `handle<T>` is the pointer-sized borrowed OCCT layout and must remain unmanaged.
  `Owned<T>` separately manages C++ RAII objects placement-constructed in managed storage.
  [Borrowed handle layout](../../src/providers/occt/TedToolkit.CppBindings.Occt.Runtime/HandleValue.cs)
  · [RAII owner](../../src/shared/TedToolkit.CppBindings.Runtime/Owned.cs)

## Incremental value and cost

| Area | Impact in CppBindings |
| --- | --- |
| Disposal and finalization | **Low incremental value.** `SafeHandle` supplies a CLR-managed release state and critical finalization. The current owner already supplies deterministic, at-most-once release and a finalizer fallback. The release callback must still come from the producing module and remain callable until cleanup. |
| Synchronous calls overlapping disposal | **Meaningful if required.** A matched `DangerousAddRef` and `DangerousRelease` can defer the OCCT release until the last guarded call exits. The counter tracks managed use of the same intrusive reference. |
| Borrowed access | A call-scoped guard ends before the caller uses a returned `ref T` or `ref readonly T`. Direct `Value`, lowercase `handle<T>`, and pointers retained by native code still depend on their original lifetime and invalidation rules. Synchronization of native reads and writes is a separate concern. |
| Public API | Inheriting publicly exposes `Close`, `DangerousGetHandle`, `DangerousRelease`, and `SetHandleAsInvalid` even if the existing type name is preserved. The architecture and runtime tests constrain the public surface. An internal `SafeHandle` held by `Handle<T>` can preserve that surface, at the cost of a second managed object per owner. |
| Dispatch and allocation | Function-table calls bypass P/Invoke marshalling and require explicit guards for call protection. These add atomic operations per guarded owner and a `finally` path. Current function-table benchmarks compare dispatch and owner storage shapes. Measuring the cost of guarding actual OCCT calls would require a separate benchmark. |

[.NET SafeHandle contract](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle?view=net-8.0)
· [.NET 8 release and reference counting implementation](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeHandle.cs)
· [Function-table benchmark report](../adr/ADR-001-native-release-binding/evidence/benchmark/function-table-report.md)

A standalone Release probe on .NET 8.0.33 x64 confirmed the distinction between requesting
disposal and releasing the last reference. The probe acquired one guard, called `Dispose`, then
successfully acquired a second guard while the first was still held. Its release callback ran
once, after both guards were released. A design that promises to reject new work after disposal
starts therefore needs an explicit admission rule. It also needs to specify whether `Dispose`
may return before native release and whether the last caller may run the release callback.

## When to revisit

Revisit if a consumer needs concurrent shutdown and in-flight calls, or if a concrete incident
shows that the caller cannot coordinate their lifetimes. Start with one generated owning receiver
and keep the native function-table signature. Compare an internal `SafeHandle` with an equivalent
call guard in the existing owner. If the generator needs a new Runtime entry point, review that
contract and the generator/analyzer boundary together.

Before expanding the scope, verify with a blocking native fixture that an admitted call finishes
before release, that disposal obeys the chosen admission rule, and that cleanup runs once. Cover
the error path, multiple owners, null and owning results, and existing borrowed returns. Measure
allocation per owner and overhead on a small call and a representative OCCT workload, then verify
the supported .NET 8 and Native AOT paths. The standalone probe checked CLR lifetime behavior.
Generated OCCT bindings and performance were outside that check.
