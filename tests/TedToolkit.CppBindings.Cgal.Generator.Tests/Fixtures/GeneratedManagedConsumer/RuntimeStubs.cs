using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class NativeTypeNameAttribute(string nativeTypeName) : Attribute
    {
        public string NativeTypeName { get; } = nativeTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeError
    {
        public int Kind;

        public nint TypeName;

        public nint Message;

        public nint StackTrace;
    }

    namespace Cgal
    {
        public sealed class CgalUnknownResultException(string message) : Exception(message);

        public static class NativeErrorProjection
        {
            public static unsafe void ThrowIfFailed(
                ref NativeError error,
                delegate* unmanaged[Cdecl]<NativeError*, void> clear)
            {
            }
        }
    }
}
