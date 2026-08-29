// -----------------------------------------------------------------------
// <copyright file="PointerAdjustmentTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Services;

namespace TedToolkit.Occt.Generator.Tests.Services.RecordModelManagerTests;

/// <summary>
/// Verifies direct inheritance pointer-conversion classification.
/// </summary>
internal sealed class PointerAdjustmentTests
{
    /// <summary>
    /// Verifies the sole non-virtual MSVC base uses the identity fast path.
    /// </summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Test]
    public async Task Should_use_identity_for_one_non_virtual_base_Async()
    {
        var result = RecordModelManager.GetPointerAdjustmentKind(1, false, 0);

        await Assert.That(result).IsEqualTo(PointerAdjustmentKind.Identity);
    }

    /// <summary>
    /// Verifies complex inheritance uses generated native adjustment.
    /// </summary>
    /// <param name="directBaseCount">The direct base count.</param>
    /// <param name="isVirtual">Whether the selected base is virtual.</param>
    /// <param name="virtualBaseCount">The record virtual base count.</param>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Test]
    [Arguments(2, false, 0u)]
    [Arguments(1, true, 1u)]
    [Arguments(1, false, 1u)]
    public async Task Should_use_native_adjust_for_complex_inheritance_Async(
        int directBaseCount,
        bool isVirtual,
        uint virtualBaseCount)
    {
        var result = RecordModelManager.GetPointerAdjustmentKind(
            directBaseCount,
            isVirtual,
            virtualBaseCount);

        await Assert.That(result).IsEqualTo(PointerAdjustmentKind.NativeAdjust);
    }
}