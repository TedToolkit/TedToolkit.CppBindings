// -----------------------------------------------------------------------
// <copyright file="ExecuteAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using ClangSharp;

using Microsoft.Extensions.Logging;

using ModularPipelines.Context;
using ModularPipelines.Logging;

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;

using TUnit.Mocks;

namespace TedToolkit.Occt.Generator.Tests.Modules.ParseModuleTests;

/// <summary>
/// <see cref="ParseModule"/> execution behavior.
/// </summary>
internal sealed class ExecuteAsyncTests
{
    /// <summary>
    /// Verifies a requested record definition reaches the shared model through real Clang parsing.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_commit_a_requested_record_definition_Async()
    {
        using var fixture = await ParseFixture.CreateAsync(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Target"] = "struct Target { int Value; };", },
                [new("Target"),])
            .ConfigureAwait(false);

        var result = await ExecuteAsync(fixture.Module, fixture.Context).ConfigureAwait(false);

        await Assert.That(result).IsTrue();
        await Assert.That(fixture.RecordManager.AddedRecordNames).IsEquivalentTo(["Target",]);
    }

    /// <summary>
    /// Verifies every requested definition resolves before any record enters the shared model.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_reject_an_unresolved_target_without_partial_model_commit_Async()
    {
        using var fixture = await ParseFixture.CreateAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Target"] = "struct Target { int Value; };",
                    ["Alias"] = "struct Other { int Value; };",
                },
                [new("Target"), new("Alias"),])
            .ConfigureAwait(false);

        var exception = await CaptureInvalidOperationAsync(() => ExecuteAsync(fixture.Module, fixture.Context))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("Alias");
        await Assert.That(fixture.RecordManager.AddedRecordNames).IsEmpty();
    }

    /// <summary>
    /// Verifies an Error diagnostic fails Parse while preserving its literal text.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_fail_on_error_diagnostics_with_literal_evidence_Async()
    {
        using var fixture = await ParseFixture.CreateAsync(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Broken"] = "#error [broken-diagnostic]", },
                [new("Broken"),])
            .ConfigureAwait(false);

        var exception = await CaptureInvalidOperationAsync(() => ExecuteAsync(fixture.Module, fixture.Context))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("[broken-diagnostic]");
        await Assert.That(fixture.Logger.Messages).Contains(message => message.Contains("[broken-diagnostic]", StringComparison.Ordinal));
        await Assert.That(fixture.RecordManager.AddedRecordNames).IsEmpty();
    }

    /// <summary>
    /// Verifies duplicate targets enter the model once and bracketed warnings remain non-fatal and literal.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_keep_bracketed_warnings_literal_and_non_fatal_for_duplicate_targets_Async()
    {
        const string HeaderContent = "#warning [literal-warning]\nstruct Target { int Value; };";
        using var fixture = await ParseFixture.CreateAsync(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Target"] = HeaderContent, },
                [new("Target"), new("Target"),])
            .ConfigureAwait(false);

        var result = await ExecuteAsync(fixture.Module, fixture.Context).ConfigureAwait(false);

        await Assert.That(result).IsTrue();
        await Assert.That(fixture.Logger.Messages).Contains(message => message.Contains("[literal-warning]", StringComparison.Ordinal));
        await Assert.That(fixture.RecordManager.AddedRecordNames).IsEquivalentTo(["Target",]);
    }

    /// <summary>
    /// Verifies the selected real OCCT header parses without unrelated installed-header diagnostics.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [RequiresRealOcct]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_parse_only_the_selected_real_occt_header_Async()
    {
        const string Target = "Geom2d_BSplineCurve";
        var outputRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            var declarations = new DeclOptions[] { new(Target), };
            var environment = new VcpkgEnvironment();
            var tripletResolver = new VcpkgDefaultTripletResolver();
            var triplet = tripletResolver.GetTriplet();
            var relay = await environment.GetIncludingHeaderContentAsync(
                    triplet,
                    declarations,
                    CancellationToken.None)
                .ConfigureAwait(false);
            var options = Microsoft.Extensions.Options.Options.Create(new GenerationOptions()
            {
                DeclOptions = declarations,
                CSharpFolder = Directory.CreateDirectory(Path.Combine(outputRoot.FullName, "csharp")),
                CppFolder = Directory.CreateDirectory(Path.Combine(outputRoot.FullName, "cpp")),
                Triplet = triplet,
            });
            var recordManager = new CapturingRecordModelManager();
            var module = new ParseModule(recordManager, options, tripletResolver, environment);
            using var logger = new CapturingLogger();
            var context = IModuleContext.Mock(MockBehavior.Strict);
            context.Logger.Getter.Returns(logger);

            var result = await ExecuteAsync(module, context).ConfigureAwait(false);

            await Assert.That(relay).Contains("#include <Geom2d_BSplineCurve.hxx>");
            await Assert.That(relay).DoesNotContain("BRepGProp_Face.hxx");
            await Assert.That(result).IsTrue();
            await Assert.That(recordManager.AddedRecordNames).IsEquivalentTo([Target,]);
            await Assert.That(logger.Messages
                .Where(static message =>
                    message.Contains("BRepGProp_Face", StringComparison.Ordinal)
                    || message.Contains("BRepGProp_Gauss", StringComparison.Ordinal)
                    || message.Contains("GeomBndLib_InfiniteHelpers", StringComparison.Ordinal)
                    || message.Contains("GeomBndLib_Line", StringComparison.Ordinal)))
                .IsEmpty();
        }
        finally
        {
            outputRoot.Delete(true);
        }
    }

    private static Task<bool> ExecuteAsync(ParseModule module, IModuleContext context)
    {
        var method = typeof(ParseModule).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException("ParseModule.ExecuteAsync was not found.");
        }

        return (Task<bool>)method.Invoke(module, [context, CancellationToken.None,])!;
    }

    private static async Task<InvalidOperationException> CaptureInvalidOperationAsync(Func<Task<bool>> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected Parse to fail.");
    }

    private sealed class ParseFixture : IDisposable
    {
        private ParseFixture(
            DirectoryInfo root,
            ParseModule module,
            IModuleContext context,
            CapturingLogger logger,
            CapturingRecordModelManager recordManager)
        {
            Root = root;
            Module = module;
            Context = context;
            Logger = logger;
            RecordManager = recordManager;
        }

        public DirectoryInfo Root { get; }

        public ParseModule Module { get; }

        public IModuleContext Context { get; }

        public CapturingLogger Logger { get; }

        public CapturingRecordModelManager RecordManager { get; }

        public static async Task<ParseFixture> CreateAsync(
            IReadOnlyDictionary<string, string> headers,
            IReadOnlyList<DeclOptions> declarations)
        {
            var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            var includeRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "include"));
            var occtIncludeRoot = Directory.CreateDirectory(Path.Combine(includeRoot.FullName, "opencascade"));

            foreach (var header in headers)
            {
                await File.WriteAllTextAsync(
                        Path.Combine(occtIncludeRoot.FullName, $"{header.Key}.hxx"),
                        header.Value)
                    .ConfigureAwait(false);
            }

            var options = Microsoft.Extensions.Options.Options.Create(new GenerationOptions()
            {
                DeclOptions = declarations,
                CSharpFolder = Directory.CreateDirectory(Path.Combine(root.FullName, "csharp")),
                CppFolder = Directory.CreateDirectory(Path.Combine(root.FullName, "cpp")),
                Triplet = "x64-windows",
            });
            var environment = new FixtureVcpkgEnvironment(root, includeRoot, occtIncludeRoot);
            var recordManager = new CapturingRecordModelManager();
            var module = new ParseModule(recordManager, options, new FixtureTripletResolver(), environment);
            var logger = new CapturingLogger();
            var context = IModuleContext.Mock(MockBehavior.Strict);
            context.Logger.Getter.Returns(logger);

            return new(root, module, context, logger, recordManager);
        }

        public void Dispose()
        {
            Root.Delete(true);
        }
    }

    private sealed class FixtureVcpkgEnvironment(
        DirectoryInfo root,
        DirectoryInfo includeRoot,
        DirectoryInfo occtIncludeRoot) : IVcpkgEnvironment
    {
        public string GetRoot()
        {
            return root.FullName;
        }

        public string GetIncludeFolder(string triplet)
        {
            return includeRoot.FullName;
        }

        public string GetOcctIncludeFolder(string triplet)
        {
            return occtIncludeRoot.FullName;
        }

        public Task<string> GetIncludingHeaderContentAsync(
            string triplet,
            IReadOnlyList<DeclOptions> declarations,
            CancellationToken cancellationToken)
        {
            var content = string.Join(
                Environment.NewLine,
                declarations
                    .Select(static declaration => declaration.FileName)
                    .Distinct(StringComparer.Ordinal)
                    .Select(static target => $"#include <{target}.hxx>"));
            return Task.FromResult(content);
        }
    }

    private sealed class FixtureTripletResolver : IVcpkgDefaultTripletResolver
    {
        public string GetTriplet()
        {
            return "x64-windows";
        }
    }

    private sealed class CapturingRecordModelManager : IRecordModelManager
    {
        public List<string> AddedRecordNames { get; } = [];

        public IReadOnlyList<EnumModel> EnumModels { get; } = [];

        public IEnumerable<RecordModel> RecordModels { get; } = [];

        public RecordModel Add(CXXRecordDecl record)
        {
            AddedRecordNames.Add(record.Name);
            return null!;
        }
    }

    private sealed class CapturingLogger : IModuleLogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        public void Dispose()
        {
        }
    }
}