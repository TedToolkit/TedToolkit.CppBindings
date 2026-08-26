// -----------------------------------------------------------------------
// <copyright file="ThrowIfFailedTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.NativeErrorProjectionTests;

/// <summary>
/// Verifies projection of native error carriers into managed exceptions.
/// </summary>
internal sealed class ThrowIfFailedTests
{
    /// <summary>
    /// Verifies that every defined nonzero kind maps to the approved managed exception type.
    /// </summary>
    /// <returns>A task that completes when the mapping assertions finish.</returns>
    [Test]
    public async Task Should_map_every_defined_nonzero_kind_to_the_approved_exception_Async()
    {
        var cases = new (OcctErrorKind Kind, Type ExceptionType)[]
        {
            (OcctErrorKind.Argument, typeof(OcctArgumentException)),
            (OcctErrorKind.ArgumentOutOfRange, typeof(OcctArgumentOutOfRangeException)),
            (OcctErrorKind.Arithmetic, typeof(OcctArithmeticException)),
            (OcctErrorKind.InvalidOperation, typeof(OcctInvalidOperationException)),
            (OcctErrorKind.NullObject, typeof(OcctNullObjectException)),
            (OcctErrorKind.OutOfMemory, typeof(OcctOutOfMemoryException)),
            (OcctErrorKind.Overflow, typeof(OcctOverflowException)),
            (OcctErrorKind.OcctFailure, typeof(OcctException)),
            (OcctErrorKind.StandardException, typeof(OcctException)),
            (OcctErrorKind.Unknown, typeof(OcctException)),
        };

        foreach (var testCase in cases)
        {
            var exception = Project(new((int)testCase.Kind, 0, 0, 0));

            await Assert.That(exception.GetType()).IsEqualTo(testCase.ExceptionType);
            await Assert.That(exception).IsAssignableTo<IOcctException>();
            await Assert.That(((IOcctException)exception).ErrorKind).IsEqualTo(testCase.Kind);
            await Assert.That(exception.InnerException).IsNull();
        }
    }

    /// <summary>
    /// Verifies that a reserved value and absent diagnostics retain the failure identity and fallback contract.
    /// </summary>
    /// <returns>A task that completes when the fallback assertions finish.</returns>
    [Test]
    public async Task Should_preserve_a_reserved_kind_when_diagnostics_are_absent_Async()
    {
        const int reservedKind = 42;

        var exception = Project(new(reservedKind, 0, 0, 0));
        var occtException = (IOcctException)exception;

        await Assert.That(exception.GetType()).IsEqualTo(typeof(OcctException));
        await Assert.That((int)occtException.ErrorKind).IsEqualTo(reservedKind);
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
        var error = new NativeError(
            (int)OcctErrorKind.StandardException,
            0,
            Marshal.StringToCoTaskMemUTF8(""),
            0);

        var exception = Project(error, Free);

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
        var argument = (ArgumentException)Project(new((int)OcctErrorKind.Argument, 0, 0, 0));
        var range = (ArgumentOutOfRangeException)Project(
            new((int)OcctErrorKind.ArgumentOutOfRange, 0, 0, 0));

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
        var error = new NativeError(
            (int)OcctErrorKind.OcctFailure,
            Marshal.StringToCoTaskMemUTF8("Standard_Failure"),
            Marshal.StringToCoTaskMemUTF8("Native failure"),
            Marshal.StringToCoTaskMemUTF8("native-frame-1"));
        var clearCount = 0;

        void Clear(ref NativeError value)
        {
            clearCount++;
            Free(ref value);
        }

        OcctException? exception = null;
        try
        {
            NativeErrorProjection.ThrowIfFailed(ref error, Clear);
        }
        catch (OcctException caught)
        {
            exception = caught;
        }
        finally
        {
            Free(ref error);
        }

        await Assert.That(clearCount).IsEqualTo(1);
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
        var error = new NativeError(0, 0, 0, 0);
        var clearCount = 0;

        NativeErrorProjection.ThrowIfFailed(ref error, (ref NativeError _) => clearCount++);

        await Assert.That(clearCount).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that the private managed carrier matches the native sequential field layout.
    /// </summary>
    /// <returns>A task that completes when the layout assertions finish.</returns>
    [Test]
    public async Task Should_match_the_native_error_carrier_layout_Async()
    {
        var pointerOffset = IntPtr.Size;

        await Assert.That(Marshal.OffsetOf<NativeError>("kind").ToInt32()).IsEqualTo(0);
        await Assert.That(Marshal.OffsetOf<NativeError>("typeName").ToInt32()).IsEqualTo(pointerOffset);
        await Assert.That(Marshal.OffsetOf<NativeError>("message").ToInt32()).IsEqualTo(pointerOffset * 2);
        await Assert.That(Marshal.OffsetOf<NativeError>("stackTrace").ToInt32()).IsEqualTo(pointerOffset * 3);
        await Assert.That(Marshal.SizeOf<NativeError>()).IsEqualTo(pointerOffset * 4);
    }

    private static Exception Project(NativeError error)
    {
        return Project(error, static (ref NativeError value) => value.Clear());
    }

    private static Exception Project(NativeError error, NativeErrorClear clear)
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

    private static void Free(ref NativeError error)
    {
        if (error.TypeName != 0)
        {
            Marshal.FreeCoTaskMem(error.TypeName);
        }

        if (error.Message != 0)
        {
            Marshal.FreeCoTaskMem(error.Message);
        }

        if (error.StackTrace != 0)
        {
            Marshal.FreeCoTaskMem(error.StackTrace);
        }

        error.Clear();
    }
}