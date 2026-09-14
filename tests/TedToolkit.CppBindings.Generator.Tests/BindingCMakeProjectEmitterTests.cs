// -----------------------------------------------------------------------
// <copyright file="BindingCMakeProjectEmitterTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Generator.Tests;

/// <summary>
/// Verifies Shared native-project construction across provider dependency and source-group shapes.
/// </summary>
internal sealed class BindingCMakeProjectEmitterTests
{
    /// <summary>
    /// Verifies a simple dependency target is deterministic and snapshots caller-owned facts.
    /// </summary>
    /// <returns>A task that completes when the emitted project is checked.</returns>
    [Test]
    public async Task Should_emit_a_simple_dependency_target_from_snapshotted_facts_Async()
    {
        var definitions = new List<string>() { "GEOMETRY_DEBUG", };
        var definition = new BindingCMakeProjectDefinition(
            "GeometryBindings",
            "geometry_bindings",
            20,
            [new("Geometry", "CONFIG REQUIRED"),],
            definitions,
            includeDirectories: ["\"${CMAKE_CURRENT_SOURCE_DIR}\"",],
            linkLibraries: ["Geometry::Geometry",]);
        definitions[0] = "MUTATED";
        var first = BindingCMakeProjectEmitter.Render(definition, ["Z.cpp", "A.cpp",]);
        var second = BindingCMakeProjectEmitter.Render(definition, ["A.cpp", "Z.cpp",]);

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first).Contains("find_package(Geometry CONFIG REQUIRED)");
        await Assert.That(first).Contains("target_compile_definitions(geometry_bindings PRIVATE GEOMETRY_DEBUG)");
        await Assert.That(first).Contains("target_link_libraries(geometry_bindings PRIVATE Geometry::Geometry)");
        await Assert.That(first).DoesNotContain("MUTATED");
        await Assert.That(first.IndexOf("A.cpp", StringComparison.Ordinal))
            .IsLessThan(first.IndexOf("Z.cpp", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies bounded family/depth grouping and variable-shaped dependency facts.
    /// </summary>
    /// <returns>A task that completes when the emitted unity groups are checked.</returns>
    [Test]
    public async Task Should_emit_bounded_family_and_depth_source_groups_Async()
    {
        var sources = Enumerable.Range(0, 33).Select(static index => $"Flat_{index:D2}.cpp").ToList();
        sources.Add("Units.cpp");
        sources.Add("Units_Dimensions.cpp");
        var definition = new BindingCMakeProjectDefinition(
            "GeometryGenerated",
            "fixture",
            17,
            [new("GeometrySdk", "CONFIG REQUIRED"),],
            compileOptions: ["$<$<CXX_COMPILER_ID:MSVC>:/MP1>",],
            includeDirectories: ["${GeometrySdk_INCLUDE_DIR}",],
            linkLibraries: ["${GeometrySdk_LIBRARIES}",],
            targetProperties:
            [
                new("UNITY_BUILD", "ON"),
                new("UNITY_BUILD_MODE", "GROUP"),
                new("RUNTIME_OUTPUT_DIRECTORY", "\"${CMAKE_BINARY_DIR}/$<CONFIG>\""),
            ],
            unityBuild: new(32));

        var project = BindingCMakeProjectEmitter.Render(definition, sources);

        await Assert.That(project).Contains("PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/MP1>");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Flat_depth_1_batch_1\"");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Units_depth_0_batch_0\"");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Units_depth_1_batch_0\"");
        await Assert.That(project).Contains("UNITY_BUILD_MODE GROUP");
        await Assert.That(project).DoesNotContain("/MP8");
    }

    /// <summary>
    /// Verifies invalid CMake facts and source inventories fail before publication.
    /// </summary>
    /// <returns>A task that completes when validation is checked.</returns>
    [Test]
    public async Task Should_reject_invalid_project_facts_and_sources_Async()
    {
        var invalidDefinition = () => new BindingCMakeProjectDefinition(
            "Fixture\nInjected",
            "fixture",
            17,
            []);
        var definition = new BindingCMakeProjectDefinition("Fixture", "fixture", 17, []);
        var invalidSources = () => BindingCMakeProjectEmitter.Render(definition, ["../Escape.cpp",]);

        await Assert.That(invalidDefinition).Throws<ArgumentException>();
        await Assert.That(invalidSources).Throws<ArgumentException>();
    }
}