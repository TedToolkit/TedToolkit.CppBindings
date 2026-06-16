// -----------------------------------------------------------------------
// <copyright file="GenerateProbeOutputDirectoryCMakeTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Services;

namespace TedToolkit.Occt.Generator.Tests.RecordLayoutServiceTests;

/// <summary>
/// <see cref="RecordLayoutService.GenerateProbeOutputDirectoryCMake"/>
/// </summary>
internal sealed class GenerateProbeOutputDirectoryCMakeTest
{
    /// <summary>
    /// Verifies the generated CMake snippet keeps the probe executable in the build directory.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_pin_probe_output_to_the_build_directory_Async()
    {
        var cmake = RecordLayoutService.GenerateProbeOutputDirectoryCMake();

        await Assert.That(cmake).Contains("RUNTIME_OUTPUT_DIRECTORY \"${CMAKE_BINARY_DIR}\"");
        await Assert.That(cmake).Contains("RUNTIME_OUTPUT_DIRECTORY_RELEASE \"${CMAKE_BINARY_DIR}\"");
    }
}