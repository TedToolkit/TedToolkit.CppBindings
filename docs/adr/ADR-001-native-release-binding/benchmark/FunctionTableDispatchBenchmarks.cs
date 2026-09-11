using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

[MemoryDiagnoser]
public unsafe class FunctionTableDispatchBenchmarks
{
    private IntPtr _counter;
    private IntPtr _module;
    private int _runtimeIndex = FunctionTableStorage.ReleaseSlot;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _instanceRelease;

    [GlobalSetup]
    public void Setup()
    {
        _counter = Marshal.AllocHGlobal(sizeof(long));
        Marshal.WriteInt64(_counter, 0);
        _module = NativeFixture.LoadWithKernel32();
        IntPtr release = NativeFixture.GetExportWithKernel32(_module, NativeFixture.ProbeExport);
        _instanceRelease = (delegate* unmanaged[Cdecl]<IntPtr, void>)release;
        FunctionTableStorage.Initialize(release);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        FunctionTableStorage.Free();
        NativeFixture.FreeWithKernel32(_module);
        Marshal.FreeHGlobal(_counter);
    }

    [Benchmark(Baseline = true)]
    public void InstanceFieldFunctionPointer() => _instanceRelease(_counter);

    [Benchmark]
    public void StaticTypedFunctionPointer() => FunctionTableStorage.StaticRelease(_counter);

    [Benchmark]
    public void ManagedArrayConstantIndex() =>
        ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.Managed[FunctionTableStorage.ReleaseSlot])(_counter);

    [Benchmark]
    public void ManagedArrayRuntimeIndex() =>
        ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.Managed[_runtimeIndex])(_counter);

    [Benchmark]
    public void UnmanagedTableConstantIndex() =>
        ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.Unmanaged[FunctionTableStorage.ReleaseSlot])(_counter);

    [Benchmark]
    public void UnmanagedTableRuntimeIndex() =>
        ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.Unmanaged[_runtimeIndex])(_counter);

    [Benchmark]
    public void HexaStyleIndexerConstantIndex() =>
        ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.HexaStyle![FunctionTableStorage.ReleaseSlot])(_counter);
}
