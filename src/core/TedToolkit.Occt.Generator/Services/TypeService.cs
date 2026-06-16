// -----------------------------------------------------------------------
// <copyright file="TypeService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Normalizes Clang type names for generator output.
/// </summary>
public sealed class TypeService : ITypeService
{
    /// <inheritdoc/>
    public string GetCppName(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var result = type.AsString;

        if (string.IsNullOrEmpty(result))
        {
            throw new InvalidOperationException("Field type name cannot be empty. Please check your arguments.");
        }

        return result;
    }

    /// <inheritdoc/>
    public string GetCSharpName(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return GetCppName(type)
            .Replace("::", "_", StringComparison.InvariantCulture)
            .Replace('<', '_')
            .Replace('>', '_')
            .Trim('_');
    }

    /// <inheritdoc/>
    public ClangSharp.Type DesugarType(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.CanonicalType;
    }
}