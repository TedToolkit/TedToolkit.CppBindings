// -----------------------------------------------------------------------
// <copyright file="RecordModelManager.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;
using TedToolkit.CppBindings.Occt.Generator.Models.Types;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Builds and caches projected record and enum models from parsed Clang declarations.
/// </summary>
/// <param name="options">The generation options.</param>
/// <param name="resolver">The type resolver.</param>
/// <param name="defaultsResolver">The default triplet resolver.</param>
/// <param name="vcpkgEnvironment">The vcpkg environment service.</param>
internal sealed class RecordModelManager(
    IOptions<OcctGenerationOptions> options,
    IResolver resolver,
    IVcpkgDefaultTripletResolver defaultsResolver,
    IVcpkgEnvironment vcpkgEnvironment) : IRecordModelManager
{
    private static readonly Regex NativeIdentifierRegex = new("[A-Za-z_][A-Za-z0-9_]*");

    private static readonly Regex RefQualifierRegex = new(
        @"\)\s*(?:const\s*)?(?:volatile\s*)?(&&|&)(?:\s*noexcept)?$");

    private readonly List<EnumModel> _enumModels = [];

    private readonly HashSet<CXCursor> _enumNames = [];

    private readonly Dictionary<CXCursor, RecordModel> _recordNames = [];

    private readonly Dictionary<string, byte[]> _sourceFiles = new(StringComparer.OrdinalIgnoreCase);

    private bool _managedTemplateReferencesFinalized;

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
            FinalizeManagedTemplateReferences();
            return _recordNames.Values.Where(t => (t.IsPubliclyAccessible || t.IsRequiredDependency)
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
        var projectedType = ApplyRequiredHeaders(
            resolver.Resolve(record.TypeForDecl)
                .Type,
            record.TypeForDecl);
        var templateProjection = CreateTemplateProjection(record);
        if (templateProjection is not null)
        {
            projectedType.CSharpPInvokeType = new(templateProjection.ClosedTypeName);
            projectedType.CSharpPublicType = new(templateProjection.ClosedTypeName);
        }

        var result = new RecordModel()
        {
            TemplateProjection = templateProjection,
            IsPubliclyAccessible = IsPubliclyAccessible(record),
            IsClosedTemplateSpecialization = IsClosedTemplateSpecialization(record),
            DescriptionItems = commentProjection.DescriptionItems,
            SourceHeader = Path.GetFileName(file.Name.CString),
            Type = projectedType,
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
            .Select(field => ToModel(field, record, templateProjection))
            .ToArray();
        if (result.TemplateProjection is not null && !HasGenericPhysicalLayout(result))
        {
            KeepClosedTemplateProjection(result);
        }

        result.MethodModels = record.Methods
            .Where(m => ShouldIncludeMethod(m, record.IsAbstract))
            .GroupBy(GetMethodSignatureKey)
            .Select(static methods => methods
                .OrderBy(GetConstQualificationWeight)
                .First())
            .Select(ToModel)
            .SelectMany(ExpandDefaultArgumentOverloads)
            .GroupBy(GetProjectedMethodSignatureKey)
            .Select(SelectProjectedOverload)
            .Where(method => IsInstantiableTemplateMember(result.Type.CppTypeName, method))
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

    private static bool HasGenericPhysicalLayout(RecordModel record)
    {
        var currentOffset = 0L;
        foreach (var field in record.FieldModels.OrderBy(static field => field.Offset))
        {
            if (field.Offset != currentOffset)
            {
                return false;
            }

            if (string.IsNullOrEmpty(field.CSharpTemplateType)
                && !string.IsNullOrEmpty(field.CppTemplateType)
                && !string.Equals(
                    field.CppTemplateType,
                    field.Type.CppTypeName,
                    StringComparison.Ordinal))
            {
                // The field depends on template arguments through a nested native type that has
                // not itself been projected as a managed generic layout. Keeping this record closed
                // avoids claiming that one concrete field layout is valid for every T.
                return false;
            }

            currentOffset = checked(currentOffset + field.Size);
        }

        return currentOffset == record.Size || (record.FieldModels.Count is 0 && record.Size is 1);
    }

    private void FinalizeManagedTemplateReferences()
    {
        if (_managedTemplateReferencesFinalized)
        {
            return;
        }

        _managedTemplateReferencesFinalized = true;
        var records = _recordNames.Values.ToArray();
        KeepAmbiguousTemplateSpecializationsClosed(records);
        var replacements = records
            .Where(static record => record.TemplateProjection is not null)
            .Select(static record => record.TemplateProjection!)
            .ToDictionary(static projection => projection.FixedTypeName, StringComparer.Ordinal);
        if (replacements.Count is 0)
        {
            return;
        }

        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var resolving = new HashSet<string>(StringComparer.Ordinal);
        foreach (var projection in replacements.Values)
        {
            ResolveTemplateProjection(projection, replacements, resolved, resolving);
        }

        foreach (var record in records)
        {
            if (record.TemplateProjection is not null)
            {
                record.Type.CSharpPInvokeType = new(record.TemplateProjection.ClosedTypeName);
                record.Type.CSharpPublicType = new(record.TemplateProjection.ClosedTypeName);
            }

            UpdateManagedType(record.Type, replacements, resolved, resolving);
            foreach (var field in record.FieldModels)
            {
                UpdateManagedType(field.Type, replacements, resolved, resolving);
            }

            foreach (var method in record.MethodModels)
            {
                UpdateManagedType(method.ReturnType, replacements, resolved, resolving);
                foreach (var parameter in method.Parameters)
                {
                    UpdateManagedType(parameter.Type, replacements, resolved, resolving);
                }
            }
        }
    }

    private static void KeepAmbiguousTemplateSpecializationsClosed(IReadOnlyList<RecordModel> records)
    {
        foreach (var specialization in records
                     .Where(static record => record.TemplateProjection is not null)
                     .GroupBy(static record => (
                         record.TemplateProjection!.FamilyName,
                         record.TemplateProjection.ClosedTypeName))
                     .Where(static group => group
                         .Select(static record => record.Type.CppTypeName)
                         .Distinct(StringComparer.Ordinal)
                         .Skip(1)
                         .Any())
                     .SelectMany(static group => group))
        {
            KeepClosedTemplateProjection(specialization);
        }

        foreach (var family in records
                     .Where(static record => record.TemplateProjection is not null)
                     .GroupBy(static record => record.TemplateProjection!.FamilyName, StringComparer.Ordinal))
        {
            var shapes = family.Select(GetTemplatePhysicalShape)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count();
            if (shapes < 2)
            {
                continue;
            }

            foreach (var specialization in family)
            {
                KeepClosedTemplateProjection(specialization);
            }
        }
    }

    private static string GetTemplatePhysicalShape(RecordModel record)
    {
        return string.Join(
            "\n",
            record.ObjectKind,
            record.IsStandardTransient,
            record.Bases.Count(static relation => relation.IsPublic),
            string.Join("|", record.FieldModels.Select(static field =>
                string.Join(":", field.Name, field.CppTemplateType, field.CSharpTemplateType))));
    }

    private static void KeepClosedTemplateProjection(RecordModel record)
    {
        var projection = record.TemplateProjection;
        if (projection is null)
        {
            return;
        }

        record.Type.CSharpPInvokeType = new(projection.FixedTypeName);
        record.Type.CSharpPublicType = new(projection.FixedTypeName);
        record.TemplateProjection = null;
    }

    private static void ResolveTemplateProjection(
        TemplateProjectionModel projection,
        IReadOnlyDictionary<string, TemplateProjectionModel> replacements,
        HashSet<string> resolved,
        HashSet<string> resolving)
    {
        if (resolved.Contains(projection.FixedTypeName)
            || !resolving.Add(projection.FixedTypeName))
        {
            return;
        }

        foreach (var argument in projection.GenericArguments)
        {
            argument.ClosedCSharpType = ReplaceManagedTemplateReferences(
                argument.ClosedCSharpType,
                replacements,
                resolved,
                resolving);
        }

        projection.ClosedTypeName = projection.FamilyName + "<"
            + string.Join(", ", projection.GenericArguments.Select(static argument => argument.ClosedCSharpType))
            + ">";
        _ = resolving.Remove(projection.FixedTypeName);
        _ = resolved.Add(projection.FixedTypeName);
    }

    private static void UpdateManagedType(
        TypeModel type,
        IReadOnlyDictionary<string, TemplateProjectionModel> replacements,
        HashSet<string> resolved,
        HashSet<string> resolving)
    {
        type.CSharpPInvokeType = new(ReplaceManagedTemplateReferences(
            type.CSharpPInvokeType.ToCode(), replacements, resolved, resolving));
        type.CSharpPublicType = new(ReplaceManagedTemplateReferences(
            type.CSharpPublicType.ToCode(), replacements, resolved, resolving));
        type.OcctHandleElementType = ReplaceManagedTemplateReferences(
            type.OcctHandleElementType, replacements, resolved, resolving);
    }

    private static string ReplaceManagedTemplateReferences(
        string value,
        IReadOnlyDictionary<string, TemplateProjectionModel> replacements,
        HashSet<string> resolved,
        HashSet<string> resolving)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return NativeIdentifierRegex.Replace(value, match =>
        {
            if (!replacements.TryGetValue(match.Value, out var projection))
            {
                return match.Value;
            }

            var next = match.Index + match.Length;
            while (next < value.Length && char.IsWhiteSpace(value[next]))
            {
                next++;
            }

            if (next < value.Length && value[next] is '<')
            {
                return match.Value;
            }

            ResolveTemplateProjection(projection, replacements, resolved, resolving);
            return projection.ClosedTypeName;
        });
    }

    private TemplateProjectionModel? CreateTemplateProjection(CXXRecordDecl record)
    {
        if (record is not ClassTemplateSpecializationDecl specialization
            || IsHandleSpecialization(specialization))
        {
            return null;
        }

        var specializedCursor = clang.getSpecializedCursorTemplate(specialization.Handle);
        if (specializedCursor.kind is CXCursorKind.CXCursor_ClassTemplatePartialSpecialization)
        {
            // ClangSharp 21 incorrectly casts this cursor to ClassTemplateDecl. Keep the exact
            // closed projection until the wrapper exposes the partial-specialization parameter list.
            return null;
        }

        var parameters = specialization.SpecializedTemplate.TemplateParameters;
        var arguments = specialization.TemplateArgs;
        if (parameters.Count is 0 || parameters.Count != arguments.Count)
        {
            return null;
        }

        var nativeSpellings = GetTemplateArgumentSpellings(record.TypeForDecl.AsString, arguments);
        var projections = new TemplateArgumentProjection[arguments.Count];
        var genericCount = 0;
        for (var index = 0; index < arguments.Count; index++)
        {
            var parameterName = string.IsNullOrWhiteSpace(parameters[index].Name)
                ? $"T{index + 1}"
                : parameters[index].Name.ToValidCSharpName();
            var argument = arguments[index];
            var nativeArgument = nativeSpellings[index];
            if (parameters[index] is TemplateTypeParmDecl
                && IsManagedGenericArgument(argument, out var closedCSharpType))
            {
                projections[index] = new()
                {
                    ParameterName = parameterName,
                    NativeArgument = nativeArgument,
                    ClosedCSharpType = closedCSharpType,
                    Kind = TemplateArgumentProjectionKind.Generic,
                };
                genericCount++;
            }
            else
            {
                projections[index] = new()
                {
                    ParameterName = parameterName,
                    NativeArgument = nativeArgument,
                    Kind = TemplateArgumentProjectionKind.Fixed,
                };
            }
        }

        if (genericCount is 0)
        {
            return null;
        }

        var familyName = specialization.SpecializedTemplate.QualifiedName.ToGeneratedTypeName();
        foreach (var fixedArgument in projections.Where(static argument =>
                     argument.Kind is TemplateArgumentProjectionKind.Fixed))
        {
            familyName += "_" + ToFixedTemplateArgumentToken(fixedArgument.NativeArgument);
        }

        var genericArguments = projections.Where(static argument =>
                argument.Kind is TemplateArgumentProjectionKind.Generic)
            .ToArray();
        return new()
        {
            FixedTypeName = record.TypeForDecl.AsString.ToGeneratedTypeName(),
            NativeTemplateName = specialization.SpecializedTemplate.QualifiedName,
            NativeTypePattern = CreateNativeTemplatePattern(specialization.SpecializedTemplate.QualifiedName, projections),
            FamilyName = familyName,
            DeclarationTypeName = familyName + "<"
                + string.Join(", ", genericArguments.Select(static argument => argument.ParameterName)) + ">",
            ClosedTypeName = familyName + "<"
                + string.Join(", ", genericArguments.Select(static argument => argument.ClosedCSharpType)) + ">",
            Arguments = projections,
        };
    }

    private static string CreateNativeTemplatePattern(
        string nativeTemplateName,
        IReadOnlyList<TemplateArgumentProjection> arguments)
    {
        return nativeTemplateName + "<" + string.Join(", ", arguments.Select(static argument =>
            argument.Kind is TemplateArgumentProjectionKind.Generic
                ? argument.ParameterName
                : argument.NativeArgument)) + ">";
    }

    private bool IsManagedGenericArgument(TemplateArgument argument, out string closedCSharpType)
    {
        closedCSharpType = "";
        if (argument.Kind is not CXTemplateArgumentKind.CXTemplateArgumentKind_Type
            || argument.AsType.CanonicalType.Kind is CXTypeKind.CXType_Void)
        {
            return false;
        }

        if (argument.AsType.CanonicalType.AsCXXRecordDecl is { Definition: null, })
        {
            return false;
        }

        var transport = Resolver.CreateTransport(argument.AsType, out _);
        if (transport.Indirections.Count is not 0)
        {
            return false;
        }

        closedCSharpType = resolver.Resolve(argument.AsType).Type.CSharpPublicType.ToCode();
        return !string.IsNullOrWhiteSpace(closedCSharpType)
               && closedCSharpType is not "void"
               && !closedCSharpType.Contains('*', StringComparison.Ordinal)
               && !closedCSharpType.Contains('&', StringComparison.Ordinal);
    }

    private static string[] GetTemplateArgumentSpellings(
        string nativeTypeName,
        IReadOnlyList<TemplateArgument> arguments)
    {
        var result = SplitInnermostTemplateArguments(nativeTypeName);
        while (result.Count < arguments.Count)
        {
            result.Add(GetTemplateArgumentFallback(arguments[result.Count]));
        }

        if (result.Count > arguments.Count)
        {
            result.RemoveRange(arguments.Count, result.Count - arguments.Count);
        }

        return result.ToArray();
    }

    private static List<string> SplitInnermostTemplateArguments(string nativeTypeName)
    {
        var close = nativeTypeName.LastIndexOf('>');
        if (close < 0)
        {
            return [];
        }

        var depth = 0;
        var open = -1;
        for (var index = close; index >= 0; index--)
        {
            switch (nativeTypeName[index])
            {
                case '>':
                    depth++;
                    break;

                case '<':
                    depth--;
                    if (depth is 0)
                    {
                        open = index;
                        index = -1;
                    }

                    break;
            }
        }

        if (open < 0)
        {
            return [];
        }

        var contents = nativeTypeName.AsSpan(open + 1, close - open - 1);
        var result = new List<string>();
        var start = 0;
        depth = 0;
        for (var index = 0; index < contents.Length; index++)
        {
            switch (contents[index])
            {
                case '<':
                case '(':
                case '[':
                case '{':
                    depth++;
                    break;

                case '>':
                case ')':
                case ']':
                case '}':
                    depth--;
                    break;

                case ',' when depth is 0:
                    result.Add(contents[start..index].Trim().ToString());
                    start = index + 1;
                    break;
            }
        }

        result.Add(contents[start..].Trim().ToString());
        return result;
    }

    private static string GetTemplateArgumentFallback(TemplateArgument argument)
    {
        return argument.Kind switch
        {
            CXTemplateArgumentKind.CXTemplateArgumentKind_Type => argument.AsType.AsString,
            CXTemplateArgumentKind.CXTemplateArgumentKind_Integral
                when argument.IntegralType.CanonicalType.Kind is CXTypeKind.CXType_Bool =>
                argument.AsIntegral is 0 ? "false" : "true",
            CXTemplateArgumentKind.CXTemplateArgumentKind_Integral =>
                argument.AsIntegral.ToString(CultureInfo.InvariantCulture),
            CXTemplateArgumentKind.CXTemplateArgumentKind_Declaration => argument.AsDecl.QualifiedName,
            CXTemplateArgumentKind.CXTemplateArgumentKind_NullPtr => "nullptr",
            _ => "argument",
        };
    }

    private static string ToFixedTemplateArgumentToken(string nativeArgument)
    {
        var value = nativeArgument.Trim();
        if (value.StartsWith('-'))
        {
            value = "minus_" + value[1..];
        }
        else if (value.StartsWith('+'))
        {
            value = "plus_" + value[1..];
        }

        value = value.ToGeneratedTypeName().Trim('_');
        return string.IsNullOrEmpty(value) ? "argument" : value;
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
            || !HasDefinedTemplateArguments(unwrappedDependency)
            || (!IsPubliclyAccessible(unwrappedDependency)
                && !HasPublicTemplateArguments(unwrappedDependency)))
        {
            return;
        }

        var dependencyModel = Add(unwrappedDependency);
        dependencyModel.IsRequiredDependency = true;
        _ = dependencies.Add(dependencyModel);
    }

    private static bool HasDefinedTemplateArguments(CXXRecordDecl record)
    {
        if (record is not ClassTemplateSpecializationDecl specialization)
        {
            return true;
        }

        foreach (var argument in specialization.TemplateArgs.Where(static argument =>
                     argument.Kind is CXTemplateArgumentKind.CXTemplateArgumentKind_Type))
        {
            var argumentRecord = argument.AsType.GetAddingType()?.AsCXXRecordDecl;
            if (argumentRecord is not null && argumentRecord.Definition is null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasPublicTemplateArguments(CXXRecordDecl record)
    {
        if (record is not ClassTemplateSpecializationDecl specialization)
        {
            return false;
        }

        var templateCursor = clang.getSpecializedCursorTemplate(specialization.Handle);
        if (!IsPubliclyAccessibleTemplate(templateCursor))
        {
            return false;
        }

        foreach (var argument in specialization.TemplateArgs.Where(static argument =>
                     argument.Kind is CXTemplateArgumentKind.CXTemplateArgumentKind_Type))
        {
            var argumentType = argument.AsType.GetAddingType();
            if (argumentType?.AsCXXRecordDecl is { } argumentRecord
                && !IsPubliclyAccessible(argumentRecord.Definition ?? argumentRecord))
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

    private static bool IsPubliclyAccessibleTemplate(CXCursor cursor)
    {
        if (cursor.kind is CXCursorKind.CXCursor_NoDeclFound
            or CXCursorKind.CXCursor_InvalidFile
            or CXCursorKind.CXCursor_NotImplemented
            or CXCursorKind.CXCursor_InvalidCode)
        {
            return false;
        }

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

    private static bool IsClosedTemplateSpecialization(CXXRecordDecl record)
    {
        if (record is not ClassTemplateSpecializationDecl specialization)
        {
            return true;
        }

        return specialization.TemplateArgs.Any(static argument =>
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
        if (!File.Exists(sourceHeader))
        {
            return;
        }

        var source = File.ReadAllText(sourceHeader);
        AddPrerequisiteHeader(source, "OCCT_DUMP_", "Standard_Dump.hxx", headers);
        AddPrerequisiteHeader(
            source,
            "NCollection_List<TopoDS_Shape>",
            "TopoDS_Shape.hxx",
            headers);
        AddPrerequisiteHeader(source, "TopoDS_TShape.hxx", "TopoDS_Shape.hxx", headers);
        AddPrerequisiteHeader(source, "XCAFDoc_AssemblyIterator.hxx", "TDF_Label.hxx", headers);
    }

    private static void AddPrerequisiteHeader(
        string source,
        string marker,
        string header,
        HashSet<string> headers)
    {
        _ = source.Contains(marker, StringComparison.Ordinal) && headers.Add(header);
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

        if (type.AsCXXRecordDecl is { } declaration)
        {
            var recordDecl = declaration.Definition ?? declaration;
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

        var header = Path.GetFileName(file.Name.CString);
        headers.Add(string.Equals(header, "winnt.h", StringComparison.OrdinalIgnoreCase)
            ? "Windows.h"
            : header);
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
            OverloadPriority = method.Parameters.Count,
            NoExceptions = IsNoExcept(method),
            IsConst = method.IsConst,
            IsVolatile = method.Type.AsString.Contains(" volatile", StringComparison.Ordinal),
            RefQualifier = GetRefQualifier(method.Type.AsString),
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
            CppDefaultValue = GetCppDefaultValue(paramDel),
            DescriptionItems = descriptionItems ?? [],
            Type = ToModel(paramDel.Type),
            Name = string.IsNullOrEmpty(paramDel.Name) ? $"value{parameterIndex}" : paramDel.Name,
        };
    }

    private IEnumerable<MethodModel> ExpandDefaultArgumentOverloads(MethodModel method)
    {
        yield return method;

        if (method.Type is not MethodModelType.NEW)
        {
            yield break;
        }

        var requiredCount = method.Parameters.Count;
        while (requiredCount > 0
               && method.Parameters[requiredCount - 1].CppDefaultValue is not null)
        {
            requiredCount--;
        }

        for (var parameterCount = requiredCount; parameterCount < method.Parameters.Count; parameterCount++)
        {
            yield return new()
            {
                DescriptionItems = method.DescriptionItems,
                ReturnTypeDescriptionItems = method.ReturnTypeDescriptionItems,
                NativeDefaultArguments = method.Parameters
                    .Skip(parameterCount)
                    .Select(static parameter => parameter.CppDefaultValue!)
                    .ToArray(),
                OverloadPriority = method.Parameters.Count,
                NativeExportName = method.NativeExportName,
                NativeMethodName = method.NativeMethodName,
                NoExceptions = method.NoExceptions,
                IsConst = method.IsConst,
                IsVolatile = method.IsVolatile,
                RefQualifier = method.RefQualifier,
                IsStatic = method.IsStatic,
                ReturnType = method.ReturnType,
                ReturnSelf = method.ReturnSelf,
                MethodName = method.MethodName,
                Type = method.Type,
                Parameters = method.Parameters.Take(parameterCount).ToArray(),
            };
        }
    }

    private static MethodModel SelectProjectedOverload(IEnumerable<MethodModel> methods)
    {
        var candidates = methods.ToArray();
        var selected = candidates
            .OrderByDescending(static method => method.OverloadPriority)
            .First();
        if (candidates.Length > 1 || selected.NativeDefaultArguments.Count is 0)
        {
            return selected;
        }

        return new()
        {
            DescriptionItems = selected.DescriptionItems,
            ReturnTypeDescriptionItems = selected.ReturnTypeDescriptionItems,
            NativeDefaultArguments = [],
            OverloadPriority = selected.OverloadPriority,
            NativeExportName = selected.NativeExportName,
            NativeMethodName = selected.NativeMethodName,
            NoExceptions = selected.NoExceptions,
            IsConst = selected.IsConst,
            IsVolatile = selected.IsVolatile,
            RefQualifier = selected.RefQualifier,
            IsStatic = selected.IsStatic,
            ReturnType = selected.ReturnType,
            ReturnSelf = selected.ReturnSelf,
            MethodName = selected.MethodName,
            Type = selected.Type,
            Parameters = selected.Parameters,
        };
    }

    private static string GetRefQualifier(string functionType)
    {
        var match = RefQualifierRegex.Match(functionType);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string GetProjectedMethodSignatureKey(MethodModel method)
    {
        return string.Join("|",
        [
            method.Type.ToString(),
            method.MethodName,
            method.ReturnType.CSharpPublicType.ToCode(),
            .. method.Parameters.Select(static parameter => parameter.Type.CSharpPublicType.ToCode()),
        ]);
    }

    private static bool IsInstantiableTemplateMember(string cppTypeName, MethodModel method)
    {
        // IntPolyh_Array<T>::Dump() requires T::Dump(). IntPolyh_Edge only provides Dump(int),
        // so this concrete member cannot be instantiated even though Clang exposes its declaration.
        if (cppTypeName is "IntPolyh_Array<IntPolyh_Edge>" or "IntPolyh_Array<IntPolyh_Triangle>")
        {
            return method.MethodName is not "Dump" || method.Parameters.Count is not 0;
        }

        if (cppTypeName is "NCollection_CellFilter<BRepExtrema_VertexInspector>")
        {
            return method.MethodName is not "Remove";
        }

        if (cppTypeName is "NCollection_Vec3<unsigned long long>")
        {
            return method.MethodName is not "cwiseAbs";
        }

        if (cppTypeName is "BVH_PairTraverse<double, 3>"
            && method.MethodName is "Select"
            && method.Parameters.Count is 0)
        {
            return false;
        }

        if (cppTypeName.StartsWith("std::unique_ptr<Geom_OsculatingSurface", StringComparison.Ordinal)
            && method.Type is MethodModelType.DELETE)
        {
            return false;
        }

        if (method.MethodName is "swap"
            && (cppTypeName.StartsWith("std::variant<std::monostate, Geom2dGridEval_", StringComparison.Ordinal)
                || cppTypeName.StartsWith("std::variant<std::monostate, GeomBndLib_", StringComparison.Ordinal)
                || cppTypeName.StartsWith("std::variant<std::monostate, GeomGridEval_", StringComparison.Ordinal)))
        {
            return false;
        }

        if (cppTypeName is not "NCollection_Sequence<CSLib_Class2d>")
        {
            return true;
        }

        if (method.MethodName is "Assign" or "SetValue")
        {
            return false;
        }

        if (method.MethodName is "Append" or "Prepend" or "InsertBefore" or "InsertAfter"
            && method.Parameters.Any(parameter => parameter.Type.CppValueTypeName == cppTypeName))
        {
            return false;
        }

        return true;
    }

    private string? GetCppDefaultValue(ParmVarDecl parameter)
    {
        if (parameter.DefaultArg is { } defaultArgument)
        {
            return ReadSourceRange(defaultArgument.Extent)?.Trim();
        }

        var parameterSource = ReadSourceRange(parameter.SourceRange);
        if (parameterSource is null)
        {
            return null;
        }

        var assignmentIndex = FindDefaultAssignment(parameterSource);
        return assignmentIndex < 0 ? null : parameterSource[(assignmentIndex + 1)..].Trim();
    }

    private string? ReadSourceRange(CXSourceRange range)
    {
        range.Start.GetFileLocation(out var startFile, out _, out _, out var startOffset);
        range.End.GetFileLocation(out var endFile, out _, out _, out var endOffset);
        var fileName = startFile.Name.CString;
        if (string.IsNullOrEmpty(fileName)
            || !string.Equals(fileName, endFile.Name.CString, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fileName)
            || endOffset <= startOffset)
        {
            return null;
        }

        if (!_sourceFiles.TryGetValue(fileName, out var source))
        {
            source = File.ReadAllBytes(fileName);
            _sourceFiles.Add(fileName, source);
        }

        return Encoding.UTF8.GetString(source, checked((int)startOffset), checked((int)(endOffset - startOffset)))
            .Trim();
    }

    private static int FindDefaultAssignment(string parameterSource)
    {
        var depth = 0;
        for (var index = 0; index < parameterSource.Length; index++)
        {
            switch (parameterSource[index])
            {
                case '(':
                case '[':
                case '{':
                case '<':
                    depth++;
                    break;

                case ')':
                case ']':
                case '}':
                case '>':
                    depth--;
                    break;

                case '=' when depth is 0
                                   && (index is 0 || parameterSource[index - 1] is not '!' and not '<' and not '>' and not '=')
                                   && (index + 1 >= parameterSource.Length || parameterSource[index + 1] is not '='):
                    return index;
            }
        }

        return -1;
    }

    private FieldModel ToModel(
        FieldDecl fieldDecl,
        CXXRecordDecl declaringRecord,
        TemplateProjectionModel? templateProjection)
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

        var templateField = GetTemplateField(fieldDecl, declaringRecord, templateProjection);
        return new()
        {
            CSharpTemplateType = GetDirectTemplateFieldType(templateField, templateProjection),
            CppTemplateType = templateField?.Type.AsString ?? "",
            DescriptionItems = commentProjection.DescriptionItems,
            Name = fieldDecl.Name,
            Type = ToModel(fieldDecl.Type),
            Offset = offset,
            Size = size,
            Alignment = alignment,
        };
    }

    private static FieldDecl? GetTemplateField(
        FieldDecl fieldDecl,
        CXXRecordDecl declaringRecord,
        TemplateProjectionModel? templateProjection)
    {
        if (templateProjection is null
            || declaringRecord is not ClassTemplateSpecializationDecl specialization)
        {
            return null;
        }

        return specialization.SpecializedTemplate.TemplatedDecl.Fields
            .FirstOrDefault(candidate => string.Equals(candidate.Name, fieldDecl.Name, StringComparison.Ordinal));
    }

    private static string GetDirectTemplateFieldType(
        FieldDecl? templateField,
        TemplateProjectionModel? templateProjection)
    {
        if (templateField is null || templateProjection is null)
        {
            return "";
        }

        var current = templateField.Type;
        var pointerDepth = 0;
        while (current is PointerType)
        {
            pointerDepth++;
            current = current.PointeeType;
        }

        if (current is not TemplateTypeParmType templateParameter
            || templateParameter.Index >= templateProjection.Arguments.Count)
        {
            return "";
        }

        var argument = templateProjection.Arguments[(int)templateParameter.Index];
        if (argument.Kind is not TemplateArgumentProjectionKind.Generic)
        {
            return "";
        }

        return argument.ParameterName + new string('*', pointerDepth);
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