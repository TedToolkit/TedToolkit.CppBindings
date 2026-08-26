// -----------------------------------------------------------------------
// <copyright file="OwnedConstructorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Runtime.Analyzers.Tests.GeneratedCodeOnlyUsageAnalyzerTests;

/// <summary>
/// Verifies generated-only admission for the real Runtime Owned constructor.
/// </summary>
internal sealed class OwnedConstructorTests
{
    /// <summary>
    /// Verifies that handwritten Owned construction is rejected through the real Runtime contract.
    /// </summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Test]
    public async Task Should_report_handwritten_owned_construction_Async()
    {
        const string Source = """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using TedToolkit.Occt;

            public struct Raii : IOcctRaii;

            public static unsafe class Consumer
            {
                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                private static void Destroy(Raii* value) { }

                public static void Use()
                {
                    using var owner = new Owned<Raii>(&Destroy);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTOCCT001",]);
    }

    /// <summary>
    /// Verifies that generated source can call the real Runtime Owned constructor.
    /// </summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Test]
    public async Task Should_allow_generated_owned_construction_Async()
    {
        const string Source = """
            using System;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using TedToolkit.Occt;

            public struct Raii : IOcctRaii;

            public static unsafe class Wrapper
            {
                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                private static void Destroy(Raii* value) { }

                public static Owned<Raii> Create()
                {
                    var owner = new Owned<Raii>(&Destroy);
                    var constructionSucceeded = false;

                    try
                    {
                        fixed (Raii* target = &owner.Value)
                        {
                            *target = default;
                        }

                        constructionSucceeded = true;
                        GC.KeepAlive(owner);
                        return owner;
                    }
                    finally
                    {
                        if (!constructionSucceeded)
                        {
                            GC.SuppressFinalize(owner);
                        }
                    }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost
            .AnalyzeGeneratorOutputAsync(Source)
            .ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }
}