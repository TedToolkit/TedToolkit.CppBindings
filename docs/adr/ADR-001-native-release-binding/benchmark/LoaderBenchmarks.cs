using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

[MemoryDiagnoser]
public class LoaderBenchmarks
{
    [Benchmark(Baseline = true)]
    public void Kernel32LoadResolveFree()
    {
        IntPtr module = NativeFixture.LoadWithKernel32();
        _ = NativeFixture.GetExportWithKernel32(module, NativeFixture.ReleaseExport);
        NativeFixture.FreeWithKernel32(module);
    }

#if NET8_0_OR_GREATER
    [Benchmark]
    public void NativeLibraryLoadResolveFree()
    {
        IntPtr module = NativeLibrary.Load(NativeFixture.ModulePath);
        _ = NativeLibrary.GetExport(module, NativeFixture.ReleaseExport);
        NativeLibrary.Free(module);
    }
#endif
}
