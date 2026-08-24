// -----------------------------------------------------------------------
// <copyright file="NativeBoundaryTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Tests.Modules.ParseModuleTests;

namespace TedToolkit.Occt.Generator.Tests.Models.CppCompileCoontextTests;

/// <summary>
/// Real native compilation and loading behavior for the transient CMake project.
/// </summary>
internal sealed class NativeBoundaryTests
{
    /// <summary>
    /// Verifies multiple wrappers link, load, translate a standard exception, and release its payload in one library.
    /// </summary>
    /// <returns>A task that completes when the native boundary assertions have finished.</returns>
    [Test]
    [RequiresRealOcct]
    [NotInParallel("native-build-toolchain")]
    public async Task Should_link_load_translate_and_release_with_the_acceptance_toolchain_Async()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        try
        {
            var context = new CppCompileCoontext(root, "ted_toolkit_occt", 17);
            await GenerateCppModule.CopyCppInteropSourcesAsync(context, CancellationToken.None)
                .ConfigureAwait(false);
            await Task.WhenAll(
                    context.AddSourceAsync(
                        "FirstWrapper.cpp",
                        "#include \"csharp_interop.h\"\nCSHARP_WRAPPER(test_noop(), {})\n",
                        CancellationToken.None),
                    context.AddSourceAsync(
                        "SecondWrapper.cpp",
                        "#include \"csharp_interop.h\"\n#include <stdexcept>\n"
                        + "CSHARP_WRAPPER_TRY(test_throw_standard_exception(), "
                        + "{ throw std::runtime_error(\"expected native failure\"); })\n",
                        CancellationToken.None))
                .ConfigureAwait(false);

            var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
            await Assert.That(vcpkgRoot).IsNotNull().And.IsNotEmpty();
            var artifact = await context.BuildAsync(
                    RunProcessAsync,
                    false,
                    vcpkgRoot!,
                    "x64-windows",
                    CancellationToken.None)
                .ConfigureAwait(false);

            var library = NativeLibrary.Load(artifact.FullName);
            try
            {
                var freeError = Marshal.GetDelegateForFunctionPointer<FreeErrorDelegate>(
                    NativeLibrary.GetExport(library, "free_error"));
                var throwStandardException = Marshal.GetDelegateForFunctionPointer<ThrowStandardExceptionDelegate>(
                    NativeLibrary.GetExport(library, "test_throw_standard_exception"));

                var error = throwStandardException();
                try
                {
                    await Assert.That(error.TypeName).IsNotEqualTo(nint.Zero);
                    await Assert.That(error.Message).IsNotEqualTo(nint.Zero);
                    await Assert.That(Marshal.PtrToStringUTF8(error.Message)).IsEqualTo("expected native failure");
                }
                finally
                {
                    freeError(error);
                }
            }
            finally
            {
                NativeLibrary.Free(library);
            }
        }
        finally
        {
            await DeleteDirectoryAsync(root).ConfigureAwait(false);
        }
    }

    private static async Task DeleteDirectoryAsync(DirectoryInfo directory)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                directory.Refresh();
                if (directory.Exists)
                {
                    directory.Delete(true);
                }

                return;
            }
            catch (IOException) when (attempt < 19)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            }
        }
    }

    private static async Task<CppCommandResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process()
        {
            StartInfo = new()
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Unable to start {fileName}.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            throw;
        }

        return new(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InteropError
    {
        public nint TypeName;

        public nint Message;

        public nint StackTrace;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate InteropError ThrowStandardExceptionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeErrorDelegate(InteropError error);
}