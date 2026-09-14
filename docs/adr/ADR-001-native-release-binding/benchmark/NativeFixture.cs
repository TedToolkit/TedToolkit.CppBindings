using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

internal static class NativeFixture
{
    internal const string LibraryName = "ted_occt_release_benchmark";
    internal const string ProbeExport = "release_probe";
    internal const string CreateExport = "create_owner";
    internal const string ReleaseExport = "release_owner";

    internal static string ModulePath =>
        Environment.GetEnvironmentVariable("TED_OCCT_BENCH_NATIVE")
        ?? throw new InvalidOperationException(
            "TED_OCCT_BENCH_NATIVE must contain the absolute path to the benchmark DLL.");

    internal static void ValidateConfiguration()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("This benchmark compares Windows loading paths.");
        }

        string modulePath = ModulePath;
        if (!System.IO.Path.IsPathRooted(modulePath) || !System.IO.File.Exists(modulePath))
        {
            throw new InvalidOperationException(
                $"TED_OCCT_BENCH_NATIVE does not identify an existing absolute DLL path: {modulePath}");
        }
    }

    internal static IntPtr LoadWithKernel32()
    {
        IntPtr module = Kernel32.LoadLibraryW(ModulePath);
        if (module == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return module;
    }

    internal static IntPtr GetExportWithKernel32(IntPtr module, string name)
    {
        IntPtr address = Kernel32.GetProcAddress(module, name);
        if (address == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return address;
    }

    internal static void FreeWithKernel32(IntPtr module)
    {
        if (!Kernel32.FreeLibrary(module))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}

internal static class Kernel32
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr LoadLibraryW(string fileName);
}

internal static class DirectImports
{
    [DllImport(NativeFixture.LibraryName, EntryPoint = NativeFixture.ProbeExport,
        CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void ReleaseProbe(IntPtr counter);

    [DllImport(NativeFixture.LibraryName, EntryPoint = NativeFixture.CreateExport,
        CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr CreateOwner();

    [DllImport(NativeFixture.LibraryName, EntryPoint = NativeFixture.ReleaseExport,
        CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void ReleaseOwner(IntPtr owner);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void ReleaseDelegate(IntPtr value);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr CreateDelegate();
