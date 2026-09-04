// -----------------------------------------------------------------------
// <copyright file="NativeErrorSupportGeneratorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Generators;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CppGeneratorTests;

/// <summary>
/// Verifies the shared native error owner emitted beside per-type sources.
/// </summary>
internal sealed class NativeErrorSupportGeneratorTests
{
    /// <summary>
    /// Verifies that the carrier matches the runtime layout and owns diagnostics through one clear export.
    /// </summary>
    /// <returns>A task that completes when the generated source assertions finish.</returns>
    [Test]
    public async Task Should_generate_minimal_error_transport_Async()
    {
        var header = NativeErrorSupportGenerator.GenerateHeader();
        var source = NativeErrorSupportGenerator.GenerateSource();

        await Assert.That(header).Contains("int Kind;");
        await Assert.That(header).Contains("char* TypeName;");
        await Assert.That(header).Contains("char* Message;");
        await Assert.That(header).Contains("char* StackTrace;");
        await Assert.That(header).Contains("extern \"C\" void NativeError_Clear");
        await Assert.That(source).Contains("std::free(error->TypeName);");
        await Assert.That(source).Contains("*error = {};");
        await Assert.That(source).DoesNotContain("namespace ");
    }
}