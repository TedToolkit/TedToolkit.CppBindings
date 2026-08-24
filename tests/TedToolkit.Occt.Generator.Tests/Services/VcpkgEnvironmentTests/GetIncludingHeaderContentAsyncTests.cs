// -----------------------------------------------------------------------
// <copyright file="GetIncludingHeaderContentAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;

namespace TedToolkit.Occt.Generator.Tests.Services.VcpkgEnvironmentTests;

/// <summary>
/// <see cref="VcpkgEnvironment.GetIncludingHeaderContentAsync"/> behavior.
/// </summary>
internal sealed class GetIncludingHeaderContentAsyncTests
{
    private const string TRIPLET = "x64-windows";

    /// <summary>
    /// Verifies only distinct requested public headers enter the relay.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_include_only_distinct_requested_headers_Async()
    {
        using var fixture = await VcpkgFixture.CreateAsync("Target", "Unrelated").ConfigureAwait(false);
        var environment = new VcpkgEnvironment();

        var content = await environment.GetIncludingHeaderContentAsync(
                TRIPLET,
                [new("Target"), new("Target"),],
                CancellationToken.None)
            .ConfigureAwait(false);

        await Assert.That(content).Contains("#include <Target.hxx>");
        await Assert.That(content).DoesNotContain("#include <Unrelated.hxx>");
        await Assert.That(content.Split("#include <Target.hxx>", StringSplitOptions.None).Length - 1).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies a target collection is required before relay construction.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_reject_an_empty_target_collection_Async()
    {
        using var fixture = await VcpkgFixture.CreateAsync("Target").ConfigureAwait(false);
        var environment = new VcpkgEnvironment();

        var exception = await CaptureInvalidOperationAsync(() => environment.GetIncludingHeaderContentAsync(
                TRIPLET,
                [],
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("least one target");
    }

    /// <summary>
    /// Verifies a null target collection is rejected before relay construction.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_reject_a_null_target_collection_Async()
    {
        using var fixture = await VcpkgFixture.CreateAsync("Target").ConfigureAwait(false);
        var environment = new VcpkgEnvironment();

        var exception = await CaptureInvalidOperationAsync(() => environment.GetIncludingHeaderContentAsync(
                TRIPLET,
                null!,
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("least one target");
    }

    /// <summary>
    /// Verifies a null target entry reports its index and required grammar.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_reject_a_null_target_entry_Async()
    {
        using var fixture = await VcpkgFixture.CreateAsync("Target").ConfigureAwait(false);
        var environment = new VcpkgEnvironment();

        var exception = await CaptureInvalidOperationAsync(() => environment.GetIncludingHeaderContentAsync(
                TRIPLET,
                [null!,],
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("index 0");
        await Assert.That(exception.Message).Contains("[A-Za-z_][A-Za-z0-9_]*");
    }

    /// <summary>
    /// Verifies malformed target values report their index, value, and required grammar.
    /// </summary>
    /// <param name="fileName">The malformed target value.</param>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("Foo.hxx")]
    [Arguments("../Foo")]
    [Arguments("Foo Bar")]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_reject_a_malformed_target_with_diagnostic_evidence_Async(string? fileName)
    {
        using var fixture = await VcpkgFixture.CreateAsync("Target").ConfigureAwait(false);
        var environment = new VcpkgEnvironment();

        var exception = await CaptureInvalidOperationAsync(() => environment.GetIncludingHeaderContentAsync(
                TRIPLET,
                [new(fileName!),],
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("index 0");
        await Assert.That(exception.Message).Contains("[A-Za-z_][A-Za-z0-9_]*");
    }

    /// <summary>
    /// Verifies a missing selected header reports the target and resolved OCCT include root.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_reject_a_missing_requested_header_Async()
    {
        using var fixture = await VcpkgFixture.CreateAsync("Target").ConfigureAwait(false);
        var environment = new VcpkgEnvironment();

        var exception = await CaptureInvalidOperationAsync(() => environment.GetIncludingHeaderContentAsync(
                TRIPLET,
                [new("Missing"),],
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("Missing");
        await Assert.That(exception.Message).Contains(fixture.OcctIncludeRoot.FullName);
    }

    private static async Task<InvalidOperationException> CaptureInvalidOperationAsync(Func<Task<string>> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected relay construction to fail.");
    }

    private sealed class VcpkgFixture : IDisposable
    {
        private readonly string? _originalRoot;

        private VcpkgFixture(DirectoryInfo root, DirectoryInfo occtIncludeRoot, string? originalRoot)
        {
            Root = root;
            OcctIncludeRoot = occtIncludeRoot;
            _originalRoot = originalRoot;
        }

        public DirectoryInfo Root { get; }

        public DirectoryInfo OcctIncludeRoot { get; }

        public static async Task<VcpkgFixture> CreateAsync(params string[] headerStems)
        {
            var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            var occtIncludeRoot = Directory.CreateDirectory(
                Path.Combine(root.FullName, "installed", TRIPLET, "include", "opencascade"));

            foreach (var headerStem in headerStems)
            {
                await File.WriteAllTextAsync(
                        Path.Combine(occtIncludeRoot.FullName, $"{headerStem}.hxx"),
                        $"struct {headerStem} {{}};")
                    .ConfigureAwait(false);
            }

            var originalRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
            Environment.SetEnvironmentVariable("VCPKG_ROOT", root.FullName);
            return new(root, occtIncludeRoot, originalRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("VCPKG_ROOT", _originalRoot);
            Root.Delete(true);
        }
    }
}