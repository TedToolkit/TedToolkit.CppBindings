// -----------------------------------------------------------------------
// <copyright file="HeaderCompilerDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Discovers external C linkage declarations from the public headers of one compiler input.
/// </summary>
internal static class HeaderCompilerDiscovery
{
    /// <summary>
    /// Projects the eligible declarations from the selected headers.
    /// </summary>
    /// <param name="headers">The absolute header paths and managed wrapper names.</param>
    /// <param name="includes">The public header includes compiled together.</param>
    /// <param name="arguments">The native target and preprocessor arguments.</param>
    /// <param name="records">The shared record discovery and reuse context.</param>
    /// <param name="cancellationToken">The token that cancels discovery.</param>
    /// <returns>The eligible functions, once per C symbol.</returns>
    internal static Dictionary<string, BindingFreeFunctionDefinition> Discover(
        IReadOnlyDictionary<string, string> headers,
        string includes,
        string[] arguments,
        HeaderRecordDiscovery records,
        in CancellationToken cancellationToken)
    {
        const string InputName = "TedToolkitHeaderBindings.cpp";
        using var input = CXUnsavedFile.Create(InputName, includes);
        using var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index, InputName, arguments, [input,], CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies);
        using var unit = TranslationUnit.GetOrCreate(translationUnit);
        ThrowForDiagnostics(translationUnit);

        var functions = new Dictionary<string, BindingFreeFunctionDefinition>(StringComparer.Ordinal);
        var pending = new Stack<Cursor>(unit.TranslationUnitDecl.CursorChildren.Reverse());
        while (pending.TryPop(out var cursor))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cursor.CursorKind is CXCursorKind.CXCursor_LinkageSpec or CXCursorKind.CXCursor_Namespace)
            {
                foreach (var child in cursor.CursorChildren.Reverse())
                {
                    pending.Push(child);
                }
            }
            else if (cursor is FunctionDecl { CursorKind: CXCursorKind.CXCursor_FunctionDecl, IsExternC: true, } function
                     && !functions.ContainsKey(function.Spelling)
                     && GetHeader(function) is { } header
                     && headers.TryGetValue(header, out var wrapper)
                     && GetQualifiedName(function) is { } qualifiedName)
            {
                functions.Add(function.Spelling, Project(function, qualifiedName, wrapper, records));
            }
        }

        return functions;
    }

    private static string? GetHeader(FunctionDecl function)
    {
        function.Location.GetFileLocation(out var file, out _, out _, out _);
        var path = file.Name.CString;
        return string.IsNullOrEmpty(path) ? null : Path.GetFullPath(path);
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
        string wrapperClassName,
        HeaderRecordDiscovery records)
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

            return (name, ProjectType(parameter.Type, qualifiedName, records));
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
            ProjectType(function.ReturnType, qualifiedName, records),
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

    /// <summary>
    /// Projects a compiler type using the same record catalog as the managed layout emitter.
    /// </summary>
    /// <param name="type">The compiler type.</param>
    /// <param name="context">The source declaration used for diagnostics.</param>
    /// <param name="records">The shared record discovery context.</param>
    /// <returns>The callable and field type projection.</returns>
    /// <exception cref="NotSupportedException">The type has no supported value or reference transport.</exception>
    internal static BindingFreeFunctionType ProjectType(ClangSharp.Type type, string context, HeaderRecordDiscovery records)
    {
        type = type.CanonicalType;
        var isReference = type is LValueReferenceType;
        var value = isReference ? type.PointeeType.CanonicalType : type;
        var native = isReference ? value.Handle : value.Handle.UnqualifiedType;
        if (isReference && native.IsVolatileQualified)
        {
            throw UnsupportedType(type.Handle, context);
        }

        var record = value is RecordType nativeRecord ? records.Resolve(nativeRecord, context) : null;
        if (!isReference && record is not null && (record.ObjectKind != NativeObjectKind.Value || native.SizeOf <= 0))
        {
            throw new NotSupportedException($"'{context}' requires a complete copyable value layout for '{native.Spelling.CString}'.");
        }

        return new(
            native.Spelling.CString,
            record?.Type.CSharpTypeName ?? ManagedType(value, context, records),
            isReference,
            isReference && native.IsConstQualified) { IsRecordValue = !isReference && record is not null, };
    }

    private static string ManagedType(ClangSharp.Type type, string context, HeaderRecordDiscovery records)
    {
        return type switch
        {
            RecordType record => records.Resolve(record, context).Type.CSharpTypeName,
            PointerType pointer => ManagedType(pointer.PointeeType.CanonicalType, context, records) + "*",
            _ => ManagedScalar(type.Handle, context),
        };
    }

    private static string ManagedScalar(CXType type, string functionName)
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
            CXTypeKind.CXType_Enum => ManagedScalar(type.Declaration.EnumDecl_IntegerType.CanonicalType, functionName),
            _ => throw UnsupportedType(type, functionName),
        };
    }

    private static NotSupportedException UnsupportedType(CXType type, string functionName)
    {
        return new($"'{functionName}' uses unsupported type '{type.Spelling.CString}'.");
    }

    private static void ThrowForDiagnostics(CXTranslationUnit unit)
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

        throw new InvalidOperationException("Clang could not parse the public binding headers.\n" + string.Join("\n", errors));
    }
}