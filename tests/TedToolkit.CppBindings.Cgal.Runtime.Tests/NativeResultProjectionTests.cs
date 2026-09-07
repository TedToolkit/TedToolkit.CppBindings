// -----------------------------------------------------------------------
// <copyright file="NativeResultProjectionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Runtime.Tests;

/// <summary>
/// Verifies generated operation-specific result semantics against a compiled C++ container.
/// </summary>
[NotInParallel("cgal-runtime-native")]
internal sealed class NativeResultProjectionTests
{
    /// <summary>
    /// Verifies empty and every declared alternative while the native container is destroyed once.
    /// </summary>
    /// <returns>A task that completes when all declared partitions finish.</returns>
    [Test]
    public async Task Should_project_all_declared_partitions_and_destroy_once_Async()
    {
        var cases = new[]
        {
            new { Scenario = 0, Kind = IntersectionKind.None, TransferCount = 0, },
            new { Scenario = 1, Kind = IntersectionKind.Point, TransferCount = 1, },
            new { Scenario = 2, Kind = IntersectionKind.Segment, TransferCount = 1, },
        };

        foreach (var testCase in cases)
        {
            NativeFixture.ResetCounters();
            var result = IntersectionResult.FromNative(NativeFixture.CreateResult(testCase.Scenario));

            await Assert.That(result.Kind).IsEqualTo(testCase.Kind);
            await Assert.That(NativeFixture.ContainerCreateCount).IsEqualTo(1);
            await Assert.That(NativeFixture.ContainerDestroyCount).IsEqualTo(1);
            await Assert.That(NativeFixture.AlternativeTransferCount).IsEqualTo(testCase.TransferCount);

            if (testCase.Kind == IntersectionKind.Point)
            {
                await Assert.That(result.TryGetPoint(out var point)).IsTrue();
                await Assert.That(point).IsEqualTo(new Point(1.0, 2.0));
            }

            if (testCase.Kind == IntersectionKind.Segment)
            {
                await Assert.That(result.TryGetSegment(out var segment)).IsTrue();
                await Assert.That(segment).IsEqualTo(new Segment(new(1.0, 2.0), new(3.0, 4.0)));
            }
        }
    }

    /// <summary>
    /// Verifies undeclared alternatives and invalid tags fail closed after native destruction.
    /// </summary>
    /// <returns>A task that completes when both unknown partitions finish.</returns>
    [Test]
    public async Task Should_reject_unknown_tags_after_destroying_the_native_container_once_Async()
    {
        foreach (var scenario in new[] { 3, 4, })
        {
            NativeFixture.ResetCounters();
            var transport = NativeFixture.CreateResult(scenario);
            CgalUnknownResultException? caught = null;
            try
            {
                _ = IntersectionResult.FromNative(transport);
            }
            catch (CgalUnknownResultException exception)
            {
                caught = exception;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught).IsAssignableTo<CgalException>();
            await Assert.That(caught!.Message).Contains(transport.Tag.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Assert.That(NativeFixture.ContainerCreateCount).IsEqualTo(1);
            await Assert.That(NativeFixture.ContainerDestroyCount).IsEqualTo(1);
            await Assert.That(NativeFixture.AlternativeTransferCount).IsEqualTo(0);
        }
    }

    private enum IntersectionKind
    {
        None = 0,

        Point = 1,

        Segment = 2,
    }

    private readonly record struct Point(double X, double Y);

    private readonly record struct Segment(Point Source, Point Target);

    private readonly struct IntersectionResult
    {
        private readonly Point point;

        private readonly Segment segment;

        private IntersectionResult(IntersectionKind kind, in Point point, in Segment segment)
        {
            Kind = kind;
            this.point = point;
            this.segment = segment;
        }

        internal IntersectionKind Kind { get; }

        internal static IntersectionResult FromNative(NativeResult value)
        {
            return value.Tag switch
            {
                0 => new(IntersectionKind.None, default, default),
                1 => new(IntersectionKind.Point, new(value.AX, value.AY), default),
                2 => new(
                    IntersectionKind.Segment,
                    default,
                    new(new(value.AX, value.AY), new(value.BX, value.BY))),
                _ => throw new CgalUnknownResultException(
                    $"Native CGAL intersection returned undeclared alternative tag {value.Tag}."),
            };
        }

        internal bool TryGetPoint(out Point value)
        {
            value = point;
            return Kind == IntersectionKind.Point;
        }

        internal bool TryGetSegment(out Segment value)
        {
            value = segment;
            return Kind == IntersectionKind.Segment;
        }
    }
}