// -----------------------------------------------------------------------
// <copyright file="CgalResultProjectionCompatibilityTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace TedToolkit.CppBindings.Cgal.Generator.Tests;

/// <summary>
/// Binds the compiled Runtime result fixture to the result projection in Shared's current CGAL plan.
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
        var provider = new CgalGenerationProvider(new()
        {
            VcpkgRoot = new(GetVcpkgRoot()),
            RequireLockedHeaderInventory = false,
            RequireLockedToolchain = false,
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
            CSharpNamespace = "TedToolkit.CppBindings.Cgal.Runtime.Tests",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
            CppVersion = 20,
        });
        var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var source = plan.CSharpSources.Single(static item => item.RelativePath == "Cgal.ResultProjection.g.cs");
        var writer = new StringWriter();
        await using (writer.ConfigureAwait(false))
        {
            await source.RenderAsync(writer, CancellationToken.None).ConfigureAwait(false);
            var actual = writer.ToString();
            var expected = await File.ReadAllTextAsync(GetRuntimeGeneratedSourcePath()).ConfigureAwait(false);

            await Assert.That(Normalize(actual)).IsEqualTo(Normalize(expected));
        }
    }

    private static string GetVcpkgRoot()
    {
        return Environment.GetEnvironmentVariable("VCPKG_ROOT") is { Length: > 0, } root ? root : @"C:\vcpkg";
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