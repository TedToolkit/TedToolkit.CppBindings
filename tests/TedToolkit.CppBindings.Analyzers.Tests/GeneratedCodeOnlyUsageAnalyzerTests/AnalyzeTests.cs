// -----------------------------------------------------------------------
// <copyright file="AnalyzeTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Analyzers.Tests.GeneratedCodeOnlyUsageAnalyzerTests;

/// <summary>
/// Verifies generated-only Runtime API usage diagnostics.
/// </summary>
internal sealed class AnalyzeTests
{
    /// <summary>
    /// Verifies explicit and target-typed handwritten Handle construction.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_handwritten_handle_construction_Async()
    {
        const string Source = """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public struct Transient : IStandard_Transient;

            public static unsafe class Consumer
            {
                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                private static void Release(Transient* value) { }

                public static void Use(Transient* value)
                {
                    using var first = new Handle<Transient>(value, &Release);
                    Handle<Transient> second = new(value, &Release);
                    second.Dispose();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics).Count().IsEqualTo(2);
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB001", "TTCB001",]);
    }

    /// <summary>
    /// Verifies every supported operational reference shape for marked members.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_every_marked_operational_reference_Async()
    {
        const string Source = """
            using System;
            using TedToolkit.CppBindings;

            public sealed class Hooks
            {
                [GeneratedCodeOnly]
                public Hooks() { }

                [GeneratedCodeOnly]
                public void Call() { }

                [GeneratedCodeOnly]
                public int Value { get; set; }

                [GeneratedCodeOnly]
                public event Action? Changed;
            }

            public static class Consumer
            {
                public static void Use()
                {
                    var hooks = new Hooks();
                    hooks.Call();
                    _ = hooks.Value;
                    hooks.Value = 1;
                    hooks.Changed += Handler;
                    Action capture = hooks.Call;
                }

                private static void Handler() { }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics).Count().IsEqualTo(6);
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB001", "TTCB001", "TTCB001", "TTCB001", "TTCB001", "TTCB001",]);
    }

    /// <summary>
    /// Verifies that marking a type reserves its callable members for generated code.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_callable_members_declared_by_a_marked_type_Async()
    {
        const string Source = """
            using TedToolkit.CppBindings;

            [GeneratedCodeOnly]
            public sealed class Hooks
            {
                public int Value;

                public void Call() { }
            }

            public static class Consumer
            {
                public static void Use()
                {
                    var hooks = new Hooks();
                    hooks.Call();
                    _ = hooks.Value;
                    hooks.Value = 1;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB001", "TTCB001", "TTCB001", "TTCB001",]);
    }

    /// <summary>
    /// Verifies that generated source can call the public Runtime native-error projection bridge.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_allow_generated_native_error_projection_Async()
    {
        const string Source = """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using TedToolkit.CppBindings;
            using TedToolkit.CppBindings.Occt;

            public static unsafe class Wrapper
            {
                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                private static void Clear(NativeError* error) { }

                public static void Project(ref NativeError error)
                {
                    error.Kind = 0;
                    _ = error.Message;
                    NativeErrorProjection.ThrowIfFailed(ref error, &Clear);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost
            .AnalyzeGeneratorOutputAsync(Source)
            .ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Verifies standard generated source is exempt from the caller policy.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_ignore_generated_source_Async()
    {
        const string Source = """
            using TedToolkit.CppBindings;

            public static class Wrapper
            {
                public static void Use()
                {
                    var hooks = new Hooks();
                    hooks.Call();
                }
            }

            public sealed class Hooks
            {
                [GeneratedCodeOnly]
                public Hooks() { }

                [GeneratedCodeOnly]
                public void Call() { }
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

    /// <summary>
    /// Verifies markers placed directly on accessors are honored.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_report_marked_accessor_references_Async()
    {
        const string Source = """
            using System;
            using TedToolkit.CppBindings;

            public sealed class Hooks
            {
                public int Value
                {
                    [GeneratedCodeOnly]
                    get { return 0; }
                }

                public event Action Changed
                {
                    [GeneratedCodeOnly]
                    add { }
                    remove { }
                }
            }

            public static class Consumer
            {
                public static void Use(Hooks hooks)
                {
                    _ = hooks.Value;
                    hooks.Changed += Handler;
                }

                private static void Handler() { }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TTCB001", "TTCB001",]);
    }

    /// <summary>
    /// Verifies non-operational symbol names do not trigger the usage rule.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_ignore_non_operational_symbol_names_Async()
    {
        const string Source = """
            using System;
            using TedToolkit.CppBindings;

            public sealed class Hooks
            {
                [GeneratedCodeOnly]
                public void Call() { }

                [GeneratedCodeOnly]
                public int Value { get; }

                [GeneratedCodeOnly]
                public event Action? Changed;
            }

            public static class Consumer
            {
                public static string[] Names()
                {
                    return [nameof(Hooks.Call), nameof(Hooks.Value), nameof(Hooks.Changed),];
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Verifies ordinary Handle disposal and Value access remain outside analyzer scope.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_allow_value_access_and_disposal_Async()
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
                public static void Use(Handle<Transient> handle)
                {
                    ref var value = ref handle.Value;
                    value.Number++;
                    handle.Dispose();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(Source).ConfigureAwait(false);

        await Assert.That(diagnostics).IsEmpty();
    }
}