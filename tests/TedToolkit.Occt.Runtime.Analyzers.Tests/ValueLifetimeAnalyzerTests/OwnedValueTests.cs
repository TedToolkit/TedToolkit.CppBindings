// -----------------------------------------------------------------------
// <copyright file="OwnedValueTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Runtime.Analyzers.Tests.GeneratedCodeOnlyUsageAnalyzerTests;

namespace TedToolkit.Occt.Runtime.Analyzers.Tests.ValueLifetimeAnalyzerTests;

/// <summary>
/// Verifies value-lifetime diagnostics against the real Runtime Owned contract.
/// </summary>
internal sealed class OwnedValueTests
{
    /// <summary>
    /// Verifies that accessing the real Runtime Owned value after disposal reports TTOCCT002.
    /// </summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Test]
    public async Task Should_report_disposed_real_runtime_owned_value_access_Async()
    {
        const string Source = """
            using TedToolkit.Occt;

            public struct Raii : IOcctRaii
            {
                public int Number;
            }

            public static class Consumer
            {
                public static void Use(Owned<Raii> owner)
                {
                    owner.Dispose();
                    _ = owner.Value.Number;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTOCCT002",]);
    }
}