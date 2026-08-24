// -----------------------------------------------------------------------
// <copyright file="ExecuteAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models;
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
            foreach (var operation in AbiV1ConformanceModel.CreateOperations())
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
            foreach (var operation in AbiV1ConformanceModel.CreateOperations())
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

    /// <summary>
    /// Verifies the native interop declaration and its single implementation are copied into the transient project.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_copy_one_interop_declaration_and_implementation_into_the_cpp_project_Async()
    {
        var rootDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            var context = new CppCompileCoontext(rootDirectory, "ted_toolkit_occt", 17);
            await GenerateCppModule.CopyCppInteropSourcesAsync(context, CancellationToken.None)
                .ConfigureAwait(false);
            var sourceDirectory = Path.Combine(rootDirectory.FullName, "ted_toolkit_occt", "src");
            var header = await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "csharp_interop.h"))
                .ConfigureAwait(false);
            var implementation = await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "csharp_interop.cpp"))
                .ConfigureAwait(false);

            await Assert.That(header).Contains("API_EXPORT void free_error(interop_error error) noexcept;");
            await Assert.That(header).DoesNotContain("delete[] error.type_name");
            await Assert.That(implementation).Contains("API_EXPORT void free_error(interop_error error) noexcept");
            await Assert.That(implementation).Contains("delete[] error.type_name");
        }
        finally
        {
            if (rootDirectory.Exists)
            {
                rootDirectory.Delete(true);
            }
        }
    }
}