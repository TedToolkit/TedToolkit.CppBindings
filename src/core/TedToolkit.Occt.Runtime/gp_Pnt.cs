using System.Runtime.InteropServices;

namespace TedToolkit.Occt.Console;

public readonly unsafe struct gp_Pnt2d : IDisposable
{
    /// <summary>
    ///  Hello.
    /// </summary>
    /// <inheritdoc cref="interop_error.ThrowIfError"/>
    public void SetCoord(int theIndex, double theXi)
    {
        fixed (gp_Pnt2d* selfPtr = &this)
        {
            SetCoordNative(selfPtr, theIndex, theXi);
        }
    }

    [DllImport("Name", CallingConvention = CallingConvention.Cdecl, EntryPoint = "gp_Pnt2d_SetCoord_int_double")]
    private static extern void SetCoordNative(gp_Pnt2d* self, int theIndex, double theXi);

    [DllImport("Name", CallingConvention = CallingConvention.Cdecl, EntryPoint = "gp_Pnt2d_Delete")]
    private static extern void DeleteNative(gp_Pnt2d* self);

    public void Dispose()
    {
        fixed (gp_Pnt2d* selfPtr = &this)
        {
            DeleteNative(selfPtr);
        }
    }
}
