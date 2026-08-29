// -----------------------------------------------------------------------
// <copyright file="RecordModelManager.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Builds and caches projected record and enum models from parsed Clang declarations.
/// </summary>
/// <param name="options">The generation options.</param>
/// <param name="resolver">The type resolver.</param>
/// <param name="defaultsResolver">The default triplet resolver.</param>
/// <param name="vcpkgEnvironment">The vcpkg environment service.</param>
internal sealed class RecordModelManager(
    IOptions<GenerationOptions> options,
    IResolver resolver,
    IVcpkgDefaultTripletResolver defaultsResolver,
    IVcpkgEnvironment vcpkgEnvironment) : IRecordModelManager
{
    private static readonly Regex NativeIdentifierRegex = new("[A-Za-z_][A-Za-z0-9_]*");

    private readonly List<EnumModel> _enumModels = [];

    private readonly HashSet<CXCursor> _enumNames = [];

    private readonly Dictionary<CXCursor, RecordModel> _recordNames = [];

    /// <inheritdoc/>
    public IReadOnlyList<EnumModel> EnumModels
    {
        get
        {
            return _enumModels;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<RecordModel> RecordModels
    {
        get
        {
            ShareHeaderRequirements();
            return _recordNames.Values.Where(t => t.IsPubliclyAccessible
                                                   && t.IsClosedTemplateSpecialization
                                                   && !IsAnonymousTypeName(t.Type.CppTypeName));
        }
    }

    private void ShareHeaderRequirements()
    {
        foreach (var models in _recordNames.Values.GroupBy(static model => model.SourceHeader, StringComparer.Ordinal))
        {
            var requiredHeaders = models
                .SelectMany(static model => model.NativeRequiredHeaders)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            foreach (var model in models)
            {
                model.NativeRequiredHeaders = requiredHeaders;
            }
        }
    }

    /// <inheritdoc/>
    public RecordModel Add(CXXRecordDecl record)
    {
        record = UnwrapRecord(record.Definition!);

        var key = record.CanonicalDecl.Handle;
        if (_recordNames.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var commentProjection = record.ToCommentProjection();
        var type = record.TypeForDecl.Handle;
        type = clang.getCanonicalType(type);
        var size = clang.Type_getSizeOf(type);
        if (size < 0)
        {
            throw new NotSupportedException($"Can't get size of type ({record.TypeForDecl.AsString})");
        }

        record.Location.GetFileLocation(out var file, out _, out _, out _);
        var result = new RecordModel()
        {
            IsPubliclyAccessible = IsPubliclyAccessible(record),
            IsClosedTemplateSpecialization = IsClosedTemplateSpecialization(record),
            DescriptionItems = commentProjection.DescriptionItems,
            SourceHeader = Path.GetFileName(file.Name.CString),
            Type = ApplyRequiredHeaders(
                resolver.Resolve(record.TypeForDecl)
                    .Type,
                record.TypeForDecl),
            Size = size,
            IsAbstract = record.IsAbstract,
            IsStandardTransient = false,
        };
        _recordNames.Add(key, result);
        var triplet = options.Value.GetTriplet(defaultsResolver);
        var isOcctType =
            file.Name.CString.Contains(vcpkgEnvironment.GetOcctIncludeFolder(triplet), StringComparison.InvariantCulture);

        result.FieldModels = record.Fields
            .Where(f => isOcctType || f.Access is CX_CXXAccessSpecifier.CX_CXXPublic)
            .Where(options.Value.FieldTypeToGenerate)
            .Where(static f => IsDefined(f.Type))
            .Select(ToModel)
            .ToArray();

        result.MethodModels = record.Methods
            .Where(m => ShouldIncludeMethod(m, record.IsAbstract))
            .GroupBy(GetMethodSignatureKey)
            .Select(static methods => methods
                .OrderBy(GetConstQualificationWeight)
                .First())
            .Select(ToModel)
            .ToArray();

        result.NativeRequiredHeaders = GetNativeRequiredHeaders(record);
        result.NativeDependencyRecords = GetNativeDependencyRecords(record);
        result.UsesAllocatorPlacementNew = record.Methods.Any(static method =>
            method.Name is "operator new" && method.Parameters.Count is 2);

        result.Bases = isOcctType
            ? record.Bases
                .Select(baseSpecifier => CreateBaseRelation(record, baseSpecifier))
                .ToArray()
            : [];
        result.IsStandardTransient = DerivesFromStandardTransient(record, []);
        NativeExportNameBuilder.Assign(result);

        return result;
    }

    private RecordModel[] GetNativeDependencyRecords(CXXRecordDecl record)
    {
        var dependencies = new HashSet<RecordModel>();
        var visitedTypes = new HashSet<CXType>();
        if (record is ClassTemplateSpecializationDecl specialization)
        {
            foreach (var argument in specialization.TemplateArgs)
            {
                if (argument.Kind is CXTemplateArgumentKind.CXTemplateArgumentKind_Type)
                {
                    AddNativeDependency(argument.AsType, dependencies, visitedTypes);
                }
            }
        }

        foreach (var field in record.Fields)
        {
            AddNativeDependency(field.Type, dependencies, visitedTypes);
        }

        foreach (var method in record.Methods)
        {
            AddNativeDependency(method.ReturnType, dependencies, visitedTypes);
            foreach (var parameter in method.Parameters)
            {
                AddNativeDependency(parameter.Type, dependencies, visitedTypes);
            }
        }

        return dependencies
            .Where(dependency => !ReferenceEquals(dependency, _recordNames[record.CanonicalDecl.Handle]))
            .OrderBy(static dependency => dependency.SourceHeader, StringComparer.Ordinal)
            .ToArray();
    }

    private void AddNativeDependency(
        ClangSharp.Type type,
        HashSet<RecordModel> dependencies,
        HashSet<CXType> visitedTypes)
    {
        var terminalType = type.CanonicalType;
        if (!visitedTypes.Add(terminalType.Handle))
        {
            return;
        }

        while (terminalType is PointerType or LValueReferenceType or RValueReferenceType)
        {
            terminalType = terminalType.PointeeType.CanonicalType;
            if (!visitedTypes.Add(terminalType.Handle))
            {
                return;
            }
        }

        if (terminalType.AsCXXRecordDecl?.Definition is not { } dependency)
        {
            return;
        }

        if (dependency is ClassTemplateSpecializationDecl specialization)
        {
            foreach (var argument in specialization.TemplateArgs)
            {
                if (argument.Kind is CXTemplateArgumentKind.CXTemplateArgumentKind_Type)
                {
                    AddNativeDependency(argument.AsType, dependencies, visitedTypes);
                }
            }
        }

        if (TryUnwrapRecord(dependency) is not { } unwrappedDependency
            || !IsPubliclyAccessible(unwrappedDependency))
        {
            return;
        }

        _ = dependencies.Add(Add(unwrappedDependency));
    }

    private static bool IsClosedTemplateSpecialization(CXXRecordDecl record)
    {
        if (record is not ClassTemplateSpecializationDecl specialization)
        {
            return true;
        }

        return specialization.TemplateArgs.All(static argument =>
            argument.Kind is not CXTemplateArgumentKind.CXTemplateArgumentKind_Type
            || argument.AsType.CanonicalType.Kind is not CXTypeKind.CXType_Void);
    }

    private string[] GetNativeRequiredHeaders(CXXRecordDecl record)
    {
        var headers = new HashSet<string>(StringComparer.Ordinal);
        AddMacroPrerequisiteHeaders(record, headers);
        AddNamedOcctHeaders(record.TypeForDecl.AsString, headers);
        if (record is ClassTemplateSpecializationDecl specialization)
        {
            foreach (var argument in specialization.TemplateArgs)
            {
                CollectRequiredHeaders(argument, headers, [], []);
            }
        }

        foreach (var field in record.Fields)
        {
            headers.UnionWith(GetRequiredHeaders(field.Type));
        }

        foreach (var method in record.Methods)
        {
            headers.UnionWith(GetRequiredHeaders(method.ReturnType));
            foreach (var parameter in method.Parameters)
            {
                headers.UnionWith(GetRequiredHeaders(parameter.Type));
            }
        }

        return headers.Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddMacroPrerequisiteHeaders(CXXRecordDecl record, HashSet<string> headers)
    {
        record.Location.GetFileLocation(out var file, out _, out _, out _);
        var sourceHeader = file.Name.CString;
        if (!File.Exists(sourceHeader)
            || !File.ReadAllText(sourceHeader).Contains("OCCT_DUMP_", StringComparison.Ordinal))
        {
            return;
        }

        _ = headers.Add("Standard_Dump.hxx");
    }

    private void AddNamedOcctHeaders(string nativeTypeName, HashSet<string> headers)
    {
        var triplet = options.Value.GetTriplet(defaultsResolver);
        var includeFolder = vcpkgEnvironment.GetOcctIncludeFolder(triplet);
        foreach (Match match in NativeIdentifierRegex.Matches(nativeTypeName))
        {
            var header = match.Value + ".hxx";
            if (File.Exists(Path.Combine(includeFolder, header)))
            {
                _ = headers.Add(header);
            }
        }
    }

    private static bool IsPubliclyAccessible(CXXRecordDecl record)
    {
        return IsPubliclyAccessible(record, []);
    }

    private static bool IsPubliclyAccessible(CXXRecordDecl record, HashSet<CXCursor> visited)
    {
        if (!visited.Add(record.CanonicalDecl.Handle))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(record.Name)
            || record.TypeForDecl.AsString.Contains("(unnamed", StringComparison.Ordinal))
        {
            return false;
        }

        var cursor = record.Handle;
        while (true)
        {
            var parent = clang.getCursorSemanticParent(cursor);
            if (parent.kind is CXCursorKind.CXCursor_TranslationUnit or CXCursorKind.CXCursor_Namespace)
            {
                break;
            }

            if (parent.kind is not (CXCursorKind.CXCursor_StructDecl
                or CXCursorKind.CXCursor_ClassDecl
                or CXCursorKind.CXCursor_ClassTemplate
                or CXCursorKind.CXCursor_ClassTemplatePartialSpecialization)
                || !HasPublicAccess(cursor))
            {
                return false;
            }

            cursor = parent;
        }

        if (record is not ClassTemplateSpecializationDecl specialization)
        {
            return true;
        }

        foreach (var argument in specialization.TemplateArgs)
        {
            if (argument.Kind is not CXTemplateArgumentKind.CXTemplateArgumentKind_Type)
            {
                continue;
            }

            var argumentType = argument.AsType.GetAddingType();
            if (argumentType?.AsCXXRecordDecl is { } argumentRecord
                && !IsPubliclyAccessible(argumentRecord.Definition ?? argumentRecord, visited))
            {
                return false;
            }

            if (argumentType is not null
                && TryGetEnumDecl(argumentType, out var argumentEnum)
                && !IsPubliclyAccessible(argumentEnum))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPubliclyAccessible(EnumDecl declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration.Name)
            || IsAnonymousTypeName(declaration.TypeForDecl.AsString))
        {
            return false;
        }

        var cursor = declaration.Handle;
        while (true)
        {
            var parent = clang.getCursorSemanticParent(cursor);
            if (parent.kind is CXCursorKind.CXCursor_TranslationUnit or CXCursorKind.CXCursor_Namespace)
            {
                return true;
            }

            if (parent.kind is not (CXCursorKind.CXCursor_StructDecl
                or CXCursorKind.CXCursor_ClassDecl
                or CXCursorKind.CXCursor_ClassTemplate
                or CXCursorKind.CXCursor_ClassTemplatePartialSpecialization)
                || !HasPublicAccess(cursor))
            {
                return false;
            }

            cursor = parent;
        }
    }

    private static bool HasPublicAccess(CXCursor cursor)
    {
        if (clang.getCXXAccessSpecifier(cursor) is CX_CXXAccessSpecifier.CX_CXXPublic)
        {
            return true;
        }

        var template = clang.getSpecializedCursorTemplate(cursor);
        return clang.getCXXAccessSpecifier(template) is CX_CXXAccessSpecifier.CX_CXXPublic;
    }

    private static bool DerivesFromStandardTransient(
        CXXRecordDecl record,
        HashSet<CXCursor> visited)
    {
        record = record.Definition ?? record;
        if (!visited.Add(record.CanonicalDecl.Handle))
        {
            return false;
        }

        if (record.TypeForDecl.CanonicalType.AsString is "Standard_Transient")
        {
            return true;
        }

        return record.Bases.Any(baseSpecifier =>
            baseSpecifier.Type.AsCXXRecordDecl is { } baseRecord
            && DerivesFromStandardTransient(baseRecord, visited));
    }

    private BaseRelationModel CreateBaseRelation(CXXRecordDecl record, CXXBaseSpecifier baseSpecifier)
    {
        var baseRecord = baseSpecifier.Type.AsCXXRecordDecl?.Definition
                         ?? throw new NotSupportedException(
                             $"Can't resolve base of '{record.TypeForDecl.AsString}'.");
        return new()
        {
            Base = Add(baseRecord),
            IsVirtual = baseSpecifier.IsVirtual,
            IsPublic = clang.getCXXAccessSpecifier(baseSpecifier.Handle)
                is CX_CXXAccessSpecifier.CX_CXXPublic,
            PointerAdjustment = GetPointerAdjustmentKind(
                record.Bases.Count,
                baseSpecifier.IsVirtual,
                record.NumVBases),
        };
    }

    /// <summary>
    /// Classifies whether a direct base conversion is address preserving.
    /// </summary>
    /// <param name="directBaseCount">The number of direct bases.</param>
    /// <param name="isVirtual">Whether this direct base is virtual.</param>
    /// <param name="virtualBaseCount">The number of virtual bases in the record.</param>
    /// <returns>The required pointer adjustment strategy.</returns>
    internal static PointerAdjustmentKind GetPointerAdjustmentKind(
        int directBaseCount,
        bool isVirtual,
        uint virtualBaseCount)
    {
        return directBaseCount is 1 && !isVirtual && virtualBaseCount is 0
            ? PointerAdjustmentKind.Identity
            : PointerAdjustmentKind.NativeAdjust;
    }

    private TypeModel ToModel(ClangSharp.Type type)
    {
        var result = resolver.Resolve(type);

        if (result.Decl is { } recordDecl)
        {
            Add(recordDecl);
        }

        if (result.Enum is not null)
        {
            Add((EnumDecl)result.Enum);
        }

        return ApplyRequiredHeaders(result.Type, type);
    }

    private static TypeModel ApplyRequiredHeaders(TypeModel typeModel, ClangSharp.Type type)
    {
        typeModel.RequiredHeaders = GetRequiredHeaders(type);
        return typeModel;
    }

    private static IReadOnlyList<string> GetRequiredHeaders(ClangSharp.Type type)
    {
        var headers = new HashSet<string>(StringComparer.Ordinal);
        var visitedTypes = new HashSet<CXType>();
        var visitedDecls = new HashSet<CXCursor>();

        CollectRequiredHeaders(type, headers, visitedTypes, visitedDecls);

        return [.. headers,];
    }

    private static void CollectRequiredHeaders(
        ClangSharp.Type? type,
        HashSet<string> headers,
        HashSet<CXType> visitedTypes,
        HashSet<CXCursor> visitedDecls)
    {
        if (type is null)
        {
            return;
        }

        type = type.CanonicalType;
        if (!visitedTypes.Add(type.Handle))
        {
            return;
        }

        switch (type)
        {
            case PointerType pointerType:
                CollectRequiredHeaders(pointerType.PointeeType, headers, visitedTypes, visitedDecls);
                return;

            case LValueReferenceType lValueReferenceType:
                CollectRequiredHeaders(lValueReferenceType.PointeeType, headers, visitedTypes, visitedDecls);
                return;

            case RValueReferenceType rValueReferenceType:
                CollectRequiredHeaders(rValueReferenceType.PointeeType, headers, visitedTypes, visitedDecls);
                return;

            case TemplateSpecializationType templateSpecializationType:
                foreach (var templateArgument in templateSpecializationType.Args)
                {
                    CollectRequiredHeaders(templateArgument, headers, visitedTypes, visitedDecls);
                }

                break;
        }

        if (TryGetEnumDecl((ClangSharp.Type)type, out var enumDecl))
        {
            AddHeader(enumDecl, headers, visitedDecls);
            return;
        }

        if (type.AsCXXRecordDecl?.Definition is { } recordDecl)
        {
            AddHeader(recordDecl, headers, visitedDecls);

            if (recordDecl is ClassTemplateSpecializationDecl classTemplateSpecializationDecl)
            {
                foreach (var templateArgument in classTemplateSpecializationDecl.TemplateArgs)
                {
                    CollectRequiredHeaders(templateArgument, headers, visitedTypes, visitedDecls);
                }
            }
        }
    }

    private static void CollectRequiredHeaders(
        TemplateArgument templateArgument,
        HashSet<string> headers,
        HashSet<CXType> visitedTypes,
        HashSet<CXCursor> visitedDecls)
    {
        switch (templateArgument.Kind)
        {
            case CXTemplateArgumentKind.CXTemplateArgumentKind_Type:
                CollectRequiredHeaders(templateArgument.AsType, headers, visitedTypes, visitedDecls);
                break;

            case CXTemplateArgumentKind.CXTemplateArgumentKind_Declaration:
                AddHeader(templateArgument.AsDecl, headers, visitedDecls);
                break;

            case CXTemplateArgumentKind.CXTemplateArgumentKind_NullPtr:
                CollectRequiredHeaders(templateArgument.NullPtrType, headers, visitedTypes, visitedDecls);
                break;
        }
    }

    private static void AddHeader(Decl decl, HashSet<string> headers, HashSet<CXCursor> visitedDecls)
    {
        if (!visitedDecls.Add(decl.Handle))
        {
            return;
        }

        decl.Location.GetFileLocation(out var file, out _, out _, out _);
        if (string.IsNullOrEmpty(file.Name.CString))
        {
            return;
        }

        headers.Add(Path.GetFileName(file.Name.CString));
    }

    private static bool TryGetEnumDecl(ClangSharp.Type type, [NotNullWhen(true)] out EnumDecl? enumDecl)
    {
        enumDecl = type switch
        {
            EnumType enumType => enumType.Decl,
            _ => type.AsTagDecl as EnumDecl,
        };

        return enumDecl is not null;
    }

    private MethodModel ToModel(CXXMethodDecl method)
    {
        var commentProjection = method.ToCommentProjection();

        return new()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            ReturnTypeDescriptionItems = commentProjection.ReturnTypeDescriptionItems,
            ReturnType = ToModel(method.ReturnType),
            ReturnSelf = IsCompoundAssignmentOperator(method),
            MethodName = GetMethodName(method),
            NativeMethodName = method.Name,
            Type = GetMethodType(method),
            Parameters = method.Parameters.Select((p, index) => ToModel(
                    p,
                    index,
                    commentProjection))
                .ToArray(),
            NoExceptions = IsNoExcept(method),
            IsConst = method.IsConst,
            IsStatic = method.IsStatic,
        };
    }

    private static MethodModelType GetMethodType(CXXMethodDecl method)
    {
        return method switch
        {
            CXXConstructorDecl => MethodModelType.NEW,
            CXXDestructorDecl => MethodModelType.DELETE,
            CXXConversionDecl conversionDecl => IsExplicitConversion(conversionDecl)
                ? MethodModelType.EXPLICIT
                : MethodModelType.IMPLICIT,
            _ when method.IsOverloadedOperator => MethodModelType.OPERATOR,
            _ => MethodModelType.NORMAL,
        };
    }

    private static string GetMethodName(CXXMethodDecl method)
    {
        return GetMethodType(method) switch
        {
            MethodModelType.NEW => "New",
            MethodModelType.DELETE => "Delete",
            MethodModelType.IMPLICIT => "Implicit",
            MethodModelType.EXPLICIT => "Explicit",
            MethodModelType.OPERATOR => method.OverloadedOperator switch
            {
                CX_OverloadedOperatorKind.CX_OO_Invalid => "unknown",
                CX_OverloadedOperatorKind.CX_OO_Plus => "+",
                CX_OverloadedOperatorKind.CX_OO_Minus => "-",
                CX_OverloadedOperatorKind.CX_OO_Star => "*",
                CX_OverloadedOperatorKind.CX_OO_Slash => "/",
                CX_OverloadedOperatorKind.CX_OO_Percent => "%",
                CX_OverloadedOperatorKind.CX_OO_Caret => "^",
                CX_OverloadedOperatorKind.CX_OO_Amp => "&",
                CX_OverloadedOperatorKind.CX_OO_Pipe => "|",
                CX_OverloadedOperatorKind.CX_OO_Tilde => "~",
                CX_OverloadedOperatorKind.CX_OO_Exclaim => "!",
                CX_OverloadedOperatorKind.CX_OO_Less => "<",
                CX_OverloadedOperatorKind.CX_OO_Greater => ">",
                CX_OverloadedOperatorKind.CX_OO_PlusEqual => "+=",
                CX_OverloadedOperatorKind.CX_OO_MinusEqual => "-=",
                CX_OverloadedOperatorKind.CX_OO_StarEqual => "*=",
                CX_OverloadedOperatorKind.CX_OO_SlashEqual => "/=",
                CX_OverloadedOperatorKind.CX_OO_PercentEqual => "%=",
                CX_OverloadedOperatorKind.CX_OO_CaretEqual => "^=",
                CX_OverloadedOperatorKind.CX_OO_AmpEqual => "&=",
                CX_OverloadedOperatorKind.CX_OO_PipeEqual => "|=",
                CX_OverloadedOperatorKind.CX_OO_LessLess => "<<",
                CX_OverloadedOperatorKind.CX_OO_GreaterGreater => ">>",
                CX_OverloadedOperatorKind.CX_OO_LessLessEqual => "<<=",
                CX_OverloadedOperatorKind.CX_OO_GreaterGreaterEqual => ">>=",
                CX_OverloadedOperatorKind.CX_OO_EqualEqual => "==",
                CX_OverloadedOperatorKind.CX_OO_ExclaimEqual => "!=",
                CX_OverloadedOperatorKind.CX_OO_LessEqual => "<=",
                CX_OverloadedOperatorKind.CX_OO_GreaterEqual => ">=",
                CX_OverloadedOperatorKind.CX_OO_Spaceship => "<=>",
                CX_OverloadedOperatorKind.CX_OO_AmpAmp => "&&",
                CX_OverloadedOperatorKind.CX_OO_PipePipe => "||",
                CX_OverloadedOperatorKind.CX_OO_PlusPlus => "++",
                CX_OverloadedOperatorKind.CX_OO_MinusMinus => "--",
                CX_OverloadedOperatorKind.CX_OO_Comma => ",",
                CX_OverloadedOperatorKind.CX_OO_Arrow => "->",
                CX_OverloadedOperatorKind.CX_OO_Subscript => "[]",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method.OverloadedOperator,
                    "Unsupported overloaded operator kind."),
            },
            _ => method.Name.ToValidCSharpName(),
        };
    }

    private static bool IsExplicitConversion(CXXConversionDecl conversionDecl)
    {
        return conversionDecl.IsExplicit;
    }

    private static string GetMethodSignatureKey(CXXMethodDecl method)
    {
        var methodType = GetMethodType(method);

        return string.Join("|",
        [
            methodType.ToString(),
            GetMethodName(method),
            .. GetConversionReturnTypeSignatureParts(method, methodType),
            .. method.Parameters.Select(static p => GetMethodSignatureTypeCode(p.Type)),
        ]);
    }

    private static string GetMethodSignatureTypeCode(ClangSharp.Type type)
    {
        return type.ToPInvokeDataType().ToCode();
    }

    private static IEnumerable<string> GetConversionReturnTypeSignatureParts(
        CXXMethodDecl method,
        MethodModelType methodType)
    {
        if (methodType is not (MethodModelType.IMPLICIT or MethodModelType.EXPLICIT))
        {
            yield break;
        }

        yield return GetMethodSignatureTypeCode(method.ReturnType);
    }

    private static int GetConstQualificationWeight(CXXMethodDecl method)
    {
        var weight = method.IsConst ? 1 : 0;

        weight += CountConstQualifier(method.ReturnType);
        weight += method.Parameters.Sum(static p => CountConstQualifier(p.Type));

        return weight;
    }

    private static int CountConstQualifier(ClangSharp.Type type)
    {
        var count = 0;
        for (var current = type; ;)
        {
            if (current.IsLocalConstQualified)
            {
                count++;
                current = current.Desugar;
                continue;
            }

            if (current is PointerType or LValueReferenceType or RValueReferenceType)
            {
                current = current.PointeeType;
                continue;
            }

            break;
        }

        return count;
    }

    private static bool IsNoExcept(CXXMethodDecl method)
    {
        if (method.Type is not FunctionProtoType fpt)
        {
            return false;
        }

        return fpt.ExceptionSpecType switch
        {
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_BasicNoexcept => true,
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_ComputedNoexcept => true,
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_NoThrow => true,
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_DynamicNone => true,
            _ => false,
        };
    }

    private ParameterModel ToModel(
        ParmVarDecl paramDel,
        int parameterIndex,
        CommentProjection methodCommentProjection)
    {
        methodCommentProjection.ParameterDescriptionItems.TryGetValue(paramDel.Name, out var descriptionItems);

        return new()
        {
            DescriptionItems = descriptionItems ?? [],
            Type = ToModel(paramDel.Type),
            Name = string.IsNullOrEmpty(paramDel.Name) ? $"value{parameterIndex}" : paramDel.Name,
        };
    }

    private FieldModel ToModel(FieldDecl fieldDecl)
    {
        var commentProjection = fieldDecl.ToCommentProjection();

        var offset = fieldDecl.Handle.OffsetOfField / 8;
        var size = clang.Type_getSizeOf(fieldDecl.Type.Handle);
        var alignment = clang.Type_getAlignOf(fieldDecl.Type.Handle);
        if (offset < 0 || size <= 0 || alignment <= 0)
        {
            throw new NotSupportedException(
                $"Can't get physical layout of field ({fieldDecl.Name} in {fieldDecl.Parent?.Name})");
        }

        return new()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            Name = fieldDecl.Name,
            Type = ToModel(fieldDecl.Type),
            Offset = offset,
            Size = size,
            Alignment = alignment,
        };
    }

    private static bool IsDefined(ClangSharp.Type type)
    {
        if (IsAnonymousTypeName(type.CanonicalType.AsString))
        {
            return false;
        }

        var addingType = type.CanonicalType.GetAddingType()?.CanonicalType;

        if (addingType is null)
        {
            return false;
        }

        if (IsAnonymousTypeName(addingType.AsString))
        {
            return false;
        }

        if (addingType.Kind is CXTypeKind.CXType_LongDouble
            or CXTypeKind.CXType_Overload
            or CXTypeKind.CXType_Dependent
            or CXTypeKind.CXType_ObjCId
            or CXTypeKind.CXType_ObjCClass
            or CXTypeKind.CXType_ObjCSel
            or CXTypeKind.CXType_Float128
            or CXTypeKind.CXType_ShortAccum
            or CXTypeKind.CXType_Accum
            or CXTypeKind.CXType_LongAccum
            or CXTypeKind.CXType_UShortAccum
            or CXTypeKind.CXType_UAccum
            or CXTypeKind.CXType_ULongAccum
            or CXTypeKind.CXType_BFloat16
            or CXTypeKind.CXType_Ibm128)
        {
            return false;
        }

        if (addingType is BuiltinType)
        {
            return true;
        }

        if (TryGetEnumDecl(addingType, out var enumDeclaration))
        {
            return IsPubliclyAccessible(enumDeclaration);
        }

        return TryUnwrapRecord(addingType.AsCXXRecordDecl?.Definition) is { } record
               && IsPubliclyAccessible(record);
    }

    private static bool IsAnonymousTypeName(string cppTypeName)
    {
        return cppTypeName.Contains("(unnamed", StringComparison.Ordinal)
               || cppTypeName.Contains("(anonymous", StringComparison.Ordinal);
    }

    private static bool ShouldIncludeMethod(CXXMethodDecl method, bool isAbstract)
    {
        if (method is not (CXXConstructorDecl or CXXDestructorDecl) && !IsDefined(method.ReturnType))
        {
            return false;
        }

        if (method.Parameters.Any(p => !ShouldIncludeParameter(p, method.IsOverloadedOperator)))
        {
            return false;
        }

        if (method.Access is not CX_CXXAccessSpecifier.CX_CXXPublic)
        {
            return false;
        }

        if (method.IsDeleted || method.IsUnavailable || method.IsInvalidDecl)
        {
            return false;
        }

        if (method.OverloadedOperator
            is CX_OverloadedOperatorKind.CX_OO_Call
            or CX_OverloadedOperatorKind.CX_OO_Equal)
        {
            return false;
        }

        if (IsOperatorNewOrDelete(method))
        {
            return false;
        }

        if (isAbstract && method is CXXConstructorDecl or CXXDestructorDecl)
        {
            return false;
        }

        return true;
    }

    private static bool ShouldIncludeParameter(ParmVarDecl parameter, bool allowUnnamed)
    {
        if (!IsDefined(parameter.Type) || (!allowUnnamed && string.IsNullOrEmpty(parameter.Name)))
        {
            return false;
        }

        return !RequiresDeletedCopyForByValuePassing(parameter.Type);
    }

    private static bool RequiresDeletedCopyForByValuePassing(ClangSharp.Type type)
    {
        type = type.CanonicalType;

        while (type.IsLocalConstQualified)
        {
            type = type.Desugar.CanonicalType;
        }

        if (type is PointerType or LValueReferenceType or RValueReferenceType)
        {
            return false;
        }

        if (type.AsCXXRecordDecl?.Definition is not { } record)
        {
            return false;
        }

        var copyConstructors = record.Methods
            .OfType<CXXConstructorDecl>()
            .Where(static ctor => ctor.IsCopyConstructor)
            .ToArray();

        if (copyConstructors.Length is 0)
        {
            return record.HasUserDeclaredMoveOperation;
        }

        return !copyConstructors.Any(static ctor =>
            ctor.Access is CX_CXXAccessSpecifier.CX_CXXPublic
            && !ctor.IsDeleted
            && !ctor.IsUnavailable
            && !ctor.IsInvalidDecl);
    }

    private static bool IsOperatorNewOrDelete(CXXMethodDecl method)
    {
        if (!method.IsOverloadedOperator)
        {
            return false;
        }

        return method.OverloadedOperator switch
        {
            CX_OverloadedOperatorKind.CX_OO_New => true,
            CX_OverloadedOperatorKind.CX_OO_Delete => true,
            CX_OverloadedOperatorKind.CX_OO_Array_New => true,
            CX_OverloadedOperatorKind.CX_OO_Array_Delete => true,
            _ => false,
        };
    }

    private static bool IsCompoundAssignmentOperator(CXXMethodDecl method)
    {
        if (!method.IsOverloadedOperator)
        {
            return false;
        }

        return method.OverloadedOperator switch
        {
            CX_OverloadedOperatorKind.CX_OO_PlusEqual => true,
            CX_OverloadedOperatorKind.CX_OO_MinusEqual => true,
            CX_OverloadedOperatorKind.CX_OO_StarEqual => true,
            CX_OverloadedOperatorKind.CX_OO_SlashEqual => true,
            CX_OverloadedOperatorKind.CX_OO_PercentEqual => true,
            CX_OverloadedOperatorKind.CX_OO_CaretEqual => true,
            CX_OverloadedOperatorKind.CX_OO_AmpEqual => true,
            CX_OverloadedOperatorKind.CX_OO_PipeEqual => true,
            CX_OverloadedOperatorKind.CX_OO_LessLessEqual => true,
            CX_OverloadedOperatorKind.CX_OO_GreaterGreaterEqual => true,
            _ => false,
        };
    }

    private static CXXRecordDecl UnwrapRecord(CXXRecordDecl record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TryUnwrapRecord(record)
               ?? throw new NotSupportedException(
                   $"Can't unwrap handle specialization ({record.TypeForDecl.AsString})");
    }

    private static CXXRecordDecl? TryUnwrapRecord(CXXRecordDecl? record)
    {
        if (record is null)
        {
            return null;
        }

        if ((record.Definition ?? record) is not ClassTemplateSpecializationDecl classTemplateSpecializationDecl)
        {
            return record;
        }

        if (!IsHandleSpecialization(classTemplateSpecializationDecl))
        {
            return record;
        }

        foreach (var templateArgument in classTemplateSpecializationDecl.TemplateArgs)
        {
            if (templateArgument.Kind is not CXTemplateArgumentKind.CXTemplateArgumentKind_Type)
            {
                continue;
            }

            if (templateArgument.AsType.AsCXXRecordDecl?.Definition is { } innerRecord)
            {
                return innerRecord;
            }
        }

        return null;
    }

    private static bool IsHandleSpecialization(ClassTemplateSpecializationDecl record)
    {
        return record.TypeForDecl.AsString.StartsWith("opencascade::handle<", StringComparison.Ordinal)
               || record.TypeForDecl.AsString.StartsWith("occ::handle<", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public void Add(EnumDecl declaration)
    {
        if (!_enumNames.Add(declaration.CanonicalDecl.Handle))
        {
            return;
        }

        _enumModels.Add(CreateEnumModel(declaration));
    }

    private static EnumModel CreateEnumModel(EnumDecl enumDecl)
    {
        if (enumDecl.IntegerType.CanonicalType is not BuiltinType builtinType)
        {
            throw new NotSupportedException($"Unsupported enum underlying type ({enumDecl.IntegerType.AsString})");
        }

        var underlyingType = builtinType.ToDataType();

        return new()
        {
            DescriptionItems = enumDecl.ToCommentProjection().DescriptionItems,
            Name = enumDecl.QualifiedName.ToValidCSharpName(),
            SourceType = enumDecl.QualifiedName,
            UnderlyingType = underlyingType,
            Members = enumDecl.Enumerators.Select(ToEnumMember).ToArray(),
        };
    }

    private static EnumMemberModel ToEnumMember(EnumConstantDecl enumConstant)
    {
        return new()
        {
            DescriptionItems = enumConstant.ToCommentProjection().DescriptionItems,
            Name = enumConstant.Name,
            Value = enumConstant.IsUnsigned
                ? enumConstant.UnsignedInitVal.ToLiteral()
                : enumConstant.InitVal.ToLiteral(),
        };
    }
}