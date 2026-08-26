# Hexa Function-Table Analysis

## Observed implementation

Hexa.NET.ImGui describes its binding as using a static function table and supports
`netstandard2.0`. The generated `ImGui` class contains one static `FunctionTable`, constructs it
with 1,548 slots, and loads each export by generated integer index. Generated API methods read a
slot, cast the raw address to the exact `delegate* unmanaged[Cdecl]<...>` signature, and invoke it.

The storage is not a managed `IntPtr[]`. HexaGen.Runtime's `FunctionTable` is a managed class holding
an unmanaged `void**` allocated with `Marshal.AllocHGlobal`. Its indexer directly reads and writes
that memory. The object also retains an `INativeContext`; `Free()` releases both the unmanaged table
and the context. Hexa.NET.ImGui exposes `FreeApi()` to trigger this operation.

Sources inspected at fixed commits:

- [Hexa.NET.ImGui README](https://github.com/HexaEngine/Hexa.NET.ImGui/blob/02593125062b94b24bf8b8f6c162fa54301f89cd/README.md)
- [Generated 1,548-slot table](https://github.com/HexaEngine/Hexa.NET.ImGui/blob/02593125062b94b24bf8b8f6c162fa54301f89cd/Hexa.NET.ImGui/Generated/FunctionTable.cs)
- [Generated indexed calls](https://github.com/HexaEngine/Hexa.NET.ImGui/blob/02593125062b94b24bf8b8f6c162fa54301f89cd/Hexa.NET.ImGui/Generated/Functions/Functions.000.cs)
- [HexaGen.Runtime table implementation](https://github.com/HexaEngine/HexaGen/blob/50547a081b978f682328c6644b0e91a774c79512/HexaGen.Runtime/FunctionTable.cs)

## What transfers to TedToolkit.Occt

The useful transferable idea is one generated publication boundary for all resolved exports:

- resolve and validate every required symbol once;
- assign stable generated slot constants;
- retain the module and table for as long as any call can occur; and
- cast each raw slot to the exact ABI signature at the call site.

This reduces generated storage declarations and makes missing-export validation explicit. It also
works without `NativeLibrary`, so the loader can remain compatible with `netstandard2.0` consumers.

## What does not transfer unchanged

TedToolkit.Occt has generic finalizable owners in a platform-neutral runtime assembly. Their cleanup
may run long after the generated factory call. Looking up cleanup through one mutable global table
would erase the identity of the module that produced the object. Storing a table reference and
index restores identity but enlarges the owner and couples Runtime to generated table machinery.

Hexa's explicit `FreeApi()` is also incompatible with live or finalizable owners unless the caller
can prove none remain. TedToolkit.Occt therefore keeps modules and tables for process lifetime in the
initial design and copies the exact cleanup pointer into each owner.

## Storage choice

The benchmark found no stable dispatch or initialization advantage for unmanaged table storage.
At 1,536 slots, a managed array uses about 12 KB once. The unmanaged candidate uses the same 12,288
bytes of native slot storage, which BenchmarkDotNet's managed allocation column intentionally does
not report, and adds explicit lifetime risk. A managed `IntPtr[]` is therefore the simpler initial
choice. An unmanaged table remains a future optimization only if profiling demonstrates a concrete
GC or startup problem.
