// -----------------------------------------------------------------------
// <copyright file="AddOcctGeneratorsTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines;

using TedToolkit.Occt.Generator.Options;

namespace TedToolkit.Occt.Generator.Tests.PipelineBuilderExtensionTests;

/// <summary>
/// Verifies OCCT generator pipeline registration.
/// </summary>
internal sealed class AddOcctGeneratorsTests
{
    /// <summary>
    /// Verifies an invalid native library basename is rejected before a pipeline can clean existing output.
    /// </summary>
    /// <returns>A task that completes when the atomic registration assertions have finished.</returns>
    [Test]
    public async Task Should_reject_an_invalid_library_basename_before_pipeline_execution_Async()
    {
        var rootDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var csharpDirectory = rootDirectory.CreateSubdirectory("csharp");
        var cppDirectory = rootDirectory.CreateSubdirectory("cpp");
        var csharpSentinel = new FileInfo(Path.Combine(csharpDirectory.FullName, "sentinel.txt"));
        var cppSentinel = new FileInfo(Path.Combine(cppDirectory.FullName, "sentinel.txt"));
        await File.WriteAllTextAsync(csharpSentinel.FullName, "preserve").ConfigureAwait(false);
        await File.WriteAllTextAsync(cppSentinel.FullName, "preserve").ConfigureAwait(false);

        try
        {
            var options = new GenerationOptions()
            {
                DeclOptions = [],
                CSharpFolder = csharpDirectory,
                CppFolder = cppDirectory,
                NativeLibraryBaseName = "product.dll",
            };
            ArgumentException? exception = null;
            try
            {
                _ = Pipeline.CreateBuilder().AddOcctGenerators(options);
            }
            catch (ArgumentException caught)
            {
                exception = caught;
            }

            await Assert.That(exception).IsNotNull();
            await Assert.That(await File.ReadAllTextAsync(csharpSentinel.FullName).ConfigureAwait(false))
                .IsEqualTo("preserve");
            await Assert.That(await File.ReadAllTextAsync(cppSentinel.FullName).ConfigureAwait(false))
                .IsEqualTo("preserve");
        }
        finally
        {
            rootDirectory.Delete(true);
        }
    }
}