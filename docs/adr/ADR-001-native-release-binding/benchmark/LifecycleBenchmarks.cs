using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

[MemoryDiagnoser]
public unsafe class LifecycleBenchmarks
{
    private static readonly ReleaseDelegate CachedPInvokeRelease = DirectImports.ReleaseOwner;

    private IntPtr _kernelModule;
    private CreateDelegate? _kernelCreate;
    private ReleaseDelegate? _kernelRelease;
    private delegate* unmanaged[Cdecl]<IntPtr> _kernelCreateFunction;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _kernelReleaseFunction;

#if NET8_0_OR_GREATER
    private IntPtr _nativeLibraryModule;
    private delegate* unmanaged[Cdecl]<IntPtr> _nativeCreate;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _nativeRelease;
#endif

    [GlobalSetup]
    public void Setup()
    {
        _kernelModule = NativeFixture.LoadWithKernel32();
        IntPtr createExport = NativeFixture.GetExportWithKernel32(
            _kernelModule,
            NativeFixture.CreateExport);
        IntPtr releaseExport = NativeFixture.GetExportWithKernel32(
            _kernelModule,
            NativeFixture.ReleaseExport);
        _kernelCreate = Marshal.GetDelegateForFunctionPointer<CreateDelegate>(createExport);
        _kernelRelease = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releaseExport);
        _kernelCreateFunction = (delegate* unmanaged[Cdecl]<IntPtr>)createExport;
        _kernelReleaseFunction = (delegate* unmanaged[Cdecl]<IntPtr, void>)releaseExport;

#if NET8_0_OR_GREATER
        _nativeLibraryModule = NativeLibrary.Load(NativeFixture.ModulePath);
        _nativeCreate = (delegate* unmanaged[Cdecl]<IntPtr>)NativeLibrary.GetExport(
            _nativeLibraryModule,
            NativeFixture.CreateExport);
        _nativeRelease = (delegate* unmanaged[Cdecl]<IntPtr, void>)NativeLibrary.GetExport(
            _nativeLibraryModule,
            NativeFixture.ReleaseExport);
#endif

        DirectImports.ReleaseOwner(DirectImports.CreateOwner());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
#if NET8_0_OR_GREATER
        NativeLibrary.Free(_nativeLibraryModule);
#endif
        NativeFixture.FreeWithKernel32(_kernelModule);
    }

    [Benchmark(Baseline = true)]
    public void DirectPInvoke()
    {
        IntPtr owner = DirectImports.CreateOwner();
        DirectImports.ReleaseOwner(owner);
    }

    [Benchmark]
    public void CachedDelegateToPInvoke()
    {
        IntPtr owner = DirectImports.CreateOwner();
        CachedPInvokeRelease(owner);
    }

    [Benchmark]
    public void Kernel32ResolvedDelegates()
    {
        IntPtr owner = _kernelCreate!();
        _kernelRelease!(owner);
    }

    [Benchmark]
    public void Kernel32ResolvedFunctionPointers()
    {
        IntPtr owner = _kernelCreateFunction();
        _kernelReleaseFunction(owner);
    }

#if NET8_0_OR_GREATER
    [Benchmark]
    public void NativeLibraryFunctionPointers()
    {
        IntPtr owner = _nativeCreate();
        _nativeRelease(owner);
    }
#endif
}
