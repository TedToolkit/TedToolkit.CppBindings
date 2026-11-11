// -----------------------------------------------------------------------
// <copyright file="CgalCompilerDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

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
    /// <param name="profile">The closed profile whose projections must compile.</param>
    /// <param name="commandLineArgs">Additional arguments for the target compiler.</param>
    /// <returns>The deterministic compiler declaration inventory.</returns>
    internal static IReadOnlyList<CgalCompilerDeclaration> Discover(
        string includeRoot,
        IReadOnlyList<string> selectedHeaders,
        IReadOnlySet<string> reachableHeaders,
        CgalProfileManifest profile,
        IReadOnlyList<string> commandLineArgs)
    {
        var relay = string.Join(
            Environment.NewLine,
            selectedHeaders.Order(StringComparer.Ordinal).Select(static header => $"#include <{header}>"))
            + Environment.NewLine
            + CgalSemanticCatalog.RenderCompilerProbe(profile);
        using var unsaved = CXUnsavedFile.Create(RelayFileName, relay);
        using var index = CXIndex.Create();
        string[] arguments =
        [
            "-std=c++20",
            "-x",
            "c++",
            "-DCGAL_DEBUG",
            "-D_SILENCE_CXX17_CODECVT_HEADER_DEPRECATION_WARNING",
            "-I" + includeRoot,
            .. commandLineArgs,
        ];
        var translationUnit = CXTranslationUnit.Parse(
            index,
            RelayFileName,
            arguments,
            [unsaved,],
            CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies);
        using var unit = TranslationUnit.GetOrCreate(translationUnit);
        ThrowForDiagnostics(ref translationUnit);

        var declarations = new Dictionary<string, CgalCompilerDeclaration>(StringComparer.Ordinal);
        Visit(unit.TranslationUnitDecl.Handle, includeRoot, reachableHeaders, declarations);
        return Array.AsReadOnly(declarations.Values
            .OrderBy(static item => item.Header, StringComparer.Ordinal)
            .ThenBy(static item => item.Signature, StringComparer.Ordinal)
            .ThenBy(static item => item.Identity, StringComparer.Ordinal)
            .ToArray());
    }

    private static unsafe void Visit(
        CXCursor cursor,
        string includeRoot,
        IReadOnlySet<string> reachableHeaders,
        Dictionary<string, CgalCompilerDeclaration> declarations)
    {
        // Keep discovery on libclang cursors. ClangSharp can reject a native UsingEnumDecl
        // while constructing managed children, even when that declaration is outside the CGAL profile.
        var children = new List<CXCursor>();
        cursor.VisitChildren((child, _, _) =>
        {
            children.Add(child);
            return CXChildVisitResult.CXChildVisit_Recurse;
        }, default);

        foreach (var child in children)
        {
            if (TryCreate(child, includeRoot, reachableHeaders, out var declaration))
            {
                declarations.TryAdd(declaration.Identity, declaration);
            }
        }
    }

    private static bool TryCreate(
        CXCursor cursor,
        string includeRoot,
        IReadOnlySet<string> reachableHeaders,
        out CgalCompilerDeclaration declaration)
    {
        declaration = null!;
        if (!IsDeclarationKind(cursor.kind)
            || !TryGetHeader(cursor, includeRoot, out var header)
            || !reachableHeaders.Contains(header)
            || !IsPublic(cursor))
        {
            return false;
        }

        using var cursorSpelling = cursor.Spelling;
        var spelling = cursorSpelling.CString;
        if (string.IsNullOrWhiteSpace(spelling))
        {
            return false;
        }

        using var cursorUsr = clang.getCursorUSR(cursor);
        using var cursorType = clang.getCursorType(cursor).Spelling;
        using var kind = cursor.KindSpelling;
        var usr = cursorUsr.CString;
        var type = cursorType.CString;
        var signature = GetQualifiedName(cursor) + (string.IsNullOrWhiteSpace(type) ? "" : " : " + type);
        var sourceIdentity = string.IsNullOrWhiteSpace(usr)
            ? header + "|" + cursor.kind + "|" + signature
            : usr;
        declaration = new(
            "clang-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceIdentity))),
            signature,
            header,
            kind.CString,
            spelling,
            IsDirectNonTemplate(cursor));
        return true;
    }

    private static bool IsDirectNonTemplate(CXCursor cursor)
    {
        if (cursor.kind == CXCursorKind.CXCursor_Namespace || IsOpenTemplateKind(cursor.kind))
        {
            return false;
        }

        var current = cursor.SemanticParent;
        while (!current.IsNull && current.kind != CXCursorKind.CXCursor_TranslationUnit)
        {
            if (IsOpenTemplateKind(current.kind))
            {
                return false;
            }

            current = current.SemanticParent;
        }

        return true;
    }

    private static bool IsOpenTemplateKind(CXCursorKind kind)
    {
        return kind is CXCursorKind.CXCursor_TemplateTypeParameter
            or CXCursorKind.CXCursor_NonTypeTemplateParameter
            or CXCursorKind.CXCursor_TemplateTemplateParameter
            or CXCursorKind.CXCursor_FunctionTemplate
            or CXCursorKind.CXCursor_ClassTemplate
            or CXCursorKind.CXCursor_ClassTemplatePartialSpecialization
            or CXCursorKind.CXCursor_TypeAliasTemplateDecl;
    }

    private static string GetQualifiedName(CXCursor cursor)
    {
        var names = new Stack<string>();
        var current = cursor;
        while (!current.IsNull && current.kind != CXCursorKind.CXCursor_TranslationUnit)
        {
            using var name = current.Spelling;
            var spelling = name.CString;
            if (!string.IsNullOrWhiteSpace(spelling))
            {
                names.Push(spelling);
            }

            current = current.SemanticParent;
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
            or CXCursorKind.CXCursor_TypeAliasDecl
            or CXCursorKind.CXCursor_TypeAliasTemplateDecl;
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
        using var name = file.Name;
        var path = name.CString;
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