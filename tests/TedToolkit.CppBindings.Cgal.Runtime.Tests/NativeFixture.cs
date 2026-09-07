// -----------------------------------------------------------------------
// <copyright file="NativeFixture.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.CppBindings;

namespace TedToolkit.CppBindings.Cgal.Runtime.Tests;

/// <summary>
/// Loads the compiled C++20 fixture and exposes its ABI through typed function pointers.
/// </summary>
internal static unsafe class NativeFixture
{
    private static readonly nint Library = NativeLibrary.Load(
        Path.Combine(AppContext.BaseDirectory, "cgal_runtime_fixture.dll"));

    private static readonly delegate* unmanaged[Cdecl]<void> ResetCountersExport =
        (delegate* unmanaged[Cdecl]<void>)GetExport("CgalRuntime_ResetCounters");

    private static readonly delegate* unmanaged[Cdecl]<int> GetDiagnosticClearCountExport =
        (delegate* unmanaged[Cdecl]<int>)GetExport("CgalRuntime_GetDiagnosticClearCount");

    private static readonly delegate* unmanaged[Cdecl]<int> GetContainerCreateCountExport =
        (delegate* unmanaged[Cdecl]<int>)GetExport("CgalRuntime_GetContainerCreateCount");

    private static readonly delegate* unmanaged[Cdecl]<int> GetContainerDestroyCountExport =
        (delegate* unmanaged[Cdecl]<int>)GetExport("CgalRuntime_GetContainerDestroyCount");

    private static readonly delegate* unmanaged[Cdecl]<int> GetAlternativeTransferCountExport =
        (delegate* unmanaged[Cdecl]<int>)GetExport("CgalRuntime_GetAlternativeTransferCount");

    private static readonly delegate* unmanaged[Cdecl]<int, int, NativeError*, void> CreateErrorExport =
        (delegate* unmanaged[Cdecl]<int, int, NativeError*, void>)GetExport("CgalRuntime_CreateError");

    private static readonly delegate* unmanaged[Cdecl]<NativeError*, void> ClearErrorExport =
        (delegate* unmanaged[Cdecl]<NativeError*, void>)GetExport("CgalRuntime_ClearError");

    private static readonly delegate* unmanaged[Cdecl]<int, NativeResult*, void> CreateResultExport =
        (delegate* unmanaged[Cdecl]<int, NativeResult*, void>)GetExport("CgalRuntime_CreateResult");

    /// <summary>
    /// Gets the number of native diagnostic clears.
    /// </summary>
    internal static int DiagnosticClearCount
    {
        get
        {
            return GetDiagnosticClearCountExport();
        }
    }

    /// <summary>
    /// Gets the number of constructed native result containers.
    /// </summary>
    internal static int ContainerCreateCount
    {
        get
        {
            return GetContainerCreateCountExport();
        }
    }

    /// <summary>
    /// Gets the number of destroyed native result containers.
    /// </summary>
    internal static int ContainerDestroyCount
    {
        get
        {
            return GetContainerDestroyCountExport();
        }
    }

    /// <summary>
    /// Gets the number of known alternatives transferred into the ABI transport.
    /// </summary>
    internal static int AlternativeTransferCount
    {
        get
        {
            return GetAlternativeTransferCountExport();
        }
    }

    /// <summary>
    /// Gets the fixture diagnostic cleanup export.
    /// </summary>
    /// <returns>The native cleanup entry point.</returns>
    internal static delegate* unmanaged[Cdecl]<NativeError*, void> GetClearError()
    {
        return ClearErrorExport;
    }

    /// <summary>
    /// Resets all native counters.
    /// </summary>
    internal static void ResetCounters()
    {
        ResetCountersExport();
    }

    /// <summary>
    /// Creates a native diagnostic owner.
    /// </summary>
    /// <param name="kind">The native error kind.</param>
    /// <param name="malformedUtf8">Whether the message contains malformed UTF-8.</param>
    /// <returns>The owning error carrier.</returns>
    internal static NativeError CreateError(int kind, bool malformedUtf8 = false)
    {
        NativeError error = default;
        CreateErrorExport(kind, malformedUtf8 ? 1 : 0, &error);
        return error;
    }

    /// <summary>
    /// Creates one native polymorphic-result scenario.
    /// </summary>
    /// <param name="scenario">The result scenario number.</param>
    /// <returns>The value-only ABI transport.</returns>
    internal static NativeResult CreateResult(int scenario)
    {
        NativeResult result = default;
        CreateResultExport(scenario, &result);
        return result;
    }

    private static nint GetExport(string name)
    {
        return NativeLibrary.GetExport(Library, name);
    }
}