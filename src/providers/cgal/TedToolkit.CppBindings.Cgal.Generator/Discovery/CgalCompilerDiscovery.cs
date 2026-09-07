// -----------------------------------------------------------------------
// <copyright file="CgalCompilerDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

using ClangSharp;
using ClangSharp.Interop;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Uses Clang to enumerate the public declarations in one finite CGAL header closure.
/// </summary>
internal static class CgalCompilerDiscovery
{
    private const string RelayFileName = "tedtoolkit-cgal-profile.cpp";

    /// <summary>
    /// Parses the selected headers and returns every public declaration originating in their closure.
    /// </summary>
    /// <param name="includeRoot">The vcpkg include root.</param>
    /// <param name="selectedHeaders">The finite profile roots.</param>
    /// <param name="reachableHeaders">The recursively closed CGAL header set.</param>
    /// <returns>The deterministic compiler declaration inventory.</returns>
    internal static IReadOnlyList<CgalCompilerDeclaration> Discover(
        string includeRoot,
        IReadOnlyList<string> selectedHeaders,
        IReadOnlySet<string> reachableHeaders)
    {
        var relay = string.Join(
            Environment.NewLine,
            selectedHeaders.Order(StringComparer.Ordinal).Select(static header => $"#include <{header}>"));
        using var unsaved = CXUnsavedFile.Create(RelayFileName, relay);
        using var index = CXIndex.Create();
        var arguments = new string[]
        {
            "-std=c++20",
            "-x",
            "c++",
            "-DCGAL_DEBUG",
            "-D_SILENCE_CXX17_CODECVT_HEADER_DEPRECATION_WARNING",
            "-I" + includeRoot,
        };
        var translationUnit = CXTranslationUnit.Parse(
            index,
            RelayFileName,
            arguments,
            [unsaved,],
            CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies);
        using var unit = TranslationUnit.GetOrCreate(translationUnit);
        ThrowForDiagnostics(ref translationUnit);

        var declarations = new Dictionary<string, CgalCompilerDeclaration>(StringComparer.Ordinal);
        Visit(unit.TranslationUnitDecl, includeRoot, reachableHeaders, declarations);
        return Array.AsReadOnly(declarations.Values
            .OrderBy(static item => item.Header, StringComparer.Ordinal)
            .ThenBy(static item => item.Signature, StringComparer.Ordinal)
            .ThenBy(static item => item.Identity, StringComparer.Ordinal)
            .ToArray());
    }

    private static void Visit(
        Cursor cursor,
        string includeRoot,
        IReadOnlySet<string> reachableHeaders,
        Dictionary<string, CgalCompilerDeclaration> declarations)
    {
        var pending = new Stack<Cursor>();
        for (var index = cursor.CursorChildren.Count - 1; index >= 0; index--)
        {
            pending.Push(cursor.CursorChildren[index]);
        }

        while (pending.TryPop(out var child))
        {
            if (TryCreate(child, includeRoot, reachableHeaders, out var declaration))
            {
                declarations.TryAdd(declaration.Identity, declaration);
            }

            for (var index = child.CursorChildren.Count - 1; index >= 0; index--)
            {
                pending.Push(child.CursorChildren[index]);
            }
        }
    }

    private static bool TryCreate(
        Cursor cursor,
        string includeRoot,
        IReadOnlySet<string> reachableHeaders,
        out CgalCompilerDeclaration declaration)
    {
        declaration = null!;
        if (!IsDeclarationKind(cursor.CursorKind)
            || string.IsNullOrWhiteSpace(cursor.Spelling)
            || !TryGetHeader(cursor.Handle, includeRoot, out var header)
            || !reachableHeaders.Contains(header)
            || !IsPublic(cursor.Handle))
        {
            return false;
        }

        var usr = clang.getCursorUSR(cursor.Handle).CString;
        var type = clang.getCursorType(cursor.Handle).Spelling.CString;
        var signature = GetQualifiedName(cursor) + (string.IsNullOrWhiteSpace(type) ? "" : " : " + type);
        var sourceIdentity = string.IsNullOrWhiteSpace(usr)
            ? header + "|" + cursor.CursorKind + "|" + signature
            : usr;
        declaration = new(
            "clang-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceIdentity))),
            signature,
            header,
            cursor.CursorKindSpelling,
            cursor.Spelling);
        return true;
    }

    private static string GetQualifiedName(Cursor cursor)
    {
        var names = new Stack<string>();
        Cursor? current = cursor;
        while (current is not null && current.CursorKind != CXCursorKind.CXCursor_TranslationUnit)
        {
            if (!string.IsNullOrWhiteSpace(current.Spelling))
            {
                names.Push(current.Spelling);
            }

            current = current.SemanticParentCursor;
        }

        return string.Join("::", names);
    }

    private static bool IsDeclarationKind(CXCursorKind kind)
    {
        return kind is CXCursorKind.CXCursor_StructDecl
            or CXCursorKind.CXCursor_UnionDecl
            or CXCursorKind.CXCursor_ClassDecl
            or CXCursorKind.CXCursor_EnumDecl
            or CXCursorKind.CXCursor_FieldDecl
            or CXCursorKind.CXCursor_FunctionDecl
            or CXCursorKind.CXCursor_VarDecl
            or CXCursorKind.CXCursor_TypedefDecl
            or CXCursorKind.CXCursor_CXXMethod
            or CXCursorKind.CXCursor_Namespace
            or CXCursorKind.CXCursor_Constructor
            or CXCursorKind.CXCursor_Destructor
            or CXCursorKind.CXCursor_ConversionFunction
            or CXCursorKind.CXCursor_TemplateTypeParameter
            or CXCursorKind.CXCursor_NonTypeTemplateParameter
            or CXCursorKind.CXCursor_TemplateTemplateParameter
            or CXCursorKind.CXCursor_FunctionTemplate
            or CXCursorKind.CXCursor_ClassTemplate
            or CXCursorKind.CXCursor_ClassTemplatePartialSpecialization
            or CXCursorKind.CXCursor_NamespaceAlias
            or CXCursorKind.CXCursor_UsingDirective
            or CXCursorKind.CXCursor_UsingDeclaration
            or CXCursorKind.CXCursor_TypeAliasDecl;
    }

    private static bool IsPublic(CXCursor cursor)
    {
        var current = cursor;
        while (current.kind != CXCursorKind.CXCursor_TranslationUnit
               && current.kind != CXCursorKind.CXCursor_NoDeclFound)
        {
            if (clang.getCXXAccessSpecifier(current) is
                CX_CXXAccessSpecifier.CX_CXXPrivate or CX_CXXAccessSpecifier.CX_CXXProtected)
            {
                return false;
            }

            var parent = clang.getCursorSemanticParent(current);
            if (parent == current)
            {
                break;
            }

            current = parent;
        }

        return true;
    }

    private static bool TryGetHeader(CXCursor cursor, string includeRoot, out string header)
    {
        clang.getCursorLocation(cursor).GetFileLocation(out var file, out _, out _, out _);
        var path = file.Name.CString;
        if (string.IsNullOrWhiteSpace(path))
        {
            header = "";
            return false;
        }

        var relative = Path.GetRelativePath(includeRoot, path).Replace('\\', '/');
        if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            header = "";
            return false;
        }

        header = relative;
        return true;
    }

    private static void ThrowForDiagnostics(ref CXTranslationUnit translationUnit)
    {
        var errors = new List<string>();
        for (uint index = 0; index < translationUnit.NumDiagnostics; index++)
        {
            using var diagnostic = translationUnit.GetDiagnostic(index);
            if (diagnostic.Severity is CXDiagnosticSeverity.CXDiagnostic_Error
                or CXDiagnosticSeverity.CXDiagnostic_Fatal)
            {
                errors.Add(diagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString());
            }
        }

        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Clang could not parse the finite CGAL profile." + Environment.NewLine
            + string.Join(Environment.NewLine, errors));
    }
}