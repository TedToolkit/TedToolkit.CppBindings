using System;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt.NativeRelease.NetStandard20Probe;

public static unsafe class CompatibilityProbe
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string fileName);

    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("ted_occt_release_benchmark", EntryPoint = "release_owner",
        CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void ReleaseOwner(IntPtr owner);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReleaseDelegate(IntPtr owner);

    public static void VerifySurface(string path)
    {
        IntPtr module = LoadLibraryW(path);
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("The native module could not be loaded.");
        }

        try
        {
            IntPtr export = GetProcAddress(module, "release_owner");
            if (export == IntPtr.Zero)
            {
                throw new InvalidOperationException("The release export could not be resolved.");
            }

            _ = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(export);
            delegate* unmanaged[Cdecl]<IntPtr, void> release =
                (delegate* unmanaged[Cdecl]<IntPtr, void>)export;
            _ = (ReleaseDelegate)ReleaseOwner;
            _ = release;
        }
        finally
        {
            _ = FreeLibrary(module);
        }
    }
}
