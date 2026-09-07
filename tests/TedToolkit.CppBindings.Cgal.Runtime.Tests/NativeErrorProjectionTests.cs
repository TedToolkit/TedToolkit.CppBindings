// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjectionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings;

namespace TedToolkit.CppBindings.Cgal.Runtime.Tests;

/// <summary>
/// Verifies the public failure taxonomy through the compiled native diagnostic boundary.
/// </summary>
[NotInParallel("cgal-runtime-native")]
internal sealed class NativeErrorProjectionTests
{
    /// <summary>
    /// Verifies every fixed native failure category and exact-once cleanup.
    /// </summary>
    /// <returns>A task that completes when all mapping assertions finish.</returns>
    [Test]
    public async Task Should_map_every_failure_category_and_clear_once_Async()
    {
        var cases = new (int Kind, Type ExceptionType)[]
        {
            (1, typeof(CgalArgumentException)),
            (2, typeof(CgalArgumentOutOfRangeException)),
            (6, typeof(CgalOutOfMemoryException)),
            (7, typeof(CgalArithmeticException)),
            (9, typeof(CgalStandardException)),
            (10, typeof(CgalErrorException)),
            (11, typeof(CgalPreconditionException)),
            (12, typeof(CgalPostconditionException)),
            (13, typeof(CgalAssertionException)),
            (14, typeof(CgalTestException)),
            (15, typeof(CgalWarningException)),
            (16, typeof(CgalFailureException)),
            (255, typeof(CgalUnknownException)),
            (42, typeof(CgalUnknownException)),
        };

        foreach (var testCase in cases)
        {
            NativeFixture.ResetCounters();
            var exception = Project(NativeFixture.CreateError(testCase.Kind));

            await Assert.That(exception.GetType()).IsEqualTo(testCase.ExceptionType);
            await Assert.That(exception).IsAssignableTo<CgalException>();
            await Assert.That(exception.Message).IsEqualTo("fixture failure");
            await Assert.That(exception.NativeTypeName).IsEqualTo("fixture::native_failure");
            await Assert.That(exception.NativeStackTrace).IsEqualTo("fixture.cpp:42");
            await Assert.That(NativeFixture.DiagnosticClearCount).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Verifies malformed UTF-8 falls back without bypassing native cleanup.
    /// </summary>
    /// <returns>A task that completes when fallback and cleanup assertions finish.</returns>
    [Test]
    public async Task Should_clear_once_when_utf8_conversion_fails_Async()
    {
        NativeFixture.ResetCounters();
        var exception = Project(NativeFixture.CreateError(11, malformedUtf8: true));

        await Assert.That(exception).IsTypeOf<CgalPreconditionException>();
        await Assert.That(exception.Message).IsEqualTo("Native CGAL operation failed with error kind 11.");
        await Assert.That(exception.NativeTypeName).IsEqualTo("fixture::native_failure");
        await Assert.That(exception.NativeStackTrace).IsEqualTo("fixture.cpp:42");
        await Assert.That(NativeFixture.DiagnosticClearCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies success neither consumes nor requires a diagnostic owner.
    /// </summary>
    /// <returns>A task that completes when the success assertion finishes.</returns>
    [Test]
    public async Task Should_not_clear_the_success_carrier_Async()
    {
        NativeFixture.ResetCounters();
        NativeError error = default;

        ThrowIfFailed(ref error);

        await Assert.That(NativeFixture.DiagnosticClearCount).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies the provider assembly exposes no OCCT dependency.
    /// </summary>
    /// <returns>A task that completes when assembly references are inspected.</returns>
    [Test]
    public async Task Should_have_no_occt_assembly_dependency_Async()
    {
        var references = typeof(CgalException).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .Where(static name => name is not null)
            .ToArray();

        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Occt.Runtime");
        await Assert.That(references).DoesNotContain("TedToolkit.CppBindings.Occt.Windows");
    }

    private static unsafe CgalException Project(NativeError error)
    {
        try
        {
            NativeErrorProjection.ThrowIfFailed(ref error, NativeFixture.GetClearError());
            throw new InvalidOperationException("Projection returned for a nonzero native error.");
        }
        catch (CgalException exception)
        {
            return exception;
        }
    }

    private static unsafe void ThrowIfFailed(ref NativeError error)
    {
        NativeErrorProjection.ThrowIfFailed(ref error, NativeFixture.GetClearError());
    }
}