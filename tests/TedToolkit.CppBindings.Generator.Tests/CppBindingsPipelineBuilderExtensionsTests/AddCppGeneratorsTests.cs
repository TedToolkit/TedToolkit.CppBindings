// -----------------------------------------------------------------------
// <copyright file="AddCppGeneratorsTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator.Tests.CppBindingsPipelineBuilderExtensionsTests;

/// <summary>
/// Verifies source publication through typed filesystem paths.
/// </summary>
internal sealed class AddCppGeneratorsTests
{
    /// <summary>
    /// Verifies relative typed roots are normalized, cleared, and populated with nested generated sources.
    /// </summary>
    /// <returns>A task that completes when publication assertions finish.</returns>
    [Test]
    public async Task Should_publish_sources_under_relative_typed_roots_Async()
    {
        var root = DirectoryPath.CreateTempSubdirectory("CppBindings.FileSystem.");
        try
        {
            var relativeRoot = new DirectoryPath(Path.GetRelativePath(DirectoryPath.CurrentDirectory.FullName, root.FullName));
            var options = new GenerationOptions()
            {
                CSharpFolder = relativeRoot / "managed",
                CppFolder = relativeRoot / "native",
            };
            options.CSharpFolder.Create();
            options.CppFolder.Create();
            var staleManaged = options.CSharpFolder / new FileName("stale.txt");
            var staleNative = options.CppFolder / new FileName("stale.txt");
            await staleManaged.WriteAllTextAsync("stale managed").ConfigureAwait(false);
            await staleNative.WriteAllTextAsync("stale native").ConfigureAwait(false);
            var builder = Pipeline.CreateBuilder();
            builder.Options.PrintLogo = false;
            builder.Options.PrintResults = false;
            builder.Options.ShowProgressInConsole = false;
            builder.Options.DefaultRetryCount = 0;
            builder.Options.ThrowOnPipelineFailure = true;
            var pipeline = await builder.AddCppGenerators(options, new Provider()).BuildAsync().ConfigureAwait(false);

            var summary = await pipeline.RunAsync().ConfigureAwait(false);

            await Assert.That(summary.Status).IsEqualTo(Status.Successful);
            await Assert.That(await (root / "managed" / "nested" / new FileName("Test.g.cs")).ReadAllTextAsync().ConfigureAwait(false))
                .IsEqualTo("// managed\n");
            await Assert.That(await (root / "native" / "nested" / new FileName("Test.cpp")).ReadAllTextAsync().ConfigureAwait(false))
                .IsEqualTo("// native\n");
            await Assert.That((root / "managed" / new FileName("NativeApi.g.cs")).Exists).IsTrue();
            await Assert.That((root / "native" / new FileName("NativeFunctionTable.cpp")).Exists).IsTrue();
            await Assert.That(staleManaged.Exists).IsFalse();
            await Assert.That(staleNative.Exists).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies an uninitialized output path is rejected before any existing output is cleared.
    /// </summary>
    /// <param name="managed">Whether the managed output path is uninitialized.</param>
    /// <returns>A task that completes when rejection assertions finish.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Should_reject_default_paths_before_cleaning_output_Async(bool managed)
    {
        var root = DirectoryPath.CreateTempSubdirectory("CppBindings.FileSystem.");
        try
        {
            var sentinel = root / new FileName("sentinel.txt");
            await sentinel.WriteAllTextAsync("retained").ConfigureAwait(false);
            var options = new GenerationOptions()
            {
                CSharpFolder = managed ? default : root,
                CppFolder = managed ? root : default,
            };

            await Assert.That(() => Pipeline.CreateBuilder().AddCppGenerators(options, new Provider()))
                .Throws<ArgumentNullException>();
            await Assert.That(await sentinel.ReadAllTextAsync().ConfigureAwait(false)).IsEqualTo("retained");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private sealed class Provider : IGenerationProvider
    {
        public IReadOnlyList<Type> PreparationModules { get; } = [];

        public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new GenerationPlan(
                [new("nested/Test.g.cs", static (writer, token) => writer.WriteAsync("// managed\n".AsMemory(), token)),],
                [new("nested/Test.cpp", static (writer, token) => writer.WriteAsync("// native\n".AsMemory(), token)),],
                []));
        }
    }
}