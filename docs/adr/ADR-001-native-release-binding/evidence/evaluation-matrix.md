# Evaluation Matrix

Compatibility is a hard gate. Scores use 1 (poor) through 5 (best).

| Candidate | Legacy compatibility (40%) | Hot-path performance (30%) | Current `Handle<T>` fit (20%) | Simplicity (10%) | Weighted result |
|---|---:|---:|---:|---:|---:|
| `kernel32` + unmanaged function pointer | 5 | 5 | 5 | 4 | 4.9 |
| Static `DllImport` direct call | 5 | 5 | 2 | 5 | 4.4 |
| `kernel32` + marshaled delegate | 5 | 2 | 1 | 3 | 3.1 |
| `NativeLibrary` + unmanaged function pointer | 1 | 5 | 5 | 5 | 3.4 (disqualified) |

## Recommendation

Select `kernel32` loading plus direct unmanaged function-pointer invocation for Windows-generated
release bindings.

It is the only candidate that passes the `netstandard2.0`/Framework compatibility gate, retains the
existing `Handle<T>` representation, and matches the modern function-pointer hot path. Static
`DllImport` remains the default for normal generated calls when no raw address must be stored.

## Sensitivity

Changing performance or simplicity weights does not change the selection while legacy compatibility
and the existing generic `Handle<T>` shape remain hard constraints. Removing legacy compatibility
would make `NativeLibrary` equally valid, but the benchmark provides no reason to prefer it for
steady-state cleanup.

## Function-table storage extension

Compatibility remains a hard gate. The storage choice is separate from the loader choice.

| Candidate | Dispatch | Owner/module safety | Lifetime safety | Generated scale | Recommendation |
|---|---|---|---|---|---|
| Per-owner cleanup pointer | Same few-ns range | Preserves exact producing module | Safe while module is retained | One pointer per owner | Keep for `Handle<T>` and `Owned<T>` |
| Static typed field per export | Same few-ns range | Safe for ordinary calls | Process-lifetime field | Large generated field surface | Valid, not preferred at large scale |
| Static managed `IntPtr[]` | Same few-ns range | Safe for ordinary calls | GC-rooted; no explicit free | Compact and index-generated | Select for generated export cache |
| Static unmanaged `void**` | Same few-ns range | Safe for ordinary calls | Explicit allocation/free | Compact | Reject initially; no measured benefit over managed array |
| Owner stores table reference + index | Same order, noisy | Preserves table identity | Owner retains table object, not necessarily module | Adds two fields | Reject: 40 B vs current 32 B |
| Owner uses process-global table only | Same order, noisy | Loses per-owner module identity | Unsafe if table is replaced/freed | Smallest owner | Reject as a generic owner contract |

The selected hybrid is a generated static managed table for centralized export resolution and
ordinary dispatch, plus a table-to-owner handoff that copies the exact cleanup pointer into each
finalizable owner. It adopts Hexa's table organization without coupling runtime owners to global
generated state.
