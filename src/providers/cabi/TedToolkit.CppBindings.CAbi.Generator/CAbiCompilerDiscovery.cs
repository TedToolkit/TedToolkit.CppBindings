// -----------------------------------------------------------------------
// <copyright file="CAbiCompilerDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.CAbi.Generator;

/// <summary>
/// Discovers and projects external C linkage declarations from one selected header.
/// </summary>
internal static class CAbiCompilerDiscovery
{
    /// <summary>
    /// Projects the eligible declarations from the selected header.
    /// </summary>
    /// <param name="header">The absolute header path.</param>
    /// <param name="wrapperClassName">The managed containing type.</param>
    /// <param name="arguments">The native target and preprocessor arguments.</param>
    /// <param name="cancellationToken">The token that cancels discovery.</param>
    /// <returns>The eligible functions, once per C symbol.</returns>
    internal static IReadOnlyList<BindingFreeFunctionDefinition> Discover(
        string header,
        string wrapperClassName,
        string[] arguments,
        in CancellationToken cancellationToken)
    {
        using var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index, header, arguments, [], CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies);
        using var unit = TranslationUnit.GetOrCreate(translationUnit);
        ThrowForDiagnostics(translationUnit, header);

        var functions = new Dictionary<string, BindingFreeFunctionDefinition>(StringComparer.Ordinal);
        var pending = new Stack<Cursor>(unit.TranslationUnitDecl.CursorChildren.Reverse());
        while (pending.TryPop(out var cursor))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!cursor.Location.IsFromMainFile)
            {
                continue;
            }

            if (cursor.CursorKind is CXCursorKind.CXCursor_LinkageSpec or CXCursorKind.CXCursor_Namespace)
            {
                foreach (var child in cursor.CursorChildren.Reverse())
                {
                    pending.Push(child);
                }
            }
            else if (cursor is FunctionDecl { CursorKind: CXCursorKind.CXCursor_FunctionDecl, IsExternC: true, } function
                     && !functions.ContainsKey(function.Spelling)
                     && GetQualifiedName(function) is { } qualifiedName)
            {
                functions.Add(function.Spelling, Project(function, qualifiedName, wrapperClassName));
            }
        }

        return functions.Values.ToArray();
    }

    private static string? GetQualifiedName(FunctionDecl function)
    {
        var names = new Stack<string>();
        names.Push(function.Spelling);
        for (var parent = function.SemanticParentCursor;
             parent is not null && parent.CursorKind != CXCursorKind.CXCursor_TranslationUnit;
             parent = parent.SemanticParentCursor)
        {
            if (parent.CursorKind == CXCursorKind.CXCursor_LinkageSpec)
            {
                continue;
            }

            if (parent.CursorKind != CXCursorKind.CXCursor_Namespace || parent.Spelling.Length == 0)
            {
                return null;
            }

            names.Push(parent.Spelling);
        }

        return "::" + string.Join("::", names);
    }

    private static BindingFreeFunctionDefinition Project(
        FunctionDecl function,
        string qualifiedName,
        string wrapperClassName)
    {
        if (function.IsVariadic
            || function.Type.Handle.FunctionTypeCallingConv is not
                (CXCallingConv.CXCallingConv_C or CXCallingConv.CXCallingConv_Win64))
        {
            throw new NotSupportedException($"Free function '{qualifiedName}' must have a fixed Windows x64 C ABI.");
        }

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var parameters = function.Parameters.Select((parameter, index) =>
        {
            var name = parameter.Spelling;
            if (!IsManagedIdentifier(name) || !usedNames.Add(name))
            {
                name = "argument" + index;
                while (!usedNames.Add(name))
                {
                    name += "_";
                }
            }

            return new BindingFreeFunctionParameter(name, ProjectType(parameter.Type.Handle, qualifiedName));
        }).ToArray();
        var isNoexcept = function.ExceptionSpecType is
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_BasicNoexcept
            or CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_NoThrow
            or CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_DynamicNone;
        var methodName = IsManagedIdentifier(function.Spelling) ? function.Spelling : "_" + function.Spelling;
        return new(
            wrapperClassName,
            methodName,
            qualifiedName,
            ProjectType(function.ReturnType.Handle, qualifiedName),
            parameters,
            function.Spelling,
            isNoexcept);
    }

    /// <summary>
    /// Determines whether the supplied name can be emitted as a bare C# identifier.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns>Whether the candidate can be emitted unchanged.</returns>
    internal static bool IsManagedIdentifier(string value)
    {
        return SyntaxFacts.IsValidIdentifier(value)
            && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
    }

    private static BindingFreeFunctionType ProjectType(CXType type, string functionName)
    {
        type = type.CanonicalType;
        var isReference = type.kind == CXTypeKind.CXType_LValueReference;
        var value = isReference ? type.PointeeType.CanonicalType : type.UnqualifiedType;
        if (isReference && value.IsVolatileQualified)
        {
            throw UnsupportedType(type, functionName);
        }

        return new(
            value.Spelling.CString,
            ManagedType(value, functionName),
            isReference,
            isReference && value.IsConstQualified);
    }

    private static string ManagedType(CXType type, string functionName)
    {
        return type.kind switch
        {
            CXTypeKind.CXType_Void => "void",
            CXTypeKind.CXType_Bool => "bool",
            CXTypeKind.CXType_Char_S or CXTypeKind.CXType_SChar => "sbyte",
            CXTypeKind.CXType_Char_U or CXTypeKind.CXType_UChar => "byte",
            CXTypeKind.CXType_Short => "short",
            CXTypeKind.CXType_UShort or CXTypeKind.CXType_WChar => "ushort",
            CXTypeKind.CXType_Char16 => "char",
            CXTypeKind.CXType_Int or CXTypeKind.CXType_Long => "int",
            CXTypeKind.CXType_UInt or CXTypeKind.CXType_ULong or CXTypeKind.CXType_Char32 => "uint",
            CXTypeKind.CXType_LongLong => "long",
            CXTypeKind.CXType_ULongLong => "ulong",
            CXTypeKind.CXType_Float => "float",
            CXTypeKind.CXType_Double or CXTypeKind.CXType_LongDouble => "double",
            CXTypeKind.CXType_Enum => ManagedType(type.Declaration.EnumDecl_IntegerType.CanonicalType, functionName),
            CXTypeKind.CXType_Pointer when type.PointeeType.kind == CXTypeKind.CXType_Record => "void*",
            CXTypeKind.CXType_Pointer => ManagedType(type.PointeeType.CanonicalType, functionName) + "*",
            _ => throw UnsupportedType(type, functionName),
        };
    }

    private static NotSupportedException UnsupportedType(CXType type, string functionName)
    {
        return new($"Free function '{functionName}' uses unsupported type '{type.Spelling.CString}'.");
    }

    private static void ThrowForDiagnostics(CXTranslationUnit unit, string header)
    {
        var errors = new List<string>();
        for (uint index = 0; index < unit.NumDiagnostics; index++)
        {
            using var diagnostic = unit.GetDiagnostic(index);
            if (diagnostic.Severity is CXDiagnosticSeverity.CXDiagnostic_Error or CXDiagnosticSeverity.CXDiagnostic_Fatal)
            {
                errors.Add(diagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString());
            }
        }

        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException($"Clang could not parse '{header}'.\n" + string.Join("\n", errors));
    }
}