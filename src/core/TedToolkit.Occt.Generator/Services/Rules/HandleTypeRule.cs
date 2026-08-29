// -----------------------------------------------------------------------
// <copyright file="HandleTypeRule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Services.Rules;

/// <summary>
/// Projects an OCCT intrusive smart-pointer value to the shared non-owning managed layout.
/// </summary>
internal sealed class HandleTypeRule : ITypeRule
{
    /// <inheritdoc/>
    public bool TryResolve(ClangSharp.Type type, out TypeResolveResult result)
    {
        ArgumentNullException.ThrowIfNull(type);
        var terminal = GetTerminalType(type);
        var record = terminal.AsCXXRecordDecl;
        if ((record?.Definition ?? record) is not ClassTemplateSpecializationDecl specialization
            || !IsHandleSpecialization(specialization)
            || !TryGetElementType(specialization, out var elementType))
        {
            result = null!;
            return false;
        }

        var elementName = elementType.AsString.ToGeneratedTypeName();
        var managedType = new DataType($"global::TedToolkit.Occt.handle<{elementName}>");
        result = new()
        {
            Decl = specialization,
            Type = new()
            {
                CppTypeName = type.AsString,
                CSharpPInvokeType = managedType,
                CSharpPublicType = managedType,
                IsOcctHandle = true,
                OcctHandleElementType = elementName,
                OcctHandleElementCppType = elementType.AsString,
            },
        };
        return true;
    }

    private static ClangSharp.Type GetTerminalType(ClangSharp.Type type)
    {
        var current = type.CanonicalType;
        while (current is PointerType or LValueReferenceType or RValueReferenceType)
        {
            current = current.PointeeType.CanonicalType;
        }

        return current;
    }

    private static bool IsHandleSpecialization(ClassTemplateSpecializationDecl record)
    {
        return record.TypeForDecl.AsString.StartsWith("opencascade::handle<", StringComparison.Ordinal)
               || record.TypeForDecl.AsString.StartsWith("occ::handle<", StringComparison.Ordinal);
    }

    private static bool TryGetElementType(
        ClassTemplateSpecializationDecl specialization,
        out ClangSharp.Type elementType)
    {
        foreach (var argument in specialization.TemplateArgs)
        {
            if (argument.Kind is CXTemplateArgumentKind.CXTemplateArgumentKind_Type)
            {
                elementType = argument.AsType;
                return true;
            }
        }

        elementType = null!;
        return false;
    }
}