// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjectionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using TedToolkit.CppBindings;

namespace TedToolkit.CppBindings.Manifold.Runtime.Tests;

/// <summary>Verifies Manifold diagnostic ownership and public exception mapping.</summary>
[NotInParallel("manifold-runtime-native-error")]
internal sealed class NativeErrorProjectionTests
{
    private static int clearCount;

    /// <summary>Verifies every fixed failure category and exact-once native cleanup.</summary>
    /// <returns>A task that completes when all categories are checked.</returns>
    [Test]
    public async Task Should_map_every_failure_category_and_clear_once_Async()
    {
        var cases = new[]
        {
            (Kind: 1, ExceptionType: typeof(NativeArgumentException)),
            (Kind: 2, ExceptionType: typeof(NativeArgumentOutOfRangeException)),
            (Kind: 6, ExceptionType: typeof(NativeOutOfMemoryException)),
            (Kind: 7, ExceptionType: typeof(NativeOverflowException)),
            (Kind: 8, ExceptionType: typeof(NativeStandardException)),
            (Kind: 255, ExceptionType: typeof(NativeUnknownException)),
        };

        foreach (var testCase in cases)
        {
            clearCount = 0;
            var error = CreateError(testCase.Kind, "native.type", "native message", "native stack");
            var exception = Project(ref error);
            var nativeException = (INativeException)exception;

            await Assert.That(exception.GetType()).IsEqualTo(testCase.ExceptionType);
            await Assert.That(exception.Message).IsEqualTo("native message");
            await Assert.That(nativeException.NativeTypeName).IsEqualTo("native.type");
            await Assert.That(nativeException.NativeStackTrace).IsEqualTo("native stack");
            await Assert.That(clearCount).IsEqualTo(1);
            await Assert.That(error.Kind).IsEqualTo(0);
            await Assert.That(error.TypeName).IsEqualTo(0);
            await Assert.That(error.Message).IsEqualTo(0);
            await Assert.That(error.StackTrace).IsEqualTo(0);
        }
    }

    /// <summary>Verifies malformed UTF-8 still clears the native carrier exactly once.</summary>
    /// <returns>A task that completes when fallback behavior is checked.</returns>
    [Test]
    public async Task Should_clear_once_when_utf8_conversion_fails_Async()
    {
        clearCount = 0;
        var error = CreateError(1, "native.type", null, "native stack");
        error.Message = AllocateBytes([0xff, 0x00]);

        var exception = Project(ref error);

        await Assert.That(exception).IsTypeOf<NativeArgumentException>();
        await Assert.That(exception.Message).IsEqualTo("Native Manifold operation failed with error kind 1.");
        await Assert.That(clearCount).IsEqualTo(1);
        await Assert.That(error.Kind).IsEqualTo(0);
    }

    /// <summary>Verifies a successful carrier is not cleared.</summary>
    /// <returns>A task that completes when success behavior is checked.</returns>
    [Test]
    public async Task Should_not_clear_a_success_carrier_Async()
    {
        clearCount = 0;
        NativeError error = default;

        unsafe
        {
            NativeErrorProjection.ThrowIfFailed(ref error, &ClearError);
        }

        await Assert.That(clearCount).IsEqualTo(0);
    }

    /// <summary>Verifies the provider Runtime has no dependency on another geometry provider.</summary>
    /// <returns>A task that completes when references are checked.</returns>
    [Test]
    public async Task Should_have_no_other_provider_dependency_Async()
    {
        var references = typeof(NativeErrorProjection).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .Where(static name => name is not null)
            .ToArray();

        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Occt.Runtime");
        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Cgal.Runtime");
        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Fcl.Runtime");
    }

    private static unsafe Exception Project(ref NativeError error)
    {
        try
        {
            NativeErrorProjection.ThrowIfFailed(ref error, &ClearError);
            throw new InvalidOperationException("Projection returned for a failed native carrier.");
        }
        catch (Exception exception) when (exception is INativeException)
        {
            return exception;
        }
    }

    private static NativeError CreateError(int kind, string? type, string? message, string? stack)
    {
        return new NativeError
        {
            Kind = kind,
            TypeName = AllocateText(type),
            Message = AllocateText(message),
            StackTrace = AllocateText(stack),
        };
    }

    private static unsafe nint AllocateText(string? value)
    {
        return value is null ? 0 : AllocateBytes([.. Encoding.UTF8.GetBytes(value), 0]);
    }

    private static unsafe nint AllocateBytes(ReadOnlySpan<byte> bytes)
    {
        var result = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
        bytes.CopyTo(new Span<byte>(result, bytes.Length));
        return (nint)result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void ClearError(NativeError* error)
    {
        Interlocked.Increment(ref clearCount);
        NativeMemory.Free((void*)error->TypeName);
        NativeMemory.Free((void*)error->Message);
        NativeMemory.Free((void*)error->StackTrace);
        *error = default;
    }
}