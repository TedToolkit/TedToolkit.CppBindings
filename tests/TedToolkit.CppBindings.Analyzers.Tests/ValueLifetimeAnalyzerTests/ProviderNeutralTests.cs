// -----------------------------------------------------------------------
// <copyright file="ProviderNeutralTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Analyzers.Tests.GeneratedCodeOnlyUsageAnalyzerTests;

namespace TedToolkit.CppBindings.Analyzers.Tests.ValueLifetimeAnalyzerTests;

/// <summary>
/// Verifies recognition of ownership contracts without provider type-name assumptions.
/// </summary>
internal sealed class ProviderNeutralTests
{
    /// <summary>
    /// Verifies a third-party owner and interface receiver while ignoring an unrelated Value shape.
    /// </summary>
    /// <returns>A task that completes when diagnostic assertions finish.</returns>
    [Test]
    public async Task Should_follow_the_owner_contract_instead_of_type_names_Async()
    {
        const string Source = """
            using TedToolkit.CppBindings;

            namespace OtherLibrary;

            public class Lease : ICppOwner<int>
            {
                private int value;
                public ref int Value => ref value;
                public void Dispose() { }
            }

            public sealed class DerivedLease : Lease { }

            public sealed class Handle
            {
                private int value;
                public ref int Value => ref value;
                public void Dispose() { }
            }

            public static class Consumer
            {
                public static ref int Escape(Lease owner) => ref owner.Value;
                public static ref int EscapeInterface(ICppOwner<int> owner) => ref owner.Value;
                public static ref int EscapeInherited(DerivedLease owner) => ref owner.Value;
                public static ref int Unrelated(Handle value) => ref value.Value;
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB002", "TTCB002", "TTCB002",]);
    }
}