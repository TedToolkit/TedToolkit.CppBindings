using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.InteropErrorTests;

/// <summary>
/// <see cref="interop_error.ThrowIfError(TedToolkit.Occt.interop_error)"/>
/// </summary>
internal sealed class ThrowIfErrorTest
{
    [Test]
    public async Task Should_ignore_empty_error_payload_Async()
    {
        interop_error.ThrowIfError(default);

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Should_map_std_bad_alloc_to_out_of_memory_exception_Async()
    {
        using var error = InteropErrorAllocation.Create("std::bad_alloc", "allocation failed");
        var action = () => interop_error.ThrowIfError(error.Value);
        var exception = CaptureException(action);

        await Assert.That(action).Throws<OutOfMemoryException>()
            .WithMessage("allocation failed");
        await Assert.That(exception.Data["NativeTypeName"]).IsEqualTo("std::bad_alloc");
    }

    [Test]
    public async Task Should_map_standard_out_of_range_to_argument_out_of_range_exception_Async()
    {
        using var error = InteropErrorAllocation.Create("Standard_OutOfRange", "index must be 1 or 2");
        var action = () => interop_error.ThrowIfError(error.Value);
        var exception = CaptureException(action);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
        await Assert.That(exception.Message.Contains("index must be 1 or 2", StringComparison.Ordinal)).IsTrue();
        await Assert.That(exception.Data["NativeTypeName"]).IsEqualTo("Standard_OutOfRange");
    }

    [Test]
    public async Task Should_map_standard_failure_to_occt_native_exception_and_keep_native_stack_trace_Async()
    {
        using var error = InteropErrorAllocation.Create("Standard_Failure", "native failure", "native stack");
        var action = () => interop_error.ThrowIfError(error.Value);
        var exception = (OcctNativeException)CaptureException(action);

        await Assert.That(action).Throws<OcctNativeException>();
        await Assert.That(exception.NativeTypeName).IsEqualTo("Standard_Failure");
        await Assert.That(exception.NativeStackTrace).IsEqualTo("native stack");
        await Assert.That(exception.Data["NativeStackTrace"]).IsEqualTo("native stack");
    }

    [Test]
    public async Task Should_fallback_to_occt_native_exception_for_unknown_types_Async()
    {
        using var error = InteropErrorAllocation.Create("CustomNativeError", "unexpected");
        var action = () => interop_error.ThrowIfError(error.Value);
        var exception = (OcctNativeException)CaptureException(action);

        await Assert.That(action).Throws<OcctNativeException>()
            .WithMessage("unexpected");
        await Assert.That(exception.NativeTypeName).IsEqualTo("CustomNativeError");
    }

    private static Exception CaptureException(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the action to throw.");
    }

    private sealed class InteropErrorAllocation : IDisposable
    {
        private readonly nint _typeName;
        private readonly nint _message;
        private readonly nint _stackTrace;

        private InteropErrorAllocation(nint typeName, nint message, nint stackTrace)
        {
            _typeName = typeName;
            _message = message;
            _stackTrace = stackTrace;
            Value = CreateError(typeName, message, stackTrace);
        }

        public interop_error Value { get; }

        public static InteropErrorAllocation Create(string? typeName, string? message, string? stackTrace = null)
        {
            return new InteropErrorAllocation(
                StringToUtf8(typeName),
                StringToUtf8(message),
                StringToUtf8(stackTrace));
        }

        public void Dispose()
        {
            Free(_typeName);
            Free(_message);
            Free(_stackTrace);
        }

        private static nint StringToUtf8(string? value)
        {
            return value is null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(value);
        }

        private static unsafe interop_error CreateError(nint typeName, nint message, nint stackTrace)
        {
            return new interop_error((byte*)typeName, (byte*)message, (byte*)stackTrace);
        }

        private static void Free(nint pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
    }
}
