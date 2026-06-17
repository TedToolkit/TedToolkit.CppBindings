// -----------------------------------------------------------------------
// <copyright file="TypeService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Normalizes Clang type names for generator output.
/// </summary>
/// <param name="generationOptions">The generator options.</param>
public sealed class TypeService(IOptions<GenerationOptions> generationOptions) : ITypeService
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

        return NormalizeCSharpTypeName(GetCppName(type));
    }

    /// <summary>
    /// Normalizes a C++ type display name into a legal C# identifier for generated type names.
    /// </summary>
    /// <param name="typeName">The C++ type name.</param>
    /// <returns>The normalized C# type name.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="typeName"/> does not contain any identifier characters.</exception>
    private string NormalizeCSharpTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new InvalidOperationException("Field type name cannot be empty. Please check your arguments.");
        }

        var builder = ZString.CreateStringBuilder();
        var previousWasUnderscore = false;

        foreach (var character in typeName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasUnderscore = false;
                continue;
            }

            if (builder.Length > 0 && !previousWasUnderscore)
            {
                builder.Append('_');
                previousWasUnderscore = true;
            }
        }

        var normalizedTypeName = builder.ToString().Trim('_');

        if (normalizedTypeName.Length == 0)
        {
            throw new InvalidOperationException("Field type name cannot be empty. Please check your arguments.");
        }

        if (char.IsDigit(normalizedTypeName[0]))
        {
            return ZString.Concat("_", normalizedTypeName);
        }

        return normalizedTypeName;
    }

    /// <summary>
    /// Determines whether a C++ type name should be parsed by the generator.
    /// </summary>
    /// <param name="typeName">The C++ type name.</param>
    /// <returns><see langword="true"/> when the type should be parsed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="typeName"/> does not contain any non-whitespace characters.</exception>
    private bool ShouldParseTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new InvalidOperationException("Field type name cannot be empty. Please check your arguments.");
        }

        return !typeName.Contains("std::", StringComparison.Ordinal)
               && !typeName.Contains("NCollection_Allocator", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public ClangSharp.Type DesugarType(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.CanonicalType;
    }

    /// <inheritdoc/>
    public bool ShouldParse(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var typeName = GetCppName(type);
        return ShouldParseTypeName(typeName)
               && !generationOptions.Value.ShouldSkip(type);
    }
}
