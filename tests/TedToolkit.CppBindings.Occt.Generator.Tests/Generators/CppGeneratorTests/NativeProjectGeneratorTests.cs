// -----------------------------------------------------------------------
// <copyright file="NativeProjectGeneratorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Generators;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CppGeneratorTests;

/// <summary>
/// Verifies the generated native build topology.
/// </summary>
internal sealed class NativeProjectGeneratorTests
{
    /// <summary>
    /// Verifies that native compilation is serialized while sources are batched into bounded unity units.
    /// </summary>
    /// <returns>A task that completes when the generated CMake assertions finish.</returns>
    [Test]
    public async Task Should_generate_single_worker_bounded_unity_build_Async()
    {
        var sources = Enumerable.Range(0, 33).Select(static index => $"Flat_{index:D2}.cpp").ToList();
        sources.Add("Units.cpp");
        sources.Add("Units_Dimensions.cpp");
        var project = NativeProjectGenerator.Generate(sources, "fixture");

        await Assert.That(project).Contains("PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/MP1>");
        await Assert.That(project).Contains("UNITY_BUILD ON");
        await Assert.That(project).Contains("UNITY_BUILD_MODE GROUP");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Flat_depth_1_batch_1\"");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Units_depth_0_batch_0\"");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Units_depth_1_batch_0\"");
        await Assert.That(project).DoesNotContain("/MP8");
        await Assert.That(project.IndexOf("Units.cpp", StringComparison.Ordinal))
            .IsLessThan(project.IndexOf("Units_Dimensions.cpp", StringComparison.Ordinal));
    }
}