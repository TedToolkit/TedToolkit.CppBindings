// -----------------------------------------------------------------------
// <copyright file="ExecuteAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Abi.Conformance;
using TedToolkit.Occt.Generator.Abi.Generation;
using TedToolkit.Occt.Generator.Modules;

namespace TedToolkit.Occt.Generator.Tests.Modules.GenerateModuleTests;

/// <summary>
/// Shared C++ interop source materialization.
/// </summary>
internal sealed class ExecuteAsyncTest
{
    /// <summary>
    /// Verifies that the active native-generation boundary materializes only the canonical ABI-major-1 header.
    /// </summary>
    /// <returns>A task that completes when the generated artifact assertions have finished.</returns>
    [Test]
    public async Task Should_materialize_the_canonical_header_without_legacy_wrapper_sources_Async()
    {
        var outputDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            var header = await GenerateCppModule.GenerateAbiHeaderAsync(outputDirectory, CancellationToken.None)
                .ConfigureAwait(false);
            var content = await File.ReadAllTextAsync(header.FullName).ConfigureAwait(false);

            await Assert.That(header.Name).IsEqualTo("ted_toolkit_occt_v1.h");
            await Assert.That(outputDirectory.EnumerateFiles("*.cpp", SearchOption.AllDirectories)).IsEmpty();
            await Assert.That(content).DoesNotContain("CSHARP_WRAPPER");
            foreach (var operation in AbiV1ConformanceCatalog.CreateOperations())
            {
                await Assert.That(content).Contains(AbiOperationIdentity.Create(operation).SymbolName);
            }
        }
        finally
        {
            if (outputDirectory.Exists)
            {
                outputDirectory.Delete(true);
            }
        }
    }

    /// <summary>
    /// Verifies that the active native-generation boundary materializes a buildable ABI-major-1 adapter project.
    /// </summary>
    /// <returns>A task that completes when the generated project assertions have finished.</returns>
    [Test]
    public async Task Should_materialize_the_versioned_native_adapter_project_Async()
    {
        var outputDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(outputDirectory, CancellationToken.None)
                .ConfigureAwait(false);

            var implementationPath = Path.Combine(outputDirectory.FullName, "ted_toolkit_occt_v1.cpp");
            var implementation = await File.ReadAllTextAsync(implementationPath).ConfigureAwait(false);
            var cmake = await File.ReadAllTextAsync(Path.Combine(outputDirectory.FullName, "CMakeLists.txt"))
                .ConfigureAwait(false);

            await Assert.That(outputDirectory.EnumerateFiles("ted_toolkit_occt_v1.h")).HasSingleItem();
            await Assert.That(outputDirectory.EnumerateFiles("ted_toolkit_occt_v1.cpp")).HasSingleItem();
            await Assert.That(cmake).Contains("ted_toolkit_occt_abi_v1");
            await Assert.That(cmake).Contains("find_package(OpenCASCADE CONFIG REQUIRED)");
            foreach (var operation in AbiV1ConformanceCatalog.CreateOperations())
            {
                await Assert.That(implementation).Contains(AbiOperationIdentity.Create(operation).SymbolName);
            }
        }
        finally
        {
            if (outputDirectory.Exists)
            {
                outputDirectory.Delete(true);
            }
        }
    }
}