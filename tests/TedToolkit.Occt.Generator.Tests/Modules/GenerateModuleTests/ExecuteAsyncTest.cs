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
