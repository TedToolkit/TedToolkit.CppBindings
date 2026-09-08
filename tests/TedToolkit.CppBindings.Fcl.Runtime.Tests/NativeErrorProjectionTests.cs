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

namespace TedToolkit.CppBindings.Fcl.Runtime.Tests;

/// <summary>Verifies FCL diagnostic ownership and public exception mapping.</summary>
[NotInParallel("fcl-runtime-native-error")]
internal sealed class NativeErrorProjectionTests
{
    private static int clearCount;

    /// <summary>Verifies every failure category and exact-once cleanup.</summary>
    /// <returns>A task that completes when mapping is checked.</returns>
    [Test]
    public async Task Should_map_every_failure_category_and_clear_once_Async()
    {
        var cases = new[]
        {
            (Kind: 1, Type: typeof(FclArgumentException)),
            (Kind: 2, Type: typeof(FclArgumentOutOfRangeException)),
            (Kind: 6, Type: typeof(FclOutOfMemoryException)),
            (Kind: 9, Type: typeof(FclException)),
            (Kind: 255, Type: typeof(FclUnknownException)),
        };
        foreach (var testCase in cases)
        {
            clearCount = 0;
            var error = CreateError(testCase.Kind, "native.type", "native message", "native stack");
            var exception = Project(ref error);
            await Assert.That(exception.GetType()).IsEqualTo(testCase.Type);
            await Assert.That(exception.Message).IsEqualTo("native message");
            await Assert.That(exception.NativeTypeName).IsEqualTo("native.type");
            await Assert.That(exception.NativeStackTrace).IsEqualTo("native stack");
            await Assert.That(clearCount).IsEqualTo(1);
            await Assert.That(error.Kind).IsEqualTo(0);
        }
    }

    /// <summary>Verifies malformed UTF-8 still clears exactly once.</summary>
    /// <returns>A task that completes when fallback is checked.</returns>
    [Test]
    public async Task Should_clear_once_when_utf8_conversion_fails_Async()
    {
        clearCount = 0;
        var error = CreateError(1, "native.type", null, null);
        error.Message = Allocate([0xff, 0x00]);
        var exception = Project(ref error);
        await Assert.That(exception.Message).IsEqualTo("Native FCL operation failed with error kind 1.");
        await Assert.That(clearCount).IsEqualTo(1);
    }

    /// <summary>Verifies success does not clear an empty carrier.</summary>
    /// <returns>A task that completes when success is checked.</returns>
    [Test]
    public async Task Should_not_clear_success_Async()
    {
        clearCount = 0;
        NativeError error = default;
        unsafe
        {
            NativeErrorProjection.ThrowIfFailed(ref error, &Clear);
        }

        await Assert.That(clearCount).IsEqualTo(0);
    }

    /// <summary>Verifies no other geometry provider is referenced.</summary>
    /// <returns>A task that completes when references are checked.</returns>
    [Test]
    public async Task Should_have_no_other_provider_dependency_Async()
    {
        var references = typeof(FclException).Assembly.GetReferencedAssemblies()
            .Select(static value => value.Name).Where(static value => value is not null).ToArray();
        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Occt.Runtime");
        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Cgal.Runtime");
        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Manifold.Runtime");
    }

    private static unsafe FclException Project(ref NativeError error)
    {
        try
        {
            NativeErrorProjection.ThrowIfFailed(ref error, &Clear);
            throw new InvalidOperationException("Projection returned for a failed carrier.");
        }
        catch (FclException exception)
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

    private static nint AllocateText(string? value)
    {
        return value is null ? 0 : Allocate([.. Encoding.UTF8.GetBytes(value), 0]);
    }

    private static unsafe nint Allocate(ReadOnlySpan<byte> bytes)
    {
        var result = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
        bytes.CopyTo(new Span<byte>(result, bytes.Length));
        return (nint)result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Clear(NativeError* error)
    {
        Interlocked.Increment(ref clearCount);
        NativeMemory.Free((void*)error->TypeName);
        NativeMemory.Free((void*)error->Message);
        NativeMemory.Free((void*)error->StackTrace);
        *error = default;
    }
}
