// -----------------------------------------------------------------------
// <copyright file="TypeResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;


namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Resolves clang types into projection models by applying ordered rules.
/// </summary>
public sealed class Resolver(IEnumerable<ITypeRule> typeRules) : IResolver
{
    public TypeResolveResult Resolve(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var canonicalType = type.CanonicalType;
        foreach (var typeRule in typeRules)
        {
            if (typeRule.TryResolve(canonicalType, out var result))
            {
                return result;
            }
        }

        throw new InvalidOperationException($"Could not resolve type ({canonicalType.AsString})");
    }
}
