// -----------------------------------------------------------------------
// <copyright file="CleanTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Tests.GenerationOutputCleanerTests;

/// <summary>
/// <see cref="IGenerationOutputCleaner.Clean()"/>
/// </summary>
internal sealed class CleanTest
{
    /// <summary>
    /// Verifies generator output folders are emptied before generation starts.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_clear_both_generation_output_folders_through_di_without_deleting_the_roots_Async()
    {
        var root = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var csharpFolder = root.CreateSubdirectory("csharp");
        var cppFolder = root.CreateSubdirectory("cpp");
        Directory.CreateDirectory(Path.Combine(csharpFolder.FullName, "nested"));
        Directory.CreateDirectory(Path.Combine(cppFolder.FullName, "nested"));
        await File.WriteAllTextAsync(Path.Combine(csharpFolder.FullName, "nested", "old.g.cs"), "stale")
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(cppFolder.FullName, "nested", "old.cpp"), "stale")
            .ConfigureAwait(false);

        var options = new GenerationOptions()
        {
            DeclOptions = [],
            CSharpFolder = csharpFolder,
            CppFolder = cppFolder,
        };

        try
        {
            using var serviceProvider = new ServiceCollection()
                .AddSingleton<IOptions<GenerationOptions>>(Microsoft.Extensions.Options.Options.Create(options))
                .AddSingleton<IGenerationOutputCleaner, GenerationOutputCleaner>()
                .BuildServiceProvider();

            var cleaner = serviceProvider.GetRequiredService<IGenerationOutputCleaner>();

            cleaner.Clean();

            await Assert.That(csharpFolder.Exists).IsTrue();
            await Assert.That(cppFolder.Exists).IsTrue();
            await Assert.That(csharpFolder.GetFileSystemInfos()).IsEmpty();
            await Assert.That(cppFolder.GetFileSystemInfos()).IsEmpty();
        }
        finally
        {
            if (root.Exists)
            {
                root.Delete(true);
            }
        }
    }
}
