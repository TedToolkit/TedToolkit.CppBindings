using System;
using System.Runtime.InteropServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

[MemoryDiagnoser]
public unsafe class FunctionTableOwnerBenchmarks
{
    private IntPtr _module;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _instanceRelease;

    [GlobalSetup]
    public void Setup()
    {
        _module = NativeFixture.LoadWithKernel32();
        IntPtr release = NativeFixture.GetExportWithKernel32(_module, NativeFixture.ReleaseExport);
        _instanceRelease = (delegate* unmanaged[Cdecl]<IntPtr, void>)release;
        FunctionTableStorage.Initialize(release);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        FunctionTableStorage.Free();
        NativeFixture.FreeWithKernel32(_module);
    }

    [Benchmark(Baseline = true)]
    public void InstanceFieldOwner()
    {
        var owner = new InstanceFieldOwnerState(DirectImports.CreateOwner(), _instanceRelease);
        owner.Dispose();
        GC.KeepAlive(owner);
    }

    [Benchmark]
    public void StaticTypedFieldOwner()
    {
        var owner = new StaticTypedFieldOwnerState(DirectImports.CreateOwner());
        owner.Dispose();
        GC.KeepAlive(owner);
    }

    [Benchmark]
    public void StaticManagedTableOwner()
    {
        var owner = new StaticManagedTableOwnerState(DirectImports.CreateOwner());
        owner.Dispose();
        GC.KeepAlive(owner);
    }

    [Benchmark]
    public void StaticUnmanagedTableOwner()
    {
        var owner = new StaticUnmanagedTableOwnerState(DirectImports.CreateOwner());
        owner.Dispose();
        GC.KeepAlive(owner);
    }

    [Benchmark]
    public void HexaStyleStaticTableOwner()
    {
        var owner = new HexaStyleStaticTableOwnerState(DirectImports.CreateOwner());
        owner.Dispose();
        GC.KeepAlive(owner);
    }

    [Benchmark]
    public void ModuleSafeTableReferenceOwner()
    {
        var owner = new ModuleSafeTableReferenceOwnerState(
            DirectImports.CreateOwner(),
            FunctionTableStorage.HexaStyle!,
            FunctionTableStorage.ReleaseSlot);
        owner.Dispose();
        GC.KeepAlive(owner);
    }

    private sealed class InstanceFieldOwnerState : IDisposable
    {
        private IntPtr _value;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _release;

        internal InstanceFieldOwnerState(
            IntPtr value,
            delegate* unmanaged[Cdecl]<IntPtr, void> release)
        {
            _value = value;
            _release = release;
        }

        ~InstanceFieldOwnerState() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void Release()
        {
            IntPtr value = Interlocked.Exchange(ref _value, IntPtr.Zero);
            if (value != IntPtr.Zero)
            {
                _release(value);
            }
        }
    }

    private sealed class StaticTypedFieldOwnerState : IDisposable
    {
        private IntPtr _value;

        internal StaticTypedFieldOwnerState(IntPtr value) => _value = value;

        ~StaticTypedFieldOwnerState() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void Release()
        {
            IntPtr value = Interlocked.Exchange(ref _value, IntPtr.Zero);
            if (value != IntPtr.Zero)
            {
                FunctionTableStorage.StaticRelease(value);
            }
        }
    }

    private sealed class StaticManagedTableOwnerState : IDisposable
    {
        private IntPtr _value;

        internal StaticManagedTableOwnerState(IntPtr value) => _value = value;

        ~StaticManagedTableOwnerState() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void Release()
        {
            IntPtr value = Interlocked.Exchange(ref _value, IntPtr.Zero);
            if (value != IntPtr.Zero)
            {
                ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.Managed[FunctionTableStorage.ReleaseSlot])(value);
            }
        }
    }

    private sealed class StaticUnmanagedTableOwnerState : IDisposable
    {
        private IntPtr _value;

        internal StaticUnmanagedTableOwnerState(IntPtr value) => _value = value;

        ~StaticUnmanagedTableOwnerState() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void Release()
        {
            IntPtr value = Interlocked.Exchange(ref _value, IntPtr.Zero);
            if (value != IntPtr.Zero)
            {
                ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.Unmanaged[FunctionTableStorage.ReleaseSlot])(value);
            }
        }
    }

    private sealed class HexaStyleStaticTableOwnerState : IDisposable
    {
        private IntPtr _value;

        internal HexaStyleStaticTableOwnerState(IntPtr value) => _value = value;

        ~HexaStyleStaticTableOwnerState() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void Release()
        {
            IntPtr value = Interlocked.Exchange(ref _value, IntPtr.Zero);
            if (value != IntPtr.Zero)
            {
                ((delegate* unmanaged[Cdecl]<IntPtr, void>)FunctionTableStorage.HexaStyle![FunctionTableStorage.ReleaseSlot])(value);
            }
        }
    }

    private sealed class ModuleSafeTableReferenceOwnerState : IDisposable
    {
        private IntPtr _value;
        private readonly HexaStyleFunctionTable _table;
        private readonly int _releaseSlot;

        internal ModuleSafeTableReferenceOwnerState(
            IntPtr value,
            HexaStyleFunctionTable table,
            int releaseSlot)
        {
            _value = value;
            _table = table;
            _releaseSlot = releaseSlot;
        }

        ~ModuleSafeTableReferenceOwnerState() => Release();

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        private void Release()
        {
            IntPtr value = Interlocked.Exchange(ref _value, IntPtr.Zero);
            if (value != IntPtr.Zero)
            {
                ((delegate* unmanaged[Cdecl]<IntPtr, void>)_table[_releaseSlot])(value);
            }
        }
    }
}
