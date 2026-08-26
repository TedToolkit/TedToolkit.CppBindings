// -----------------------------------------------------------------------
// <copyright file="WrapperIntegrationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt.Runtime.FirstWrapper;
using TedToolkit.Occt.Runtime.SecondWrapper;

namespace TedToolkit.Occt.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies independent wrapper use of Runtime's ordinary public Owned contract.
/// </summary>
internal sealed class WrapperIntegrationTests
{
    /// <summary>
    /// Verifies that independent wrappers construct and destroy their own contained RAII types.
    /// </summary>
    /// <returns>A task that completes when both wrapper assertions finish.</returns>
    [Test]
    public async Task Should_use_each_wrappers_matching_destructor_without_friend_access_Async()
    {
        var observation = UseIndependentWrappers();

        await Assert.That(observation.FirstValue).IsEqualTo(13);
        await Assert.That(observation.SecondValue).IsEqualTo(37);
        await Assert.That(observation.FirstDestroyCount).IsEqualTo(1);
        await Assert.That(observation.SecondDestroyCount).IsEqualTo(1);
    }

    private static unsafe (
        int FirstValue,
        int SecondValue,
        int FirstDestroyCount,
        int SecondDestroyCount) UseIndependentWrappers()
    {
        var firstDestroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        var secondDestroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var first = FirstRaiiFactory.Create(13, firstDestroyCount);
            var second = SecondRaiiFactory.Create(37, secondDestroyCount);
            var firstValue = first.Value.Value;
            var secondValue = second.Value.Value;

            second.Dispose();
            first.Dispose();
            return (firstValue, secondValue, *firstDestroyCount, *secondDestroyCount);
        }
        finally
        {
            NativeMemory.Free(firstDestroyCount);
            NativeMemory.Free(secondDestroyCount);
        }
    }
}