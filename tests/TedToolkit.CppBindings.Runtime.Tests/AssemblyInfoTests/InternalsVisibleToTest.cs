// -----------------------------------------------------------------------
// <copyright file="InternalsVisibleToTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

using TedToolkit.CppBindings;

namespace TedToolkit.CppBindings.Runtime.Tests.AssemblyInfoTests;

/// <summary>
/// Verifies the Runtime friend-assembly boundary.
/// </summary>
internal sealed class InternalsVisibleToTest
{
    /// <summary>
    /// Verifies that Runtime exposes internals only to its own test assembly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Should_expose_internals_only_to_runtime_tests_Async()
    {
        var names = typeof(NativeTypeNameAttribute).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(static attribute => attribute.AssemblyName)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["TedToolkit.CppBindings.Runtime.Tests",]);
    }
}