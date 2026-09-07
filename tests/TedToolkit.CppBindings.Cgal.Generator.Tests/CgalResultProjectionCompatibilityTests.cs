// -----------------------------------------------------------------------
// <copyright file="CgalResultProjectionCompatibilityTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;

namespace TedToolkit.CppBindings.Cgal.Generator.Tests;

/// <summary>
/// Binds the compiled Runtime result fixture to the current CGAL Generator renderer.
/// </summary>
internal sealed class CgalResultProjectionCompatibilityTests
{
    /// <summary>
    /// Verifies the Runtime proof compiles the current generated result projection.
    /// </summary>
    /// <returns>A task that completes when generated-source parity is established.</returns>
    [Test]
    public async Task Should_match_the_runtime_compiled_result_projection_Async()
    {
        var renderer = typeof(CgalGenerationProvider).Assembly.GetType(
            "TedToolkit.CppBindings.Cgal.Generator.CgalSourceRenderer",
            throwOnError: true)!;
        var render = renderer.GetMethod(
            "RenderManagedResultProjection",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var actual = (string)render.Invoke(
            null,
            new object[] { "TedToolkit.CppBindings.Cgal.Runtime.Tests", })!;
        var expected = await File.ReadAllTextAsync(GetRuntimeGeneratedSourcePath()).ConfigureAwait(false);

        await Assert.That(Normalize(actual)).IsEqualTo(Normalize(expected));
    }

    private static string GetRuntimeGeneratedSourcePath(
        [CallerFilePath] string callerFilePath = "")
    {
        var tests = Directory.GetParent(Path.GetDirectoryName(callerFilePath)!)!;
        return Path.Combine(
            tests.FullName,
            "TedToolkit.CppBindings.Cgal.Runtime.Tests",
            "Generated",
            "Cgal.ResultProjection.g.cs");
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    }
}