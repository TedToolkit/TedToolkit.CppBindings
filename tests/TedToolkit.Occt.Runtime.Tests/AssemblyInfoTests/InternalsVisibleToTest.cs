// -----------------------------------------------------------------------
// <copyright file="InternalsVisibleToTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.AssemblyInfoTests;

/// <summary>
/// Verifies the runtime assembly exposes the expected friend assemblies.
/// </summary>
internal sealed class InternalsVisibleToTest
{
    /// <summary>
    /// Ensures the runtime assembly includes the triplet-specific friend assemblies.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Should_expose_triplet_specific_friend_assemblies_Async()
    {
        var names = typeof(IStandard_Transient).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(static attribute => attribute.AssemblyName)
            .ToArray();

        await Assert.That(names.Length).IsEqualTo(100);
        await Assert.That(names).Contains("TedToolkit.Occt.arm-neon-android");
        await Assert.That(names).Contains("TedToolkit.Occt.x64-windows");
        await Assert.That(names).Contains("TedToolkit.Occt.x64-windows-static-md-release");
        await Assert.That(names).Contains("TedToolkit.Occt.x64-linux");
        await Assert.That(names).Contains("TedToolkit.Occt.x64-osx");
        await Assert.That(names).Contains("TedToolkit.Occt.arm64-osx");
        await Assert.That(names).Contains("TedToolkit.Occt.wasm32-emscripten");
    }
}