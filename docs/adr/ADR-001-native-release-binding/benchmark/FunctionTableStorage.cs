using System;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

internal static unsafe class FunctionTableStorage
{
    internal const int ReleaseSlot = 7;
    private const int SlotCount = 16;

    internal static readonly IntPtr[] Managed = new IntPtr[SlotCount];
    internal static delegate* unmanaged[Cdecl]<IntPtr, void> StaticRelease;
    internal static void** Unmanaged;
    internal static HexaStyleFunctionTable? HexaStyle;

    internal static void Initialize(IntPtr release)
    {
        StaticRelease = (delegate* unmanaged[Cdecl]<IntPtr, void>)release;
        Managed[ReleaseSlot] = release;

        Unmanaged = (void**)Marshal.AllocHGlobal(SlotCount * IntPtr.Size);
        for (int index = 0; index < SlotCount; index++)
        {
            Unmanaged[index] = null;
        }

        Unmanaged[ReleaseSlot] = (void*)release;
        HexaStyle = new HexaStyleFunctionTable(SlotCount);
        HexaStyle[ReleaseSlot] = (void*)release;
    }

    internal static void Free()
    {
        HexaStyle?.Dispose();
        HexaStyle = null;

        if (Unmanaged != null)
        {
            Marshal.FreeHGlobal((IntPtr)Unmanaged);
            Unmanaged = null;
        }

        Managed[ReleaseSlot] = IntPtr.Zero;
        StaticRelease = null;
    }
}

internal sealed unsafe class HexaStyleFunctionTable : IDisposable
{
    private void** _table;

    internal HexaStyleFunctionTable(int length)
    {
        _table = (void**)Marshal.AllocHGlobal(length * IntPtr.Size);
        for (int index = 0; index < length; index++)
        {
            _table[index] = null;
        }
    }

    internal void* this[int index]
    {
        get => _table[index];
        set => _table[index] = value;
    }

    public void Dispose()
    {
        if (_table != null)
        {
            Marshal.FreeHGlobal((IntPtr)_table);
            _table = null;
        }
    }
}
