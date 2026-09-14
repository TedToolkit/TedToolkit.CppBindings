using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

[MemoryDiagnoser]
public unsafe class InvocationBenchmarks
{
    private static readonly ReleaseDelegate CachedPInvoke = DirectImports.ReleaseProbe;

    private IntPtr _counter;
    private IntPtr _kernelModule;
    private ReleaseDelegate? _kernelDelegate;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _kernelFunction;

#if NET8_0_OR_GREATER
    private IntPtr _nativeLibraryModule;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _nativeLibraryFunction;
#endif

    [GlobalSetup]
    public void Setup()
    {
        _counter = Marshal.AllocHGlobal(sizeof(long));
        Marshal.WriteInt64(_counter, 0);

        _kernelModule = NativeFixture.LoadWithKernel32();
        IntPtr kernelExport = NativeFixture.GetExportWithKernel32(
            _kernelModule,
            NativeFixture.ProbeExport);
        _kernelDelegate = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(kernelExport);
        _kernelFunction = (delegate* unmanaged[Cdecl]<IntPtr, void>)kernelExport;

#if NET8_0_OR_GREATER
        _nativeLibraryModule = NativeLibrary.Load(NativeFixture.ModulePath);
        _nativeLibraryFunction = (delegate* unmanaged[Cdecl]<IntPtr, void>)NativeLibrary.GetExport(
            _nativeLibraryModule,
            NativeFixture.ProbeExport);
#endif

        DirectImports.ReleaseProbe(_counter);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
#if NET8_0_OR_GREATER
        NativeLibrary.Free(_nativeLibraryModule);
#endif
        NativeFixture.FreeWithKernel32(_kernelModule);
        Marshal.FreeHGlobal(_counter);
    }

    [Benchmark(Baseline = true)]
    public void DirectPInvoke() => DirectImports.ReleaseProbe(_counter);

    [Benchmark]
    public void CachedDelegateToPInvoke() => CachedPInvoke(_counter);

    [Benchmark]
    public void Kernel32ResolvedDelegate() => _kernelDelegate!(_counter);

    [Benchmark]
    public void Kernel32ResolvedFunctionPointer() => _kernelFunction(_counter);

#if NET8_0_OR_GREATER
    [Benchmark]
    public void NativeLibraryFunctionPointer() => _nativeLibraryFunction(_counter);
#endif
}
