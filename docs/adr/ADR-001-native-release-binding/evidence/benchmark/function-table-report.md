# Benchmark Report: Static Function Tables

## Decision

Adopt a generated static function table as an export cache, not as a faster replacement for the
cleanup pointer stored by `Handle<T>` or `Owned<T>`.

Use a managed `IntPtr[]` initially. Generated ordinary calls may index it directly, while generated
owner factories read the cleanup slot once and pass the typed pointer into the finalizable owner.
This keeps Runtime independent of generated modules and preserves the exact producing module.

## Dispatch results

Mean nanoseconds per call to the same no-inline native Cdecl probe:

| Runtime | Instance pointer | Static typed pointer | Managed table, constant slot | Managed table, runtime slot | Unmanaged table, constant slot | Unmanaged table, runtime slot | Hexa-style indexer |
|---|---:|---:|---:|---:|---:|---:|---:|
| .NET Framework 4.8.1 | 3.434 | 4.127 | 3.597 | 3.982 | 3.713 | 4.011 | 3.877 |
| .NET 8.0.30 | 3.558 | 4.362 | 4.426 | 3.695 | 4.691 | 3.314 | 4.262 |
| .NET 9.0.19 | 6.105 | 5.268 | 5.130 | 4.549 | 5.582 | 4.493 | 3.977 |
| .NET 10.0.11 | 2.971 | 2.607 | 2.397 | 2.775 | 2.352 | 2.518 | 2.793 |

All paths allocated zero managed bytes per call. Rankings reverse across runtimes: every table form
is slower than the instance field on .NET Framework, while several appear faster on .NET 9/10. The
same machine showed substantial thermal/frequency drift and broad confidence intervals. These data
support equivalence at the native-call scale; they do not support the claim that array indexing is
intrinsically faster. A table read cannot remove the final unmanaged indirect call and may add a
root, slot, bounds, or pointer load depending on the JIT.

## Finalizable owner storage

The benchmark allocates and disposes a finalizable Handle-shaped surrogate around the same native
create/release pair. Allocation is more reliable than the noisy timing column:

| Owner representation | Managed bytes per owner (all four runtimes) | Relative to current shape |
|---|---:|---:|
| Native value + instance cleanup pointer | 32 B | Baseline |
| Native value + process-global typed pointer/table | 24 B | -8 B / -25% |
| Native value + table reference + slot index | 40 B | +8 B / +25% |

The 24 B form is only correct under a hard single-global-module invariant: the table cannot be
replaced or freed while any owner remains finalizable. That assumption is not suitable for the
generic Runtime contract. The 40 B form preserves table identity but is worse than storing the exact
cleanup pointer. Therefore owner cleanup continues to use the per-instance pointer.

The exact allocation effect on `Owned<T>` also depends on `sizeof(T)`, alignment, and object padding;
the Handle-shaped measurement must not be generalized into an unconditional 8 B saving for every
`Owned<T>` specialization.

## Table initialization

Mean time to allocate a table and resolve the same export into every slot:

| Runtime | Slots | Managed array | Unmanaged table | Managed allocation |
|---|---:|---:|---:|---:|
| .NET Framework 4.8.1 | 16 / 256 / 1,536 | 1.855 / 31.857 / 164.888 us | 1.977 / 33.026 / 167.773 us | 152 / 2,078 / 12,336 B |
| .NET 8.0.30 | 16 / 256 / 1,536 | 2.876 / 32.774 / 190.312 us | 2.852 / 33.419 / 197.129 us | 152 / 2,072 / 12,312 B |
| .NET 9.0.19 | 16 / 256 / 1,536 | 1.969 / 33.455 / 199.725 us | 2.251 / 31.581 / 191.222 us | 152 / 2,072 / 12,312 B |
| .NET 10.0.11 | 16 / 256 / 1,536 | 1.975 / 29.488 / 234.783 us | 1.977 / 31.576 / 215.536 us | 152 / 2,072 / 12,312 B |

The slash-separated values correspond to 16, 256, and 1,536 slots. Initialization differences are
small and inconsistent. The unmanaged benchmark reports zero *managed* allocation because its
`8 * slotCount` bytes are allocated outside the GC heap and freed explicitly; it does not eliminate
the storage.

Production initializes once and retains the table, so neither sub-millisecond result affects owner
lifecycle cost. Missing-symbol validation, publication safety, and module lifetime matter more.

## Limits

- One Windows x64 laptop was measured; power and thermal drift was visible.
- Each method was measured in its own BenchmarkDotNet process, so very small cross-method rankings
  remain sensitive to machine state.
- The native probe is intentionally cheaper than OCCT work and isolates dispatch mechanics.
- Initialization repeatedly resolves one export; it measures storage and loader-call scaling, not a
  particular OCCT library's complete symbol-name distribution.

Full Markdown and JSON reports are retained under `function-table-results/` for `net48`, `net8.0`,
`net9.0`, and `net10.0`.
