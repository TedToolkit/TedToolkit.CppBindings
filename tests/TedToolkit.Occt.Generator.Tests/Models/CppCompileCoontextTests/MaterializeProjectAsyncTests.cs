// -----------------------------------------------------------------------
// <copyright file="MaterializeProjectAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Tests.Models.CppCompileCoontextTests;

/// <summary>
/// <see cref="CppCompileCoontext"/> project materialization behavior.
/// </summary>
internal sealed class MaterializeProjectAsyncTests
{
    /// <summary>
    /// Verifies wrapper completion order cannot change or duplicate the CMake compilation-source manifest.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_emit_each_compilation_source_once_in_ordinal_order_Async()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            var context = new CppCompileCoontext(root, "ted_toolkit_occt", 17);
            await Task.WhenAll(
                    context.AddSourceAsync("Zulu.cpp", "// Zulu", CancellationToken.None),
                    context.AddSourceAsync("csharp_interop.h", "#pragma once", CancellationToken.None),
                    context.AddSourceAsync("Alpha.cpp", "// Alpha", CancellationToken.None),
                    context.AddSourceAsync("Middle.cpp", "// Middle", CancellationToken.None))
                .ConfigureAwait(false);

            var cmakeFile = await context.MaterializeProjectAsync(false, CancellationToken.None)
                .ConfigureAwait(false);
            var cmake = await File.ReadAllTextAsync(cmakeFile.FullName).ConfigureAwait(false);

            await Assert.That(cmake)
                .Contains("add_library(ted_toolkit_occt SHARED Alpha.cpp Middle.cpp Zulu.cpp)");
            await Assert.That(cmake).DoesNotContain("csharp_interop.h");
        }
        finally
        {
            root.Delete(true);
        }
    }
}