using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt;

/// <summary>
/// Mirrors the native <c>interop_error</c> layout defined in <c>csharp_interop.h</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
#pragma warning disable CA1815, SA1300
public readonly unsafe struct interop_error
#pragma warning restore SA1300, CA1815
{
    /// <summary>
    /// Native exception type name encoded as UTF-8.
    /// </summary>
#pragma warning disable IDE1006
    private readonly byte* type_name;

    /// <summary>
    /// Native exception message encoded as UTF-8.
    /// </summary>
    private readonly byte* message;

    /// <summary>
    /// Native exception stack trace encoded as UTF-8.
    /// </summary>
    private readonly byte* stack_trace;
#pragma warning restore IDE1006

    /// <summary>
    /// Converts the current native error payload into a managed exception.
    /// </summary>
    /// <returns>A managed exception matching the native failure.</returns>
    private Exception ToException()
    {
        var typeName = PtrToString(type_name);
        var errorMessage = PtrToString(message);
        var nativeStackTrace = PtrToString(stack_trace);

        return CreateException(typeName, errorMessage, nativeStackTrace);

        static string? PtrToString(byte* value)
        {
            if (value is null)
            {
                return null;
            }

#if NET6_0_OR_GREATER
            return Marshal.PtrToStringUTF8((nint)value);
#else
            var length = 0;
            while (Marshal.ReadByte((nint)value, length) != 0)
            {
                length++;
            }

            var buffer = new byte[length];
            Marshal.Copy((nint)value, buffer, 0, length);

            return System.Text.Encoding.UTF8.GetString(buffer);
#endif
        }
    }

    /// <summary>
    /// Throws a mapped managed exception when the native call reported an error.
    /// </summary>
    /// <param name="error">The native error payload.</param>
    public void ThrowIfError()
    {
        if (type_name is null)
        {
            return;
        }

        var exception = ToException();
        Clear();
        throw exception;
    }

    private void Clear()
    {
        Free(this);

        [DllImport("ted_toolkit_occt", CallingConvention = CallingConvention.Cdecl, EntryPoint = "free_error")]
        static extern void Free(interop_error error);
    }

    private static Exception CreateException(string? typeName, string? errorMessage, string? nativeStackTrace)
    {
        var normalizedTypeName = typeName?.Trim();
        var normalizedMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "The native OCCT layer reported an error."
            : errorMessage;

        Exception exception = normalizedTypeName switch
        {
            null or "" => new OcctNativeException(null, normalizedMessage, nativeStackTrace),
            _ when Contains(normalizedTypeName, "bad_alloc") || Contains(normalizedTypeName, "OutOfMemory")
#pragma warning disable CA2201
                => new OutOfMemoryException(normalizedMessage),
#pragma warning restore CA2201
            _ when Contains(normalizedTypeName, "invalid_argument")
                => new ArgumentException(normalizedMessage),
            _ when Contains(normalizedTypeName, "out_of_range") || Contains(normalizedTypeName, "OutOfRange")
                => new ArgumentOutOfRangeException(paramName: null, message: normalizedMessage),
            _ when Contains(normalizedTypeName, "domain_error")
                   || Contains(normalizedTypeName, "DomainError")
                   || Contains(normalizedTypeName, "ConstructionError")
                   || Contains(normalizedTypeName, "DimensionError")
                => new ArgumentException(normalizedMessage),
            _ when Contains(normalizedTypeName, "overflow_error") || Contains(normalizedTypeName, "Overflow")
                => new OverflowException(normalizedMessage),
            _ when Contains(normalizedTypeName, "underflow_error") || Contains(normalizedTypeName, "Underflow")
                => new ArithmeticException(normalizedMessage),
            _ when Contains(normalizedTypeName, "logic_error")
                   || Contains(normalizedTypeName, "ProgramError")
                   || Contains(normalizedTypeName, "NoSuchObject")
                => new InvalidOperationException(normalizedMessage),
            _ when Contains(normalizedTypeName, "NullObject") || Contains(normalizedTypeName, "NullValue")
#pragma warning disable CA2201
                => new NullReferenceException(normalizedMessage),
#pragma warning restore CA2201
            _ => new OcctNativeException(normalizedTypeName, normalizedMessage, nativeStackTrace),
        };

        Annotate(exception, normalizedTypeName, nativeStackTrace);
        return exception;
    }

    private static bool Contains(string source, string value)
    {
#if NET6_0_OR_GREATER
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
#else
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
#endif
    }

    private static void Annotate(Exception exception, string? nativeTypeName, string? nativeStackTrace)
    {
        if (!string.IsNullOrWhiteSpace(nativeTypeName))
        {
            exception.Data[nameof(OcctNativeException.NativeTypeName)] = nativeTypeName;
        }

        if (!string.IsNullOrWhiteSpace(nativeStackTrace))
        {
            exception.Data[nameof(OcctNativeException.NativeStackTrace)] = nativeStackTrace;
        }
    }
}