// -----------------------------------------------------------------------
// <copyright file="Utf8StringTypeRule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Models.Types;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Services.Rules;

/// <summary>
/// Resolves native UTF-8 string pointer types into managed span projections.
/// </summary>
internal sealed class Utf8StringTypeRule : ITypeRule
{
    private static readonly DataType _readOnlyUtf8SpanType = DataType.FromType(typeof(ReadOnlySpan<byte>));

    /// <inheritdoc/>
    public bool TryResolve(ClangSharp.Type type, out TypeResolveResult result)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!IsConstCharPointer(type))
        {
            result = null!;
            return false;
        }

        result = new()
        {
            Decl = null,
            Type = new()
            {
                CppTypeName = type.AsString,
                CSharpPInvokeType = DataType.Byte.Pointer,
                CSharpPublicType = _readOnlyUtf8SpanType,
            },
        };

        return true;
    }

    private static bool IsConstCharPointer(ClangSharp.Type type)
    {
        // Resolver already feeds canonical types into rules, so OCCT aliases like
        // Standard_CString have already been desugared here. That means we can
        // identify UTF-8 input strings by their structural clang shape instead of
        // hard-coding typedef names:
        //
        //   Standard_CString -> const char*
        //   const char*      -> const-qualified char pointee behind a pointer
        if (!TryGetPointerPointee(type, out var pointeeType))
        {
            return false;
        }

        if (!pointeeType.IsLocalConstQualified)
        {
            return false;
        }

        return IsCharLikeBuiltin(pointeeType.Desugar);
    }

    private static bool TryGetPointerPointee(ClangSharp.Type type, out ClangSharp.Type pointeeType)
    {
        if (type is PointerType pointerType)
        {
            pointeeType = pointerType.PointeeType;
            return true;
        }

        pointeeType = null!;
        return false;
    }

    private static bool IsCharLikeBuiltin(ClangSharp.Type type)
    {
        return type is BuiltinType builtinType
               && builtinType.Kind is CXTypeKind.CXType_Char_S
                   or CXTypeKind.CXType_SChar
                   or CXTypeKind.CXType_Char_U
                   or CXTypeKind.CXType_UChar;
    }
}