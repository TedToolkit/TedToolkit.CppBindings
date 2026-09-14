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
        var cases = new (
            int Scenario,
            Type ExceptionType,
            string? NativeType,
            string MessageFragment,
            bool HasNativeStack)[]
        {
            (1, typeof(NativeArgumentException), "std::invalid_argument", "fixture invalid argument", false),
            (2, typeof(NativeArgumentOutOfRangeException), "std::out_of_range", "fixture out of range", false),
            (3, typeof(NativeOutOfMemoryException), "std::bad_alloc", "fixture bad allocation", false),
            (4, typeof(NativeOverflowException), "std::overflow_error", "fixture overflow", false),
            (5, typeof(NativeArithmeticException), "std::underflow_error", "fixture underflow", false),
            (6, typeof(NativeStandardException), "std::exception", "fixture standard exception", false),
            (7, typeof(NativeUnknownException), null, "error kind 255", false),
            (10, typeof(CgalErrorException), "CGAL::Error_exception", "fixture error", true),
            (11, typeof(CgalPreconditionException), "CGAL::Precondition_exception", "fixture precondition", true),
            (12, typeof(CgalPostconditionException), "CGAL::Postcondition_exception", "fixture postcondition", true),
            (13, typeof(CgalAssertionException), "CGAL::Assertion_exception", "fixture assertion", true),
            (14, typeof(CgalTestException), "CGAL::Test_exception", "fixture test", true),
            (15, typeof(CgalWarningException), "CGAL::Warning_exception", "fixture warning", true),
            (16, typeof(CgalFailureException), "CGAL::Failure_exception", "fixture failure", true),
        };

        foreach (var testCase in cases)
        {
            NativeFixture.ResetCounters();
            var exception = Project(NativeFixture.InvokeFailure(testCase.Scenario));

            await Assert.That(exception.GetType()).IsEqualTo(testCase.ExceptionType);
            await Assert.That(exception).IsAssignableTo<INativeException>();
            await Assert.That(exception.Message).Contains(testCase.MessageFragment);
            var nativeException = (INativeException)exception;
            await Assert.That(nativeException.NativeTypeName).IsEqualTo(testCase.NativeType);
            if (testCase.HasNativeStack)
            {
                await Assert.That(nativeException.NativeStackTrace).IsNotNull();
                await Assert.That(nativeException.NativeStackTrace!).Contains("CgalRuntimeFixture.cpp");
            }
            else
            {
                await Assert.That(nativeException.NativeStackTrace).IsNull();
            }

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
        var exception = Project(NativeFixture.CreateMalformedError());
        var nativeException = (INativeException)exception;

        await Assert.That(exception).IsTypeOf<CgalPreconditionException>();
        await Assert.That(exception.Message).IsEqualTo("Native CGAL operation failed with error kind 11.");
        await Assert.That(nativeException.NativeTypeName).IsEqualTo("CGAL::Precondition_exception");
        await Assert.That(nativeException.NativeStackTrace).IsEqualTo("fixture.cpp:42");
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

    private static unsafe Exception Project(NativeError error)
    {
        try
        {
            NativeErrorProjection.ThrowIfFailed(ref error, NativeFixture.GetClearError());
            throw new InvalidOperationException("Projection returned for a nonzero native error.");
        }
        catch (Exception exception) when (exception is INativeException)
        {
            return exception;
        }
    }

    private static unsafe void ThrowIfFailed(ref NativeError error)
    {
        NativeErrorProjection.ThrowIfFailed(ref error, NativeFixture.GetClearError());
    }
}