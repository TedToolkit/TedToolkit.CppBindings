// -----------------------------------------------------------------------
// <copyright file="NativeResultProjectionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Runtime.Tests;

/// <summary>
/// Verifies the generated result projection against real CGAL optional, variant, and Object containers.
/// </summary>
[NotInParallel("cgal-runtime-native")]
internal sealed class NativeResultProjectionTests
{
    /// <summary>
    /// Verifies every optional/variant partition through the generated managed result type.
    /// </summary>
    /// <returns>A task that completes when every partition has been asserted.</returns>
    [Test]
    public Task Should_project_real_optional_variant_partitions_Async()
    {
        return VerifyPartitionsAsync(NativeFixture.CreateVariantResult);
    }

    /// <summary>
    /// Verifies every CGAL Object partition through the generated managed result type.
    /// </summary>
    /// <returns>A task that completes when every partition has been asserted.</returns>
    [Test]
    public Task Should_project_real_cgal_object_partitions_Async()
    {
        return VerifyPartitionsAsync(NativeFixture.CreateObjectResult);
    }

    private static async Task VerifyPartitionsAsync(
        Func<int, Segment_2_Intersection_Transport> createResult)
    {
        var cases = new[]
        {
            new { Scenario = 0, Kind = Segment_2IntersectionKind.None, TransferCount = 0, },
            new { Scenario = 1, Kind = Segment_2IntersectionKind.Point, TransferCount = 1, },
            new { Scenario = 2, Kind = Segment_2IntersectionKind.Segment, TransferCount = 1, },
        };

        foreach (var testCase in cases)
        {
            NativeFixture.ResetCounters();
            var result = Segment_2Intersection.FromNative(createResult(testCase.Scenario));

            await Assert.That(result.Kind).IsEqualTo(testCase.Kind);
            await Assert.That(NativeFixture.ContainerCreateCount).IsEqualTo(1);
            await Assert.That(NativeFixture.ContainerDestroyCount).IsEqualTo(1);
            await Assert.That(NativeFixture.AlternativeTransferCount).IsEqualTo(testCase.TransferCount);

            if (testCase.Kind == Segment_2IntersectionKind.Point)
            {
                await Assert.That(result.TryGetPoint(out var point)).IsTrue();
                await Assert.That(point.X).IsEqualTo(1.0);
                await Assert.That(point.Y).IsEqualTo(2.0);
            }

            if (testCase.Kind == Segment_2IntersectionKind.Segment)
            {
                await Assert.That(result.TryGetSegment(out var segment)).IsTrue();
                await Assert.That(segment.Source.X).IsEqualTo(1.0);
                await Assert.That(segment.Source.Y).IsEqualTo(2.0);
                await Assert.That(segment.Target.X).IsEqualTo(3.0);
                await Assert.That(segment.Target.Y).IsEqualTo(4.0);
            }
        }

        foreach (var scenario in new[] { 3, 4, })
        {
            NativeFixture.ResetCounters();
            var transport = createResult(scenario);
            CgalUnknownResultException? caught = null;
            try
            {
                _ = Segment_2Intersection.FromNative(transport);
            }
            catch (CgalUnknownResultException exception)
            {
                caught = exception;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught).IsAssignableTo<CgalException>();
            await Assert.That(caught!.Message).Contains(
                transport.Tag.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Assert.That(NativeFixture.ContainerCreateCount).IsEqualTo(1);
            await Assert.That(NativeFixture.ContainerDestroyCount).IsEqualTo(1);
            await Assert.That(NativeFixture.AlternativeTransferCount).IsEqualTo(0);
        }
    }
}