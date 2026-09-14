// -----------------------------------------------------------------------
// <copyright file="CloneTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies wrapper-supplied explicit copy ownership.
/// </summary>
internal sealed class CloneTests
{
    /// <summary>
    /// Verifies that explicit cloning creates independent contained storage and cleanup.
    /// </summary>
    /// <returns>A task that completes when the clone assertions finish.</returns>
    [Test]
    public async Task Should_create_independent_ownership_when_explicitly_cloned_Async()
    {
        var observation = CloneAndDisposeIndependently();

        await Assert.That(observation.CloneValueAfterSourceDispose).IsEqualTo(47);
        await Assert.That(observation.SourceDestroyCount).IsEqualTo(1);
        await Assert.That(observation.CloneDestroyCount).IsEqualTo(1);
    }

    private static unsafe (int CloneValueAfterSourceDispose, int SourceDestroyCount, int CloneDestroyCount)
        CloneAndDisposeIndependently()
    {
        var sourceDestroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        var cloneDestroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            using var source = ControlledRaiiFactory.Create(47, sourceDestroyCount);
            using var clone = ControlledRaiiFactory.Clone(source, cloneDestroyCount);

            source.Dispose();
            var cloneValue = clone.Value.Value;
            clone.Dispose();
            return (cloneValue, *sourceDestroyCount, *cloneDestroyCount);
        }
        finally
        {
            NativeMemory.Free(sourceDestroyCount);
            NativeMemory.Free(cloneDestroyCount);
        }
    }
}