using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

[MemoryDiagnoser]
public unsafe class FunctionTableInitializationBenchmarks
{
    private IntPtr _module;

    [Params(16, 256, 1536)]
    public int SlotCount { get; set; }

    [GlobalSetup]
    public void Setup() => _module = NativeFixture.LoadWithKernel32();

    [GlobalCleanup]
    public void Cleanup() => NativeFixture.FreeWithKernel32(_module);

    [Benchmark(Baseline = true)]
    public IntPtr ManagedArrayLoadAll()
    {
        var table = new IntPtr[SlotCount];
        for (int index = 0; index < table.Length; index++)
        {
            table[index] = NativeFixture.GetExportWithKernel32(_module, NativeFixture.ProbeExport);
        }

        return table[table.Length - 1];
    }

    [Benchmark]
    public IntPtr UnmanagedTableLoadAll()
    {
        var table = (IntPtr*)Marshal.AllocHGlobal(SlotCount * IntPtr.Size);
        IntPtr result;
        try
        {
            for (int index = 0; index < SlotCount; index++)
            {
                table[index] = NativeFixture.GetExportWithKernel32(_module, NativeFixture.ProbeExport);
            }

            result = table[SlotCount - 1];
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)table);
        }

        return result;
    }
}
