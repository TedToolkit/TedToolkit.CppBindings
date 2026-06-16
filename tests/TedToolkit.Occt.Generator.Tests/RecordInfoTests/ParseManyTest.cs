// -----------------------------------------------------------------------
// <copyright file="ParseManyTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Tests.RecordInfoTests;

/// <summary>
/// <see cref="RecordInfo.ParseMany(string)"/>
/// </summary>
internal sealed class ParseManyTest
{
    /// <summary>
    /// Verifies grouped probe records are parsed into their corresponding record information.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_parse_grouped_probe_output_Async()
    {
        const string output = "S\tA\t16\nF\tX\t0\nF\tY\t8\nS\tB\t24\nF\tZ\t4\n";

        var infos = RecordInfo.ParseMany(output);

        await Assert.That(infos.Count).IsEqualTo(2);
        await Assert.That(infos["A"].Size).IsEqualTo(16L);
        await Assert.That(infos["A"].GetOffset("X")).IsEqualTo(0L);
        await Assert.That(infos["A"].GetOffset("Y")).IsEqualTo(8L);
        await Assert.That(infos["B"].Size).IsEqualTo(24L);
        await Assert.That(infos["B"].GetOffset("Z")).IsEqualTo(4L);
    }

    /// <summary>
    /// Verifies a field row without a preceding record header is rejected.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_reject_field_before_record_header_Async()
    {
        const string output = "F\tX\t0\n";

        var action = () => RecordInfo.ParseMany(output);

        await Assert.That(action).Throws<InvalidOperationException>();
    }
}