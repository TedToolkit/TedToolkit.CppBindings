// -----------------------------------------------------------------------
// <copyright file="ThrowIfFailedTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.Tests.NativeErrorProjectionTests;

/// <summary>
/// Verifies projection of native error carriers into managed exceptions.
/// </summary>
internal sealed class ThrowIfFailedTests
{
    private static int diagnosticsClearCount;

    private static int successClearCount;

    /// <summary>
    /// Verifies that every defined nonzero kind maps to the approved managed exception type.
    /// </summary>
    /// <returns>A task that completes when the mapping assertions finish.</returns>
    [Test]
    public async Task Should_map_every_defined_nonzero_kind_to_the_approved_exception_Async()
    {
        var cases = new (int Kind, Type ExceptionType)[]
        {
            (1, typeof(OcctArgumentException)),
            (2, typeof(OcctArgumentOutOfRangeException)),
            (3, typeof(OcctArithmeticException)),
            (4, typeof(OcctInvalidOperationException)),
            (5, typeof(OcctNullObjectException)),
            (6, typeof(OcctOutOfMemoryException)),
            (7, typeof(OcctOverflowException)),
            (8, typeof(OcctFailureException)),
            (9, typeof(OcctStandardException)),
            (255, typeof(OcctUnknownException)),
        };

        foreach (var testCase in cases)
        {
            var exception = Project(CreateError(testCase.Kind));

            await Assert.That(exception.GetType()).IsEqualTo(testCase.ExceptionType);
            await Assert.That(exception).IsAssignableTo<IOcctException>();
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    /// <summary>
    /// Verifies that a reserved value and absent diagnostics use the unknown-failure contract.
    /// </summary>
    /// <returns>A task that completes when the fallback assertions finish.</returns>
    [Test]
    public async Task Should_project_a_reserved_kind_as_an_unknown_failure_Async()
    {
        const int reservedKind = 42;

        var exception = Project(CreateError(reservedKind));
        var occtException = (IOcctException)exception;

        await Assert.That(exception.GetType()).IsEqualTo(typeof(OcctUnknownException));
        await Assert.That(exception.Message)
            .IsEqualTo("Native OCCT operation failed with error kind 42.");
        await Assert.That(occtException.NativeTypeName).IsNull();
        await Assert.That(occtException.NativeStackTrace).IsNull();
        await Assert.That(exception.InnerException).IsNull();
    }

    /// <summary>
    /// Verifies that an empty native message uses the same deterministic fallback as an absent message.
    /// </summary>
    /// <returns>A task that completes when the empty-message assertions finish.</returns>
    [Test]
    public async Task Should_use_the_fallback_message_when_the_native_message_is_empty_Async()
    {
        var error = CreateError(9, 0, Marshal.StringToCoTaskMemUTF8(""), 0);

        var exception = ProjectWithFree(error);

        await Assert.That(exception.Message)
            .IsEqualTo("Native OCCT operation failed with error kind 9.");
    }

    /// <summary>
    /// Verifies that native argument projections do not invent managed parameter metadata.
    /// </summary>
    /// <returns>A task that completes when the argument metadata assertions finish.</returns>
    [Test]
    public async Task Should_not_invent_managed_argument_metadata_Async()
    {
        var argument = (ArgumentException)Project(CreateError(1));
        var range = (ArgumentOutOfRangeException)Project(CreateError(2));

        await Assert.That(argument.ParamName).IsNull();
        await Assert.That(range.ParamName).IsNull();
        await Assert.That(range.ActualValue).IsNull();
    }

    /// <summary>
    /// Verifies that UTF-8 diagnostics are copied before native-owned storage is cleared exactly once.
    /// </summary>
    /// <returns>A task that completes when the ownership assertions finish.</returns>
    [Test]
    public async Task Should_copy_diagnostics_before_clearing_the_native_owner_once_Async()
    {
        var error = CreateError(
            8,
            Marshal.StringToCoTaskMemUTF8("Standard_Failure"),
            Marshal.StringToCoTaskMemUTF8("Native failure"),
            Marshal.StringToCoTaskMemUTF8("native-frame-1"));
        Volatile.Write(ref diagnosticsClearCount, 0);

        OcctException? exception = null;
        try
        {
            ThrowWithCountingFree(ref error);
        }
        catch (OcctException caught)
        {
            exception = caught;
        }
        finally
        {
            FreeManaged(ref error);
        }

        await Assert.That(Volatile.Read(ref diagnosticsClearCount)).IsEqualTo(1);
        await Assert.That(error.Kind).IsEqualTo(0);
        await Assert.That(exception!.Message).IsEqualTo("Native failure");
        await Assert.That(exception.NativeTypeName).IsEqualTo("Standard_Failure");
        await Assert.That(exception.NativeStackTrace).IsEqualTo("native-frame-1");
        await Assert.That(exception.StackTrace).IsNotEqualTo(exception.NativeStackTrace);
    }

    /// <summary>
    /// Verifies that a successful carrier returns without invoking native cleanup.
    /// </summary>
    /// <returns>A task that completes when the success-path assertions finish.</returns>
    [Test]
    public async Task Should_return_without_cleanup_when_the_error_kind_is_none_Async()
    {
        var error = CreateError(0);
        Volatile.Write(ref successClearCount, 0);

        ThrowWithCountingClear(ref error);

        await Assert.That(Volatile.Read(ref successClearCount)).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that a failing carrier requires an explicit native cleanup entry point.
    /// </summary>
    /// <returns>A task that completes when the null-entry-point assertions finish.</returns>
    [Test]
    public async Task Should_reject_a_null_clear_entry_point_for_a_failure_Async()
    {
        var error = CreateError(1);
        ArgumentNullException? caught = null;

        try
        {
            ThrowWithNullClear(ref error);
        }
        catch (ArgumentNullException exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ParamName).IsEqualTo("clear");
        await Assert.That(error.Kind).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that the private managed carrier matches the native sequential field layout.
    /// </summary>
    /// <returns>A task that completes when the layout assertions finish.</returns>
    [Test]
    public async Task Should_match_the_native_error_carrier_layout_Async()
    {
        var pointerOffset = IntPtr.Size;

        await Assert.That(Marshal.OffsetOf<NativeError>("Kind").ToInt32()).IsEqualTo(0);
        await Assert.That(Marshal.OffsetOf<NativeError>("TypeName").ToInt32()).IsEqualTo(pointerOffset);
        await Assert.That(Marshal.OffsetOf<NativeError>("Message").ToInt32()).IsEqualTo(pointerOffset * 2);
        await Assert.That(Marshal.OffsetOf<NativeError>("StackTrace").ToInt32()).IsEqualTo(pointerOffset * 3);
        await Assert.That(Marshal.SizeOf<NativeError>()).IsEqualTo(pointerOffset * 4);
    }

    private static unsafe Exception Project(NativeError error)
    {
        return Project(error, &ClearNative);
    }

    private static NativeError CreateError(
        int kind,
        in nint typeName,
        in nint message,
        in nint stackTrace)
    {
        return new()
        {
            Kind = kind,
            TypeName = typeName,
            Message = message,
            StackTrace = stackTrace,
        };
    }

    private static NativeError CreateError(int kind)
    {
        return CreateError(kind, 0, 0, 0);
    }

    private static unsafe Exception ProjectWithFree(NativeError error)
    {
        return Project(error, &FreeNative);
    }

    private static unsafe Exception Project(
        NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        try
        {
            NativeErrorProjection.ThrowIfFailed(ref error, clear);
            return new InvalidOperationException("Projection did not throw for a nonzero error kind.");
        }
        catch (OcctException exception)
        {
            return exception;
        }
        catch (ArgumentException exception)
        {
            return exception;
        }
        catch (ArithmeticException exception)
        {
            return exception;
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
        catch (OutOfMemoryException exception)
        {
            return exception;
        }
    }

    private static unsafe void ThrowWithCountingFree(ref NativeError error)
    {
        NativeErrorProjection.ThrowIfFailed(ref error, &CountingFreeNative);
    }

    private static unsafe void ThrowWithCountingClear(ref NativeError error)
    {
        NativeErrorProjection.ThrowIfFailed(ref error, &CountingClearNative);
    }

    private static unsafe void ThrowWithNullClear(ref NativeError error)
    {
        NativeErrorProjection.ThrowIfFailed(
            ref error,
            (delegate* unmanaged[Cdecl]<NativeError*, void>)0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static unsafe void ClearNative(NativeError* error)
    {
        if ((nint)error == 0)
        {
            return;
        }

        *error = default;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static unsafe void FreeNative(NativeError* error)
    {
        FreeCore(error);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static unsafe void CountingFreeNative(NativeError* error)
    {
        Interlocked.Increment(ref diagnosticsClearCount);
        FreeCore(error);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static unsafe void CountingClearNative(NativeError* error)
    {
        Interlocked.Increment(ref successClearCount);
        if ((nint)error == 0)
        {
            return;
        }

        *error = default;
    }

    private static unsafe void FreeManaged(ref NativeError error)
    {
        fixed (NativeError* errorPointer = &error)
        {
            FreeCore(errorPointer);
        }
    }

    private static unsafe void FreeCore(NativeError* error)
    {
        if ((nint)error == 0)
        {
            return;
        }

        if (error->TypeName != 0)
        {
            Marshal.FreeCoTaskMem(error->TypeName);
        }

        if (error->Message != 0)
        {
            Marshal.FreeCoTaskMem(error->Message);
        }

        if (error->StackTrace != 0)
        {
            Marshal.FreeCoTaskMem(error->StackTrace);
        }

        *error = default;
    }
}