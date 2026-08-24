// -----------------------------------------------------------------------
// <copyright file="BuildAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Tests.Models.CppCompileCoontextTests;

/// <summary>
/// <see cref="CppCompileCoontext"/> build-result handling.
/// </summary>
internal sealed class BuildAsyncTests
{
    /// <summary>
    /// Verifies a nonzero configure result fails with its stage, tool, exit code, and diagnostic.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_fail_with_configure_diagnostics_when_cmake_exits_nonzero_Async()
    {
        using var fixture = BuildFixture.Create();
        var invocations = 0;

        var exception = await CaptureInvalidOperationAsync(() => fixture.Context.BuildAsync(
                (_, _, _) =>
                {
                    invocations++;
                    return Task.FromResult(new CppCommandResult(17, "", "controlled failure"));
                },
                false,
                fixture.VcpkgRoot,
                "x64-windows",
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("configure");
        await Assert.That(exception.Message).Contains("cmake");
        await Assert.That(exception.Message).Contains("17");
        await Assert.That(exception.Message).Contains("controlled failure");
        await Assert.That(invocations).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies a nonzero build result is distinguished from configuration failure in diagnostics.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_fail_with_build_diagnostics_when_cmake_build_exits_nonzero_Async()
    {
        using var fixture = BuildFixture.Create();
        var invocations = 0;

        var exception = await CaptureInvalidOperationAsync(() => fixture.Context.BuildAsync(
                (_, _, _) =>
                {
                    invocations++;
                    return Task.FromResult(invocations == 1
                        ? new CppCommandResult(0, "", "")
                        : new(23, "", "controlled build failure"));
                },
                false,
                fixture.VcpkgRoot,
                "x64-windows",
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("build");
        await Assert.That(exception.Message).Contains("cmake");
        await Assert.That(exception.Message).Contains("23");
        await Assert.That(exception.Message).Contains("controlled build failure");
        await Assert.That(invocations).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies command cancellation remains pipeline cancellation instead of becoming build failure.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_preserve_cancellation_when_the_command_is_cancelled_Async()
    {
        using var fixture = BuildFixture.Create();
        using var source = new CancellationTokenSource();

        var exception = await CaptureCancellationAsync(() => fixture.Context.BuildAsync(
                async (_, _, token) =>
                {
                    await source.CancelAsync().ConfigureAwait(false);
                    return await Task.FromCanceled<CppCommandResult>(token).ConfigureAwait(false);
                },
                false,
                fixture.VcpkgRoot,
                "x64-windows",
                source.Token))
            .ConfigureAwait(false);

        await Assert.That(exception.CancellationToken).IsEqualTo(source.Token);
    }

    /// <summary>
    /// Verifies a successful command sequence cannot return a stale or missing native artifact.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_reject_a_stale_artifact_when_the_build_produces_no_output_Async()
    {
        using var fixture = BuildFixture.Create();
        var artifactPath = fixture.GetArtifactPath();
        await File.WriteAllTextAsync(artifactPath, "stale").ConfigureAwait(false);

        var exception = await CaptureInvalidOperationAsync(() => fixture.Context.BuildAsync(
                static (_, _, _) => Task.FromResult(new CppCommandResult(0, "", "")),
                false,
                fixture.VcpkgRoot,
                "x64-windows",
                CancellationToken.None))
            .ConfigureAwait(false);

        await Assert.That(exception.Message).Contains("artifact");
        await Assert.That(exception.Message).Contains("ted_toolkit_occt.dll");
        await Assert.That(File.Exists(artifactPath)).IsFalse();
    }

    /// <summary>
    /// Verifies a newly produced native artifact is returned after configure and build succeed.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_return_the_new_artifact_when_configure_and_build_succeed_Async()
    {
        using var fixture = BuildFixture.Create();
        var invocations = 0;

        var artifact = await fixture.Context.BuildAsync(
                async (_, arguments, token) =>
                {
                    invocations++;
                    if (arguments.Contains("--build", StringComparer.Ordinal))
                    {
                        await File.WriteAllTextAsync(fixture.GetArtifactPath(), "native", token)
                            .ConfigureAwait(false);
                    }

                    return new(0, "", "");
                },
                false,
                fixture.VcpkgRoot,
                "x64-windows",
                CancellationToken.None)
            .ConfigureAwait(false);

        await Assert.That(invocations).IsEqualTo(2);
        await Assert.That(artifact.FullName).IsEqualTo(fixture.GetArtifactPath());
        await Assert.That(artifact.Exists).IsTrue();
    }

    private static async Task<InvalidOperationException> CaptureInvalidOperationAsync(Func<Task<FileInfo>> action)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the native build to fail.");
    }

    private static async Task<OperationCanceledException> CaptureCancellationAsync(Func<Task<FileInfo>> action)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the native build to be cancelled.");
    }

    private sealed class BuildFixture : IDisposable
    {
        private BuildFixture(DirectoryInfo root)
        {
            Root = root;
            VcpkgRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "vcpkg")).FullName;
            Context = new(root, "ted_toolkit_occt", 17);
        }

        public CppCompileCoontext Context { get; }

        public DirectoryInfo Root { get; }

        public string VcpkgRoot { get; }

        public static BuildFixture Create()
        {
            return new(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())));
        }

        public string GetArtifactPath()
        {
            return Path.Combine(Root.FullName, "ted_toolkit_occt", "bin", "ted_toolkit_occt.dll");
        }

        public void Dispose()
        {
            Root.Delete(true);
        }
    }
}