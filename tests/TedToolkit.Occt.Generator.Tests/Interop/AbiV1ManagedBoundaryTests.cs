// -----------------------------------------------------------------------
// <copyright file="AbiV1ManagedBoundaryTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.Occt.Generator.Tests.Interop;

/// <summary>
/// Verifies the managed ABI-major-1 boundary against the exact native C-consumer artifact.
/// </summary>
internal sealed class AbiV1ManagedBoundaryTests
{
    /// <summary>
    /// Verifies version gating, carrier layouts, failures, representative calls, and authoritative cleanup.
    /// </summary>
    /// <returns>A task that completes when the boundary assertions have finished.</returns>
    [Test]
    [NotInParallel("native-build-toolchain")]
    public async Task Should_enforce_and_consume_the_native_abi_v1_contract_Async()
    {
        var libraryPath = GetNativeLibraryPath();
        await Assert.That(File.Exists(libraryPath)).IsTrue();

        var rejectedResolutions = new List<string>();
        try
        {
            using var _ = LoadedBoundary.Load(libraryPath, 2, rejectedResolutions);
            Assert.Fail("ABI major 1 must not satisfy a major-2 consumer.");
        }
        catch (NotSupportedException)
        {
        }

        await Assert.That(rejectedResolutions).IsEquivalentTo(["ted_occt_v1_abi_version",]);

        var acceptedResolutions = new List<string>();
        using var boundary = LoadedBoundary.Load(libraryPath, 1, acceptedResolutions);
        await VerifyLayoutsAsync(boundary).ConfigureAwait(false);
        await VerifyFailuresAsync(boundary).ConfigureAwait(false);
        await VerifyPointAsync(boundary).ConfigureAwait(false);
        await VerifyUtf8Async(boundary).ConfigureAwait(false);
    }

    private static async Task VerifyLayoutsAsync(LoadedBoundary boundary)
    {
        var query = boundary.Resolve<LayoutDelegate>("ted_occt_v1_test_layout");
        await Assert.That(query(1)).IsEqualTo((ulong)Marshal.SizeOf<AbiError>());
        await Assert.That(query(2)).IsEqualTo((ulong)Marshal.OffsetOf<AbiError>(nameof(AbiError.Kind)).ToInt64());
        await Assert.That(query(3)).IsEqualTo((ulong)Marshal.OffsetOf<AbiError>(nameof(AbiError.TypeName)).ToInt64());
        await Assert.That(query(4)).IsEqualTo((ulong)Marshal.OffsetOf<AbiError>(nameof(AbiError.Message)).ToInt64());
        await Assert.That(query(5)).IsEqualTo((ulong)Marshal.OffsetOf<AbiError>(nameof(AbiError.StackTrace)).ToInt64());
        await Assert.That(query(6)).IsEqualTo((ulong)Marshal.SizeOf<AbiPoint>());
        await Assert.That(query(7)).IsEqualTo((ulong)Marshal.OffsetOf<AbiPoint>(nameof(AbiPoint.X)).ToInt64());
        await Assert.That(query(8)).IsEqualTo((ulong)Marshal.OffsetOf<AbiPoint>(nameof(AbiPoint.Y)).ToInt64());
        await Assert.That(query(9)).IsEqualTo((ulong)Marshal.SizeOf<BytesView>());
        await Assert.That(query(10)).IsEqualTo((ulong)Marshal.OffsetOf<BytesView>(nameof(BytesView.Data)).ToInt64());
        await Assert.That(query(11)).IsEqualTo((ulong)Marshal.OffsetOf<BytesView>(nameof(BytesView.Length)).ToInt64());
        await Assert.That(query(12)).IsEqualTo((ulong)Marshal.SizeOf<OwnedBytes>());
        await Assert.That(query(13)).IsEqualTo((ulong)Marshal.OffsetOf<OwnedBytes>(nameof(OwnedBytes.Data)).ToInt64());
        await Assert.That(query(14)).IsEqualTo((ulong)Marshal.OffsetOf<OwnedBytes>(nameof(OwnedBytes.Length)).ToInt64());
    }

    private static async Task VerifyFailuresAsync(LoadedBoundary boundary)
    {
        var testError = boundary.Resolve<TestErrorDelegate>("ted_occt_v1_test_error");
        var clear = boundary.Resolve<ErrorClearDelegate>("ted_occt_v1_error_clear");
        var failAllocation = boundary.Resolve<FailAllocationDelegate>(
            "ted_occt_v1_test_fail_diagnostic_allocation_after");

        var reserved = testError(11);
        await Assert.That(reserved.Kind).IsEqualTo(42);
        await Assert.That(reserved.TypeName).IsEqualTo(nint.Zero);
        clear(ref reserved);
        clear(ref reserved);
        await Assert.That(reserved.Kind).IsEqualTo(0);

        var unknown = testError(10);
        await Assert.That(unknown.Kind).IsEqualTo(255);
        await Assert.That(Marshal.PtrToStringUTF8(unknown.Message)).IsEqualTo("Unknown native exception");
        clear(ref unknown);
        clear(ref unknown);
        await Assert.That(unknown.Message).IsEqualTo(nint.Zero);

        failAllocation(0);
        var diagnosticFailure = testError(9);
        await Assert.That(diagnosticFailure.Kind).IsEqualTo(9);
        await Assert.That(diagnosticFailure.TypeName).IsEqualTo(nint.Zero);
        await Assert.That(diagnosticFailure.Message).IsEqualTo(nint.Zero);
        await Assert.That(diagnosticFailure.StackTrace).IsEqualTo(nint.Zero);
        clear(ref diagnosticFailure);
        failAllocation(-1);
    }

    private static async Task VerifyPointAsync(LoadedBoundary boundary)
    {
        var create = boundary.Resolve<PointCreateDelegate>(
            "ted_occt_v1_pnt2d_create__fc15364701135459bd24b26dfc9ccce3");
        var getX = boundary.Resolve<PointGetDelegate>(
            "ted_occt_v1_pnt2d_get_x__f43aaa7be0191777857affe8fef827ea");
        var point = default(AbiPoint);
        var error = create(3.5, -7.0, ref point);
        await Assert.That(error.Kind).IsEqualTo(0);
        double x = 0;
        error = getX(point, ref x);
        await Assert.That(error.Kind).IsEqualTo(0);
        await Assert.That(x).IsEqualTo(3.5);
    }

    private static async Task VerifyUtf8Async(LoadedBoundary boundary)
    {
        var copy = boundary.Resolve<CopyUtf8Delegate>(
            "ted_occt_v1_ascii_string_copy_utf8__91cb86f8be22465dbb714f301f325044");
        var clear = boundary.Resolve<OwnedBytesClearDelegate>("ted_occt_v1_owned_bytes_clear");
        var input = "Ted 中文"u8.ToArray();
        var inputMemory = Marshal.AllocHGlobal(input.Length);
        try
        {
            Marshal.Copy(input, 0, inputMemory, input.Length);
            var output = default(OwnedBytes);
            var error = copy(new(inputMemory, (ulong)input.Length), ref output);
            await Assert.That(error.Kind).IsEqualTo(0);
            await Assert.That(output.Length).IsEqualTo((ulong)input.Length);
            var actual = new byte[input.Length];
            Marshal.Copy(output.Data, actual, 0, actual.Length);
            await Assert.That(actual).IsEquivalentTo(input);
            clear(ref output);
            clear(ref output);
            await Assert.That(output.Data).IsEqualTo(nint.Zero);
            await Assert.That(output.Length).IsEqualTo(0UL);
        }
        finally
        {
            Marshal.FreeHGlobal(inputMemory);
        }
    }

    private static string GetNativeLibraryPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TedToolkit.Occt.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Unable to locate the repository root.");
        }

        return Path.Combine(directory.FullName, "out", "build", "ted-occt-abi-v1-consumer", "abi-v1",
            "ted_toolkit_occt.dll");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AbiError
    {
        public int Kind;

        public nint TypeName;

        public nint Message;

        public nint StackTrace;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AbiPoint
    {
        public double X;

        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct BytesView
    {
        public BytesView(in nint data, ulong length)
        {
            Data = data;
            Length = length;
        }

        public readonly nint Data;

        public readonly ulong Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OwnedBytes
    {
        public nint Data;

        public ulong Length;
    }

    private sealed class LoadedBoundary : IDisposable
    {
        private readonly nint _library;

        private readonly ICollection<string> _resolutions;

        private LoadedBoundary(in nint library, ICollection<string> resolutions)
        {
            _library = library;
            _resolutions = resolutions;
        }

        public static LoadedBoundary Load(string libraryPath, uint requiredMajor, ICollection<string> resolutions)
        {
            var library = NativeLibrary.Load(libraryPath);
            var boundary = new LoadedBoundary(library, resolutions);
            try
            {
                var version = boundary.Resolve<VersionDelegate>("ted_occt_v1_abi_version")();
                if ((version >> 16) != requiredMajor)
                {
                    throw new NotSupportedException(
                        $"Native ABI major {version >> 16} does not match required major {requiredMajor}.");
                }

                return boundary;
            }
            catch
            {
                boundary.Dispose();
                throw;
            }
        }

        public T Resolve<T>(string symbol)
            where T : Delegate
        {
            _resolutions.Add(symbol);
            return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, symbol));
        }

        public void Dispose()
        {
            NativeLibrary.Free(_library);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint VersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong LayoutDelegate(int query);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate AbiError TestErrorDelegate(int scenario);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ErrorClearDelegate(ref AbiError error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FailAllocationDelegate(int successfulAllocations);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate AbiError PointCreateDelegate(double x, double y, ref AbiPoint result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate AbiError PointGetDelegate(AbiPoint point, ref double result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate AbiError CopyUtf8Delegate(BytesView input, ref OwnedBytes result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OwnedBytesClearDelegate(ref OwnedBytes result);
}