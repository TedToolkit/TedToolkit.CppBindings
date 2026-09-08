// -----------------------------------------------------------------------
// <copyright file="ManifoldProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>
/// Describes the single supported Manifold ABI profile.
/// </summary>
public sealed record ManifoldProfile
{
    /// <summary>The stable profile identity.</summary>
    public const string DefaultProfileId = "manifold-3.5.2-windows-v1";

    /// <summary>Gets the profile identity.</summary>
    public string ProfileId { get; init; } = DefaultProfileId;

    /// <summary>Gets the native library version.</summary>
    public string ManifoldVersion { get; init; } = "3.5.2";

    /// <summary>Gets the target vcpkg triplet.</summary>
    public string Triplet { get; init; } = "x64-windows";

    /// <summary>Gets the locked vcpkg registry baseline.</summary>
    public string VcpkgBuiltinBaseline { get; init; } = "30ef65cad98f08e7197c9a1656fbd871bcb72f2d";

    /// <summary>Gets the locked CMake version.</summary>
    public string CMake { get; init; } = "4.4.3";

    /// <summary>Gets the locked MSVC version.</summary>
    public string Msvc { get; init; } = "19.51.36256";

    /// <summary>Gets the unique binding module basename.</summary>
    public string NativeLibraryBaseName { get; init; } = "ted_toolkit_cpp_bindings_manifold";

    /// <summary>Gets the locked native operation-enum underlying type.</summary>
    public string NativeOperationUnderlyingType { get; init; } = "char";

    /// <summary>Gets the locked native status-enum underlying type.</summary>
    public string NativeErrorUnderlyingType { get; init; } = "int";

    /// <summary>Gets the exact native operation enumeration.</summary>
    public IReadOnlyDictionary<string, int> Operations { get; init; } = ReadOnly(new Dictionary<string, int>
    {
        ["Add"] = 0,
        ["Subtract"] = 1,
        ["Intersect"] = 2,
    });

    /// <summary>Gets the exact native status enumeration.</summary>
    public IReadOnlyDictionary<string, int> Errors { get; init; } = ReadOnly(new Dictionary<string, int>
    {
        ["NoError"] = 0,
        ["NonFiniteVertex"] = 1,
        ["NotManifold"] = 2,
        ["VertexOutOfBounds"] = 3,
        ["PropertiesWrongLength"] = 4,
        ["MissingPositionProperties"] = 5,
        ["MergeVectorsDifferentLengths"] = 6,
        ["MergeIndexOutOfBounds"] = 7,
        ["TransformWrongLength"] = 8,
        ["RunIndexWrongLength"] = 9,
        ["FaceIDWrongLength"] = 10,
        ["InvalidConstruction"] = 11,
        ["ResultTooLarge"] = 12,
        ["InvalidTangents"] = 13,
        ["Cancelled"] = 14,
    });

    private static IReadOnlyDictionary<string, int> ReadOnly(Dictionary<string, int> values)
    {
        return new ReadOnlyDictionary<string, int>(values);
    }
}
