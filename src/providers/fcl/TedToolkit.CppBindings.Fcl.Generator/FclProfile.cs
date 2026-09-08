// -----------------------------------------------------------------------
// <copyright file="FclProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Describes the single supported FCL ABI profile.</summary>
public sealed record FclProfile
{
    /// <summary>Gets the stable profile identity.</summary>
    public string ProfileId { get; init; } = "fcl-0.7.0-obbrss-double-windows-v1";

    /// <summary>Gets the locked native and toolchain identities.</summary>
    public IReadOnlyDictionary<string, string> Versions { get; init; } = ReadOnly(new Dictionary<string, string>()
    {
        ["Fcl"] = "0.7.0#5",
        ["Ccd"] = "2.1#4",
        ["Eigen"] = "5.0.1",
        ["Octomap"] = "1.10.0",
        ["VcpkgBuiltinBaseline"] = "30ef65cad98f08e7197c9a1656fbd871bcb72f2d",
        ["CMake"] = "4.4.3",
        ["Msvc"] = "19.51.36256",
        ["Triplet"] = "x64-windows",
    });

    /// <summary>Gets the unique binding module basename.</summary>
    public string NativeLibraryBaseName { get; init; } = "ted_toolkit_cpp_bindings_fcl";

    /// <summary>Gets the exact native BVH status enumeration.</summary>
    public IReadOnlyDictionary<string, int> BvhReturnCodes { get; init; } = ReadOnly(new Dictionary<string, int>()
    {
        ["BVH_OK"] = 0,
        ["BVH_ERR_MODEL_OUT_OF_MEMORY"] = -1,
        ["BVH_ERR_BUILD_OUT_OF_SEQUENCE"] = -2,
        ["BVH_ERR_BUILD_EMPTY_MODEL"] = -3,
        ["BVH_ERR_BUILD_EMPTY_PREVIOUS_FRAME"] = -4,
        ["BVH_ERR_UNSUPPORTED_FUNCTION"] = -5,
        ["BVH_ERR_UNUPDATED_MODEL"] = -6,
        ["BVH_ERR_INCORRECT_DATA"] = -7,
        ["BVH_ERR_UNKNOWN"] = -8,
    });

    private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(Dictionary<TKey, TValue> values)
        where TKey : notnull
    {
        return new ReadOnlyDictionary<TKey, TValue>(values);
    }
}
