// -----------------------------------------------------------------------
// <copyright file="BindingSemanticGraphSnapshot.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Copies the caller-owned semantic graph while preserving record and type identity.
/// </summary>
internal sealed class BindingSemanticGraphSnapshot
{
    private readonly Dictionary<RecordModel, RecordModel> _records = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<TypeModel, TypeModel> _types = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Creates one detached provider model for normalization and paired emission.
    /// </summary>
    /// <param name="source">The caller-owned provider input.</param>
    /// <returns>The detached semantic snapshot.</returns>
    public BindingProviderModel Create(BindingProviderModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(
            source.Declarations.Select(CloneDeclaration),
            source.Enums.Select(CloneEnum),
            source.EmissionProfile,
            source.ManagedSources,
            source.NativeSources,
            source.NativeExports,
            source.ManagedSourceStemEmitter,
            source.NativeSourceStemEmitter,
            source.NativeProject,
            source.FiniteProfileApis);
    }

    private BindingDeclaration CloneDeclaration(BindingDeclaration source)
    {
        return new(
            CloneRecord(source.Record),
            source.ManagedSourceGroup,
            source.IsRoot,
            source.Dependencies);
    }

    private RecordModel CloneRecord(RecordModel source)
    {
        if (_records.TryGetValue(source, out var existing))
        {
            return existing;
        }

        ArgumentNullException.ThrowIfNull(source.Type);
        ArgumentNullException.ThrowIfNull(source.DescriptionItems);
        var type = CreateTypeShell(source.Type);
        var clone = new RecordModel()
        {
            IsPubliclyAccessible = source.IsPubliclyAccessible,
            IsRequiredDependency = source.IsRequiredDependency,
            IsClosedTemplateSpecialization = source.IsClosedTemplateSpecialization,
            DescriptionItems = CloneRootDescriptions(source.DescriptionItems),
            IsAbstract = source.IsAbstract,
            UsesIntrusiveReferenceCounting = source.UsesIntrusiveReferenceCounting,
            SourceHeader = source.SourceHeader,
            UsesAllocatorPlacementNew = source.UsesAllocatorPlacementNew,
            Type = type,
            Size = source.Size,
            Alignment = source.Alignment,
            ObjectKind = source.ObjectKind,
            FieldModels = [],
            MethodModels = [],
        };
        _records.Add(source, clone);
        CompleteType(source.Type, type);

        clone.TemplateProjection = source.TemplateProjection is null
            ? null
            : CloneTemplate(source.TemplateProjection);
        clone.Bases = Copy(Required(source.Bases).Select(CloneBase));
        clone.NativeRequiredHeaders = Copy(Required(source.NativeRequiredHeaders));
        clone.NativeDependencyRecords = Copy(Required(source.NativeDependencyRecords).Select(CloneRecord));
        clone.FieldModels = Copy(Required(source.FieldModels).Select(CloneField));
        clone.MethodModels = Copy(Required(source.MethodModels).Select(CloneMethod));
        return clone;
    }

    private BaseRelationModel CloneBase(BaseRelationModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            Base = CloneRecord(source.Base),
            IsVirtual = source.IsVirtual,
            IsPublic = source.IsPublic,
            PointerAdjustment = source.PointerAdjustment,
        };
    }

    private FieldModel CloneField(FieldModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            UsesIntrusiveHandleReferenceStorage = source.UsesIntrusiveHandleReferenceStorage,
            BitWidth = source.BitWidth,
            BitOffset = source.BitOffset,
            IsSignedBitField = source.IsSignedBitField,
            IsReadOnlyBitField = source.IsReadOnlyBitField,
            IsManagedStoragePrivate = source.IsManagedStoragePrivate,
            ManagedReadOnlyPropertyName = source.ManagedReadOnlyPropertyName,
            CSharpTemplateType = source.CSharpTemplateType,
            CppTemplateType = source.CppTemplateType,
            DescriptionItems = CloneRootDescriptions(Required(source.DescriptionItems)),
            Offset = source.Offset,
            Size = source.Size,
            Alignment = source.Alignment,
            Name = source.Name,
            Type = CloneType(source.Type),
        };
    }

    private MethodModel CloneMethod(MethodModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            NativeDefaultArguments = Copy(Required(source.NativeDefaultArguments)),
            OverloadPriority = source.OverloadPriority,
            DescriptionItems = CloneRootDescriptions(Required(source.DescriptionItems)),
            NativeExportName = source.NativeExportName,
            NativeMethodName = source.NativeMethodName,
            ReturnTypeDescriptionItems = CloneDescriptions(Required(source.ReturnTypeDescriptionItems)),
            NoExceptions = source.NoExceptions,
            IsConst = source.IsConst,
            IsVolatile = source.IsVolatile,
            RefQualifier = source.RefQualifier,
            IsStatic = source.IsStatic,
            ReturnType = CloneType(source.ReturnType),
            ReturnSelf = source.ReturnSelf,
            MethodName = source.MethodName,
            Type = source.Type,
            Parameters = Copy(Required(source.Parameters).Select(CloneParameter)),
        };
    }

    private ParameterModel CloneParameter(ParameterModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            IsPlacementAllocator = source.IsPlacementAllocator,
            CppDefaultValue = source.CppDefaultValue,
            DescriptionItems = CloneDescriptions(Required(source.DescriptionItems)),
            Type = CloneType(source.Type),
            Name = source.Name,
        };
    }

    private TypeModel CloneType(TypeModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_types.TryGetValue(source, out var existing))
        {
            return existing;
        }

        var clone = CreateTypeShell(source);
        CompleteType(source, clone);
        return clone;
    }

    private TypeModel CreateTypeShell(TypeModel source)
    {
        if (_types.TryGetValue(source, out var existing))
        {
            return existing;
        }

        ArgumentNullException.ThrowIfNull(source.Transport);
        var clone = new TypeModel()
        {
            CppTypeName = source.CppTypeName,
            CSharpPInvokeType = CloneDataType(source.CSharpPInvokeType),
            CSharpPublicType = CloneDataType(source.CSharpPublicType),
            CppValueTypeName = source.CppValueTypeName,
            IsRecord = source.IsRecord,
            IsIntrusiveHandle = source.IsIntrusiveHandle,
            IntrusiveHandleElementType = source.IntrusiveHandleElementType,
            IntrusiveHandleElementCppType = source.IntrusiveHandleElementCppType,
            Transport = new(source.Transport.ValueIsConst, Required(source.Transport.Indirections)),
            RequiredHeaders = Copy(Required(source.RequiredHeaders)),
        };
        _types.Add(source, clone);
        return clone;
    }

    private void CompleteType(TypeModel source, TypeModel clone)
    {
        clone.ReferencedRecord = source.ReferencedRecord is null
            ? null
            : CloneRecord(source.ReferencedRecord);
    }

    private TemplateProjectionModel CloneTemplate(TemplateProjectionModel source)
    {
        return new()
        {
            ManagedPack = source.ManagedPack,
            FixedTypeName = source.FixedTypeName,
            NativeTemplateName = source.NativeTemplateName,
            NativeTypePattern = source.NativeTypePattern,
            FamilyName = source.FamilyName,
            DeclarationTypeName = source.DeclarationTypeName,
            ClosedTypeName = source.ClosedTypeName,
            Arguments = Copy(Required(source.Arguments).Select(CloneTemplateArgument)),
        };
    }

    private TemplateArgumentProjection CloneTemplateArgument(TemplateArgumentProjection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            ReferencedRecord = source.ReferencedRecord is null ? null : CloneRecord(source.ReferencedRecord),
            ParameterName = source.ParameterName,
            NativeArgument = source.NativeArgument,
            ClosedCSharpType = source.ClosedCSharpType,
            Kind = source.Kind,
        };
    }

    private static EnumModel CloneEnum(EnumModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            DescriptionItems = CloneRootDescriptions(Required(source.DescriptionItems)),
            Name = source.Name,
            SourceType = source.SourceType,
            UnderlyingType = CloneDataType(source.UnderlyingType),
            Members = Copy(Required(source.Members).Select(CloneEnumMember)),
        };
    }

    private static EnumMemberModel CloneEnumMember(EnumMemberModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            DescriptionItems = CloneRootDescriptions(Required(source.DescriptionItems)),
            Name = source.Name,
            Value = new CustomExpression(Required(source.Value).ToCode()),
        };
    }

    private static DataType CloneDataType(DataType source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Type);
        return new(source.Type.ToCode())
        {
            StorageKind = source.StorageKind,
            IsArray = source.IsArray,
            PointCounter = source.PointCounter,
        };
    }

    private static ReadOnlyCollection<IRootDescriptionItem> CloneRootDescriptions(
        IEnumerable<IRootDescriptionItem> source)
    {
        return Copy(source.Select(static description =>
            (IRootDescriptionItem)new DescriptionCustom(RenderDescription(description))));
    }

    private static ReadOnlyCollection<IDescriptionItem> CloneDescriptions(IEnumerable<IDescriptionItem> source)
    {
        return Copy(source.Select(static description =>
            (IDescriptionItem)new DescriptionCustom(RenderDescription(description))));
    }

    private static string RenderDescription(IToDescription source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var builder = new SourceBuilder();
        try
        {
            source.ToDescription(ref builder);
            return builder.ToCode();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source)
    {
        return Array.AsReadOnly(source.ToArray());
    }

    private static T Required<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
}