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

    private static readonly delegate* unmanaged[Cdecl]<NativeError*, void> CreateMalformedErrorExport =
        (delegate* unmanaged[Cdecl]<NativeError*, void>)GetExport("CgalRuntime_CreateMalformedError");

    private static readonly delegate* unmanaged[Cdecl]<int, NativeError*, void> InvokeFailureExport =
        (delegate* unmanaged[Cdecl]<int, NativeError*, void>)GetExport("CgalRuntime_InvokeFailure");

    private static readonly delegate* unmanaged[Cdecl]<NativeError*, void> ClearErrorExport =
        (delegate* unmanaged[Cdecl]<NativeError*, void>)GetExport("CgalRuntime_ClearError");

    private static readonly delegate* unmanaged[Cdecl]<int, Segment_2_Intersection_Transport*, void>
        CreateVariantResultExport =
            (delegate* unmanaged[Cdecl]<int, Segment_2_Intersection_Transport*, void>)GetExport(
                "CgalRuntime_CreateVariantResult");

    private static readonly delegate* unmanaged[Cdecl]<int, Segment_2_Intersection_Transport*, void>
        CreateObjectResultExport =
            (delegate* unmanaged[Cdecl]<int, Segment_2_Intersection_Transport*, void>)GetExport(
                "CgalRuntime_CreateObjectResult");

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
    /// <param name="scenario">The actual CGAL, standard, or unknown failure scenario.</param>
    /// <returns>The owning error carrier.</returns>
    internal static NativeError InvokeFailure(int scenario)
    {
        NativeError error = default;
        InvokeFailureExport(scenario, &error);
        return error;
    }

    /// <summary>
    /// Creates a malformed diagnostic through the native owner boundary.
    /// </summary>
    /// <returns>The owning error carrier.</returns>
    internal static NativeError CreateMalformedError()
    {
        NativeError error = default;
        CreateMalformedErrorExport(&error);
        return error;
    }

    /// <summary>
    /// Creates one native optional/variant result scenario.
    /// </summary>
    /// <param name="scenario">The result scenario number.</param>
    /// <returns>The value-only ABI transport.</returns>
    internal static Segment_2_Intersection_Transport CreateVariantResult(int scenario)
    {
        Segment_2_Intersection_Transport result = default;
        CreateVariantResultExport(scenario, &result);
        return result;
    }

    /// <summary>
    /// Creates one native CGAL Object result scenario.
    /// </summary>
    /// <param name="scenario">The result scenario number.</param>
    /// <returns>The value-only ABI transport.</returns>
    internal static Segment_2_Intersection_Transport CreateObjectResult(int scenario)
    {
        Segment_2_Intersection_Transport result = default;
        CreateObjectResultExport(scenario, &result);
        return result;
    }

    private static nint GetExport(string name)
    {
        return NativeLibrary.GetExport(Library, name);
    }
}