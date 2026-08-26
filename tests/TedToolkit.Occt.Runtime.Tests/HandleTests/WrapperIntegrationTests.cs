// -----------------------------------------------------------------------
// <copyright file="WrapperIntegrationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.Occt.Runtime.FirstWrapper;
using TedToolkit.Occt.Runtime.SecondWrapper;

namespace TedToolkit.Occt.Runtime.Tests.HandleTests;

/// <summary>
/// Verifies independent wrapper use of Runtime's ordinary public Handle contract.
/// </summary>
internal sealed class WrapperIntegrationTests
{
    /// <summary>
    /// Verifies that independent wrappers bind their own direct unmanaged release functions.
    /// </summary>
    /// <returns>A task that completes when both wrapper assertions finish.</returns>
    [Test]
    public async Task Should_use_each_wrappers_matching_release_function_without_friend_access_Async()
    {
        var observation = UseIndependentWrappers();

        await Assert.That(observation.FirstValue).IsEqualTo(11);
        await Assert.That(observation.SecondValue).IsEqualTo(23);
        await Assert.That(observation.FirstReleaseCount).IsEqualTo(1);
        await Assert.That(observation.SecondReleaseCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that the Windows-named example has no wrapper-specific Runtime privilege.
    /// </summary>
    /// <returns>A task that completes when the assembly-boundary assertions finish.</returns>
    [Test]
    public async Task Should_give_the_windows_named_wrapper_no_additional_capability_Async()
    {
        var windowsAssembly = typeof(FirstTransient).Assembly;
        var friends = windowsAssembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(static attribute => attribute.AssemblyName)
            .ToArray();

        await Assert.That(windowsAssembly.GetName().Name).IsEqualTo("TedToolkit.Occt.Windows");
        await Assert.That(friends).IsEmpty();
        await Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(FirstTransient))).IsFalse();
        await Assert.That(typeof(FirstTransient).GetMethod(nameof(IDisposable.Dispose))).IsNull();
    }

    private static unsafe (
        int FirstValue,
        int SecondValue,
        int FirstReleaseCount,
        int SecondReleaseCount) UseIndependentWrappers()
    {
        var firstReleaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        var secondReleaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var first = FirstTransientFactory.Create(11, firstReleaseCount);
            var second = SecondTransientFactory.Create(23, secondReleaseCount);
            var firstValue = first.Value.Value;
            var secondValue = second.Value.Value;

            second.Dispose();
            first.Dispose();
            return (firstValue, secondValue, *firstReleaseCount, *secondReleaseCount);
        }
        finally
        {
            NativeMemory.Free(firstReleaseCount);
            NativeMemory.Free(secondReleaseCount);
        }
    }
}