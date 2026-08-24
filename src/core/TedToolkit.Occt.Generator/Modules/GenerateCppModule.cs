// -----------------------------------------------------------------------
// <copyright file="GenerateCppModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Generates the canonical C interoperability declaration for ABI major 1.
/// </summary>
[DependsOn<CleanGenerationOutputModule>]
[DependsOn<ParseModule>]
public sealed class GenerateCppModule : Module<bool>
{
    private readonly IOptions<GenerationOptions> _generationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCppModule"/> class.
    /// </summary>
    /// <param name="generationOptions">The generation options.</param>
    internal GenerateCppModule(IOptions<GenerationOptions> generationOptions)
    {
        _generationOptions = generationOptions;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = await GenerateAbiProjectAsync(_generationOptions.Value.CppFolder, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Materializes the canonical ABI-major-1 header and its versioned native adapter project.
    /// </summary>
    /// <param name="outputDirectory">The native generation output directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized native project directory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputDirectory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The approved conformance model or embedded project is incomplete.</exception>
    public static async Task<DirectoryInfo> GenerateAbiProjectAsync(
        DirectoryInfo outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        _ = await GenerateAbiHeaderAsync(outputDirectory, cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(
                CopyAbiProjectResourceAsync(outputDirectory, "CMakeLists.txt", cancellationToken),
                CopyAbiProjectResourceAsync(outputDirectory, "ted_toolkit_occt_v1.cpp", cancellationToken),
                CopyAbiProjectResourceAsync(outputDirectory, "ted_toolkit_occt_v1_test.h", cancellationToken))
            .ConfigureAwait(false);
        return outputDirectory;
    }

    /// <summary>
    /// Materializes the canonical ABI-major-1 header without emitting legacy C++ wrapper sources.
    /// </summary>
    /// <param name="outputDirectory">The native generation output directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated canonical header file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputDirectory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The approved conformance model is incomplete.</exception>
    internal static async Task<FileInfo> GenerateAbiHeaderAsync(
        DirectoryInfo outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        var generated = CAbiHeaderGenerator.Generate(AbiV1ConformanceModel.CreateOperations());
        if (generated.Diagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, generated.Diagnostics));
        }

        outputDirectory.Create();
        var header = new FileInfo(Path.Combine(outputDirectory.FullName, "ted_toolkit_occt_v1.h"));
        await File.WriteAllTextAsync(header.FullName, generated.Header, cancellationToken).ConfigureAwait(false);
        return header;
    }

    private static async Task CopyAbiProjectResourceAsync(
        DirectoryInfo outputDirectory,
        string fileName,
        CancellationToken cancellationToken)
    {
        var resourceName = ZString.Concat("TedToolkit.Occt.Generator.Assets.cpp.abi-v1.", fileName);
        var sourceStream = typeof(GenerateCppModule).Assembly.GetManifestResourceStream(resourceName);
        if (sourceStream is null)
        {
            throw new InvalidOperationException($"Embedded ABI-major-1 project resource '{resourceName}' is missing.");
        }

        await using var _ = sourceStream.ConfigureAwait(false);
        using var reader = new StreamReader(sourceStream);
        var source = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var outputPath = Path.Combine(outputDirectory.FullName, fileName);
        await File.WriteAllTextAsync(outputPath, source, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies the shared native interop declaration and implementation into the transient project.
    /// </summary>
    /// <param name="compile">The transient native compilation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when both sources have been written.</returns>
    internal static async Task CopyCppInteropSourcesAsync(
        CppCompileCoontext compile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compile);
        await Task.WhenAll(
                CopyEmbeddedSourceAsync("csharp_interop.h", cancellationToken),
                CopyEmbeddedSourceAsync("csharp_interop.cpp", cancellationToken))
            .ConfigureAwait(false);

        async Task CopyEmbeddedSourceAsync(string fileName, CancellationToken token)
        {
            var resourceName = ZString.Concat("TedToolkit.Occt.Generator.Assets.cpp.", fileName);
            var sourceStream = typeof(GenerateCppModule).Assembly.GetManifestResourceStream(resourceName);
            ArgumentNullException.ThrowIfNull(sourceStream);
            await using var _ = sourceStream.ConfigureAwait(false);

            using var reader = new StreamReader(sourceStream);
            await compile.AddSourceAsync(
                    fileName,
                    await reader.ReadToEndAsync(token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(false);
        }
    }
}