// -----------------------------------------------------------------------
// <copyright file="AnalyzeTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Analyzers.Tests.GeneratedCodeOnlyUsageAnalyzerTests;

namespace TedToolkit.CppBindings.Analyzers.Tests.ValueLifetimeAnalyzerTests;

/// <summary>
/// Verifies diagnostics for locally demonstrable owner Value lifetime hazards.
/// </summary>
internal sealed class AnalyzeTests
{
    /// <summary>
    /// Verifies the initial bounded set of reference, owner-state, and receiver hazards.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_supported_value_lifetime_hazards_Async()
    {
        const string Source = """
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;

                public void Touch() { }
            }

            public static class Consumer
            {
                public static ref Transient Escape(Handle<Transient> handle)
                    => ref handle.Value;

                public static void UseTemporary()
                    => GetHandle().Value.Number++;

                public static void UseAfterDispose(Handle<Transient> handle)
                {
                    handle.Dispose();
                    _ = handle.Value.Number;
                }

                public static void DisposeAliasWhileReferenceIsLive(Handle<Transient> handle)
                {
                    var alias = handle;
                    ref var value = ref handle.Value;
                    alias.Dispose();
                    value.Number++;
                }

                public static void UseAsRoutineReceiver(Handle<Transient> handle)
                    => handle.Value.Touch();

                public static void UseOwnedAfterDispose(Owned<Transient> owner)
                {
                    owner.Dispose();
                    _ = owner.Value.Number;
                }

                private static Handle<Transient> GetHandle() => throw null!;
            }

            namespace TedToolkit.CppBindings
            {
                public sealed class Owned<T> : ICppOwner<T> where T : unmanaged
                {
                    private T value;

                    public ref T Value => ref value;

                    public void Dispose() { }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics).Count().IsEqualTo(6);
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB002", "TTCB002", "TTCB002", "TTCB002", "TTCB002", "TTCB002",]);
    }

    /// <summary>
    /// Verifies a derived reference cannot remain live across asynchronous or iterator suspension.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_value_reference_used_across_suspension_Async()
    {
        const string AwaitSource = """
            using System.Threading.Tasks;
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;
            }

            public static class Consumer
            {
                public static async Task UseAsync(Handle<Transient> handle)
                {
                    ref var value = ref handle.Value;
                    await Task.Yield();
                    value.Number++;
                }
            }
            """;
        const string YieldSource = """
            using System.Collections.Generic;
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;
            }

            public static class Consumer
            {
                public static IEnumerable<int> Use(Handle<Transient> handle)
                {
                    ref var value = ref handle.Value;
                    yield return 0;
                    value.Number++;
                }
            }
            """;

        var awaitDiagnostics = await AnalyzerTestHost
            .AnalyzeAllowingCompilationErrorsAsync(AwaitSource)
            .ConfigureAwait(false);
        var yieldDiagnostics = await AnalyzerTestHost
            .AnalyzeAllowingCompilationErrorsAsync(YieldSource)
            .ConfigureAwait(false);

        await Assert.That(awaitDiagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB002",]);
        await Assert.That(yieldDiagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB002",]);
    }

    /// <summary>
    /// Verifies a conditional Dispose is not treated as a definitely disposed owner.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_ignore_owner_that_is_only_conditionally_disposed_Async()
    {
        const string Source = """
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;
            }

            public static class Consumer
            {
                public static void Use(Handle<Transient> handle, bool shouldDispose)
                {
                    if (shouldDispose)
                    {
                        handle.Dispose();
                    }

                    _ = handle.Value.Number;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Verifies fixed pointer use requires a following owner KeepAlive.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_fixed_value_use_without_keepalive_Async()
    {
        const string MissingSource = """
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;
            }

            public static unsafe class Consumer
            {
                public static void Use(Handle<Transient> handle)
                {
                    fixed (Transient* pointer = &handle.Value)
                    {
                        pointer->Number++;
                    }
                }
            }
            """;
        const string SafeSource = """
            using System;
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;
            }

            public static unsafe class Consumer
            {
                public static void Use(Handle<Transient> handle)
                {
                    fixed (Transient* pointer = &handle.Value)
                    {
                        pointer->Number++;
                    }

                    GC.KeepAlive(handle);
                }
            }
            """;
        const string ConditionalSource = """
            using System;
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient
            {
                public int Number;
            }

            public static unsafe class Consumer
            {
                public static void Use(Handle<Transient> handle, bool keepAlive)
                {
                    fixed (Transient* pointer = &handle.Value)
                    {
                        pointer->Number++;
                    }

                    if (keepAlive)
                    {
                        GC.KeepAlive(handle);
                    }
                }
            }
            """;

        var missingDiagnostics = await AnalyzerTestHost.AnalyzeAsync(MissingSource).ConfigureAwait(false);
        var safeDiagnostics = await AnalyzerTestHost.AnalyzeAsync(SafeSource).ConfigureAwait(false);
        var conditionalDiagnostics = await AnalyzerTestHost.AnalyzeAsync(ConditionalSource).ConfigureAwait(false);

        await Assert.That(missingDiagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB002",]);
        await Assert.That(safeDiagnostics).IsEmpty();
        await Assert.That(conditionalDiagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB002",]);
    }

    /// <summary>
    /// Verifies generated-code classification exempts owner Value lifetime diagnostics.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_ignore_generated_value_lifetime_hazards_Async()
    {
        const string Source = """
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient;

            public static class Wrapper
            {
                public static void Use(Handle<Transient> handle)
                {
                    handle.Dispose();
                    _ = handle.Value;
                }
            }
            """;

        var fileNameDiagnostics = await AnalyzerTestHost
            .AnalyzeAsync(Source, "Wrapper.g.cs")
            .ConfigureAwait(false);
        var headerDiagnostics = await AnalyzerTestHost
            .AnalyzeAsync("// <auto-generated/>\n" + Source, "Wrapper.cs")
            .ConfigureAwait(false);
        var configuredDiagnostics = await AnalyzerTestHost
            .AnalyzeConfiguredGeneratedAsync(Source, "ConfiguredWrapper.cs")
            .ConfigureAwait(false);
        var generatorDiagnostics = await AnalyzerTestHost
            .AnalyzeGeneratorOutputAsync(Source)
            .ConfigureAwait(false);

        await Assert.That(fileNameDiagnostics).IsEmpty();
        await Assert.That(headerDiagnostics).IsEmpty();
        await Assert.That(configuredDiagnostics).IsEmpty();
        await Assert.That(generatorDiagnostics).IsEmpty();
    }
}