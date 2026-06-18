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

    public TypeModel Resolve(ClangSharp.Type type, out CXXRecordDecl? decl)
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var typeRule in typeRules)
        {
            if (typeRule.TryResolve(type, out var model, out decl))
            {
                return model;
            }
        }

        throw new InvalidOperationException($"Could not resolve type ({type.AsString})");
    }
}
