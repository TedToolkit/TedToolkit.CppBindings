// -----------------------------------------------------------------------
// <copyright file="BindingManagedEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.CppBindings.Generator.Semantics.BindingManagedEmitter>;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Produces the generated C# partial struct for a normalized C++ record.
/// </summary>
/// <param name="recordDecl">The record declaration being generated.</param>
/// <param name="emissionProfile">The provider's finite emission policy.</param>
/// <param name="recordCatalog">The completed record models used to classify record results.</param>
/// <param name="nativeFunctionIndices">The function-table indices keyed by native export name.</param>
/// <param name="generateRepresentation">Whether to emit the shared managed representation for this record's family.</param>
public sealed class BindingManagedEmitter(
    RecordModel recordDecl,
    BindingEmissionProfile emissionProfile,
    IReadOnlyDictionary<string, RecordModel>? recordCatalog = null,
    IReadOnlyDictionary<string, int>? nativeFunctionIndices = null,
    bool generateRepresentation = true) : IBindingSourceEmitter
{
    /// <inheritdoc />
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nameSpace = NameSpace(emissionProfile.CSharpNamespace);
        AddTo(nameSpace);
        return File()
            .AddNameSpace(nameSpace)
            .ToCode();
    }

    /// <summary>
    /// Adds this record's managed declarations to an existing namespace.
    /// </summary>
    /// <param name="nameSpace">The namespace shared by one managed source group.</param>
    /// <exception cref="ArgumentNullException"><paramref name="nameSpace"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The record requires an unproved managed alignment.</exception>
    internal void AddTo(NameSpace nameSpace)
    {
        ArgumentNullException.ThrowIfNull(nameSpace);
        if (!generateRepresentation)
        {
            new BindingManagedExtensionComposer(
                    recordDecl,
                    emissionProfile,
                    recordCatalog,
                    nativeFunctionIndices)
                .AddTo(nameSpace);
            return;
        }

        var templateProjection = recordDecl.TemplateProjection;
        var structName = templateProjection?.FamilyName ?? recordDecl.Type.CSharpPublicType.ToCode();
        var structDeclaration = Struct(structName).Unsafe
            .AddAttribute(Attribute(new DataType("global::TedToolkit.CppBindings.NativeTypeNameAttribute"))
                .AddArgument(Argument((templateProjection?.NativeTypePattern
                                       ?? recordDecl.Type.CppTypeName).ToLiteral())));
        var layoutAttribute = Attribute<StructLayoutAttribute>()
            .AddArgument(Argument(LayoutKind.Sequential.ToExpression()));
        if (templateProjection is null)
        {
            layoutAttribute.AddNamedArgument(nameof(StructLayoutAttribute.Size), recordDecl.Size.ToLiteral());
            if (recordDecl.Alignment > 0)
            {
                if (recordDecl.Alignment is not (1 or 2 or 4 or 8))
                {
                    throw new NotSupportedException(
                        $"Managed alignment is not proved for {recordDecl.Type.CppTypeName}: {recordDecl.Alignment}.");
                }

                layoutAttribute.AddNamedArgument(nameof(StructLayoutAttribute.Pack), recordDecl.Alignment.ToLiteral());
            }
        }
        else
        {
            layoutAttribute.AddNamedArgument(nameof(StructLayoutAttribute.Pack), templateProjection.ManagedPack.ToLiteral());
        }

        structDeclaration.AddAttribute(layoutAttribute);
        AddTemplateParameters(structDeclaration);
        AddRootDescriptions(structDeclaration, recordDecl.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));

        structDeclaration = emissionProfile.IsInternal
            ? structDeclaration.Internal
            : structDeclaration.Public;
        GenerateFields(structDeclaration);
        nameSpace.AddMember(structDeclaration);
        GenerateInterfaces(nameSpace, structDeclaration);
        new BindingManagedExtensionComposer(
                recordDecl,
                emissionProfile,
                recordCatalog,
                nativeFunctionIndices)
            .AddTo(nameSpace);
    }

    private void GenerateInterfaces(NameSpace nameSpace, TypeDeclaration? structDeclaration)
    {
        if (recordDecl.Type.CSharpInterfaceName == emissionProfile.IntrusiveRootInterfaceName)
        {
            structDeclaration?.AddBaseType(new DataType(emissionProfile.ManagedIntrusiveRootInterface));
            return;
        }

        var interfaceName = GetOpenInterfaceName(recordDecl);
        var interfaceDeclaration = Interface(recordDecl.TemplateProjection?.FamilyName is { } familyName
                ? "I" + familyName
                : recordDecl.Type.CSharpInterfaceName)
            .Unsafe;
        interfaceDeclaration = emissionProfile.IsInternal
            ? interfaceDeclaration.Internal
            : interfaceDeclaration.Public;
        AddTemplateParameters(interfaceDeclaration);
        structDeclaration?.AddBaseType(new DataType(interfaceName));
        foreach (var baseRelation in recordDecl.Bases.Where(relation =>
                     relation.IsPublic
                     && recordCatalog?.ContainsKey(relation.Base.Type.CppTypeName) is not false))
        {
            interfaceDeclaration.AddBaseType(new DataType(
                baseRelation.Base.Type.CSharpInterfaceName == emissionProfile.IntrusiveRootInterfaceName
                    ? emissionProfile.ManagedIntrusiveRootInterface
                    : baseRelation.Base.Type.CSharpInterfaceName));
        }

        if (recordDecl.ObjectKind is NativeObjectKind.IntrusiveHandle
            && !recordDecl.Bases.Any(relation => relation.IsPublic
                && relation.Base.UsesIntrusiveReferenceCounting
                && recordCatalog?.ContainsKey(relation.Base.Type.CppTypeName) is not false))
        {
            interfaceDeclaration.AddBaseType(new DataType(emissionProfile.ManagedIntrusiveRootInterface));
        }
        else if (recordDecl.ObjectKind is NativeObjectKind.Owned)
        {
            interfaceDeclaration.AddBaseType(new DataType("global::TedToolkit.CppBindings.ICppRaii"));
        }

        if (recordDecl.ObjectKind is NativeObjectKind.IntrusiveHandle
            && recordDecl.MethodModels.Any(static method =>
                method.Type is MethodModelType.VALUE_DELETE))
        {
            interfaceDeclaration.AddBaseType(new DataType("global::TedToolkit.CppBindings.ICppRaii"));
        }

        nameSpace.AddMember(interfaceDeclaration);
    }

    private void AddTemplateParameters(TypeDeclaration declaration)
    {
        if (recordDecl.TemplateProjection is null)
        {
            return;
        }

        foreach (var argument in recordDecl.TemplateProjection.GenericArguments)
        {
            declaration.AddTypeParameter(new TypeParameter(argument.ParameterName).AddUnmanagedConstraint());
        }
    }

    private static string GetOpenInterfaceName(RecordModel record)
    {
        return record.TemplateProjection is null
            ? record.Type.CSharpInterfaceName
            : "I" + record.TemplateProjection.DeclarationTypeName;
    }

    private void GenerateFields(TypeDeclaration structDeclaration)
    {
        if (recordDecl.TemplateProjection is not null)
        {
            foreach (var field in recordDecl.FieldModels.OrderBy(static field => field.Offset))
            {
                AddField(structDeclaration, field);
            }

            return;
        }

        var currentOffset = 0L;
        var paddingIndex = 0;
        var managedAlignment = 1L;
        foreach (var group in GetPhysicalFieldGroups())
        {
            managedAlignment = Math.Max(managedAlignment,
                AddPhysicalFieldGroup(structDeclaration, group, ref currentOffset, ref paddingIndex));
        }

        managedAlignment = Math.Max(managedAlignment,
            AddPadding(structDeclaration, ref currentOffset, recordDecl.Size, ref paddingIndex));
        if (currentOffset == recordDecl.Size && (recordDecl.Alignment is 0 || managedAlignment == recordDecl.Alignment))
        {
            return;
        }

        throw new NotSupportedException($"Sequential storage is not proved for {recordDecl.Type.CppTypeName}.");
    }

    private IEnumerable<IReadOnlyList<FieldModel>> GetPhysicalFieldGroups()
    {
        var fields = recordDecl.FieldModels.Where(static field => field.BitWidth is not 0)
            .OrderBy(static field => field.Offset).ToArray();
        for (var index = 0; index < fields.Length;)
        {
            var group = new List<FieldModel>();
            var end = checked(fields[index].Offset + fields[index].Size);
            do
            {
                var field = fields[index++];
                group.Add(field);
                end = Math.Max(end, checked(field.Offset + field.Size));
            }
            while (index < fields.Length && fields[index].Offset < end);

            yield return group;
        }
    }

    private long AddPhysicalFieldGroup(
        TypeDeclaration declaration,
        IReadOnlyList<FieldModel> group,
        ref long currentOffset,
        ref int paddingIndex)
    {
        if (group.Count > 1 && group.Any(static field => !field.BitWidth.HasValue))
        {
            return AddOverlappingFields(declaration, group, ref currentOffset, ref paddingIndex);
        }

        var managedAlignment = 1L;
        var bitfieldUnits = new HashSet<(long Offset, long Size)>();
        foreach (var fieldDecl in group)
        {
            if (fieldDecl.BitWidth.HasValue && !bitfieldUnits.Add((fieldDecl.Offset, fieldDecl.Size)))
            {
                AddBitFieldProperty(declaration, fieldDecl);
                continue;
            }

            var fieldAlignment = recordDecl.Alignment > 0
                ? Math.Min(recordDecl.Alignment, fieldDecl.Alignment)
                : Math.Max(fieldDecl.Alignment, 1);
            if (fieldDecl.Offset < currentOffset || fieldDecl.Offset % fieldAlignment != 0)
            {
                throw new NotSupportedException(
                    $"Sequential field placement is not proved for {recordDecl.Type.CppTypeName}.{fieldDecl.Name}.");
            }

            managedAlignment = Math.Max(managedAlignment,
                AddPadding(declaration, ref currentOffset, fieldDecl.Offset, ref paddingIndex));
            managedAlignment = Math.Max(managedAlignment, fieldAlignment);
            if (fieldDecl.BitWidth.HasValue)
            {
                declaration.AddMember(Field(new DataType(GetUnsignedStorageType(fieldDecl.Size)),
                    GetBitFieldStorageName(fieldDecl)).Private);
                AddBitFieldProperty(declaration, fieldDecl);
            }
            else
            {
                AddField(declaration, fieldDecl);
            }

            currentOffset = checked(fieldDecl.Offset + fieldDecl.Size);
        }

        return managedAlignment;
    }

    private long AddOverlappingFields(
        TypeDeclaration declaration,
        IReadOnlyList<FieldModel> fields,
        ref long currentOffset,
        ref int paddingIndex)
    {
        var start = fields[0].Offset;
        var end = fields.Max(static field => checked(field.Offset + field.Size));
        var alignment = fields.Max(field => Math.Min(Math.Max(recordDecl.Alignment, 1), field.Alignment));
        if (start < currentOffset || end > recordDecl.Size || alignment is not (1 or 2 or 4 or 8)
            || start % alignment != 0 || end - start < alignment)
        {
            throw new NotSupportedException($"Overlapping storage is not proved for {recordDecl.Type.CppTypeName}.");
        }

        var managedAlignment = AddPadding(declaration, ref currentOffset, start, ref paddingIndex);
        var storage = $"__overlap{start}";
        while (recordDecl.FieldModels.Any(field => field.Name == storage))
        {
            storage += "_";
        }

        var storageType = GetUnsignedStorageType(alignment);
        declaration.AddMember(Field(new DataType(storageType), storage).Private);
        currentOffset = checked(start + alignment);
        managedAlignment = Math.Max(managedAlignment,
            AddPadding(declaration, ref currentOffset, end, ref paddingIndex));
        var viewIndex = 0;
        foreach (var field in fields)
        {
            var reference = $"global::System.Runtime.CompilerServices.Unsafe.As<{storageType}, byte>("
                            + $"ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in {storage}))";
            if (field.Offset != start)
            {
                reference = $"global::System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref {reference}, "
                            + $"(nint){field.Offset - start})";
            }

            if (field.BitWidth.HasValue)
            {
                AddBitFieldProperty(declaration, field,
                    $"global::System.Runtime.CompilerServices.Unsafe.As<byte, {GetUnsignedStorageType(field.Size)}>(ref {reference})");
            }
            else
            {
                AddOverlappingReferenceProperty(declaration, field, reference, viewIndex++);
            }
        }

        return Math.Max(managedAlignment, alignment);
    }

    private void AddOverlappingReferenceProperty(TypeDeclaration declaration, FieldModel field, string reference, int viewIndex)
    {
        var type = field.Type.CSharpPInvokeType.ToCode();
        var transport = field.Type.Transport;
        var readOnly = transport.Indirections.Count is 0
            ? transport.ValueIsConst
            : transport.Indirections[0].IsConstQualified;
        var target = $"global::System.Runtime.CompilerServices.Unsafe.As<byte, {type}>(ref {reference})";
        if (type.Contains('*', StringComparison.Ordinal))
        {
            // Pointer types cannot be generic arguments; the nested field preserves an interior reference.
            var viewName = $"__overlapView{field.Offset}_{viewIndex}";
            while (recordDecl.FieldModels.Any(candidate => candidate.Name == viewName))
            {
                viewName += "_";
            }

            declaration.AddMember(Struct(viewName).Private.Unsafe
                .AddMember(Field(field.Type.CSharpPInvokeType, "Value").Public));
            target = $"global::System.Runtime.CompilerServices.Unsafe.As<byte, {viewName}>(ref {reference}).Value";
        }

        var modifier = readOnly ? "ref readonly " : "ref ";
        var property = Property(new DataType(modifier + type), field.Name).Public
            .AddAttribute(Attribute(new DataType("global::System.Diagnostics.CodeAnalysis.UnscopedRefAttribute")))
            .AddAttribute(Attribute(new DataType("global::TedToolkit.CppBindings.NativeTypeNameAttribute"))
                .AddArgument(Argument(field.Type.CppTypeName.ToLiteral())));
        property.IsReadonly = true;
        var getter = Accessor(AccessorType.GET);
        getter.Statements.Add(new Custom($"return ref {target};"));
        property.AddAccessor(getter);
        AddRootDescriptions(property, field.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        property.AddRootDescription(new DescriptionRemarks([
            new DescriptionText("This reference aliases overlapping native storage without copying or changing ownership. "
                + "The caller must obey native active-member, construction, destruction, owner-lifetime and invalidation rules."),
        ]));
        declaration.AddMember(property);
    }

    private static string GetUnsignedStorageType(long size)
    {
        return size switch
        {
            1 => "byte",
            2 => "ushort",
            4 => "uint",
            8 => "ulong",
            _ => throw new NotSupportedException($"Bitfield allocation-unit size is not proved: {size}."),
        };
    }

    private string GetBitFieldStorageName(FieldModel field)
    {
        var name = $"__bits{field.Offset}";
        while (recordDecl.FieldModels.Any(candidate => candidate.Name == name))
        {
            name += "_";
        }

        return name;
    }

    private void AddBitFieldProperty(TypeDeclaration declaration, FieldModel field, string? storageReference = null)
    {
        var width = field.BitWidth!.Value;
        if (width <= 0 || width > field.Size * 8 || field.BitOffset < 0 || field.BitOffset + width > field.Size * 8)
        {
            throw new NotSupportedException($"Bitfield range is not proved for {recordDecl.Type.CppTypeName}.{field.Name}.");
        }

        if (string.IsNullOrEmpty(field.Name))
        {
            return;
        }

        var storage = storageReference ?? GetBitFieldStorageName(field);
        var storageType = GetUnsignedStorageType(field.Size);
        var type = field.Type.CSharpPInvokeType.ToCode();
        var numericType = type switch
        {
            var name when name == DataType.FromType<CLong>().ToCode() => "int",
            var name when name == DataType.FromType<CULong>().ToCode() => "uint",
            _ => type,
        };
        var mask = width is 64 ? ulong.MaxValue : (1UL << width) - 1;
        var bits = $"(((ulong){storage} >> {field.BitOffset}) & {mask}UL)";
        var read = (field.IsSignedBitField, numericType) switch
        {
            (true, _) => $"unchecked(({numericType})((long)({bits} << {64 - width}) >> {64 - width}))",
            (_, "bool") => $"{bits} != 0",
            _ => $"unchecked(({numericType}){bits})",
        };
        if (numericType != type)
        {
            read = $"new {type}({read})";
        }

        var property = Property(field.Type.CSharpPInvokeType, field.Name).Public
            .AddAttribute(Attribute(new DataType("global::TedToolkit.CppBindings.NativeTypeNameAttribute"))
                .AddArgument(Argument(field.Type.CppTypeName.ToLiteral())));
        property.IsReadonly = field.IsReadOnlyBitField;
        var getter = Accessor(AccessorType.GET);
        getter.IsReadonly = !field.IsReadOnlyBitField;
        getter.Statements.Add(new Custom($"return {read};"));
        property.AddAccessor(getter);
        if (!field.IsReadOnlyBitField)
        {
            var numericValue = numericType != type ? "value.Value" : "value";
            var value = type is "bool" ? "(value ? 1UL : 0UL)" : $"unchecked((ulong){numericValue})";
            var setter = Accessor(AccessorType.SET);
            setter.Statements.Add(new Custom(
                $"{storage} = unchecked(({storageType})(((ulong){storage} & ~({mask}UL << {field.BitOffset}))"
                + $" | (({value} & {mask}UL) << {field.BitOffset})));"));
            property.AddAccessor(setter);
        }

        AddRootDescriptions(property, field.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        declaration.AddMember(property);
    }

    private void AddField(TypeDeclaration structDeclaration, FieldModel fieldModel)
    {
        if (fieldModel.UsesIntrusiveHandleReferenceStorage)
        {
            AddHandleReferenceProperty(structDeclaration, fieldModel);
            return;
        }

        var managedType = recordDecl.TemplateProjection is null || string.IsNullOrEmpty(fieldModel.CSharpTemplateType)
            ? fieldModel.Type.CSharpPInvokeType
            : new DataType(fieldModel.CSharpTemplateType);
        var nativeType = recordDecl.TemplateProjection is not null
                         && !string.IsNullOrEmpty(fieldModel.CppTemplateType)
            ? fieldModel.CppTemplateType
            : fieldModel.Type.CppTypeName;
        var field = Field(managedType, fieldModel.Name)
            .AddAttribute(Attribute(new DataType("global::TedToolkit.CppBindings.NativeTypeNameAttribute"))
                .AddArgument(Argument(nativeType.ToLiteral())));
        if (!fieldModel.IsManagedStoragePrivate)
        {
            field = field.Public;
        }
        else
        {
            field = field.Private;
        }

        AddRootDescriptions(field, fieldModel.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        structDeclaration.AddMember(field);
        if (fieldModel.ManagedReadOnlyPropertyName is not null)
        {
            var property = Property(managedType, fieldModel.ManagedReadOnlyPropertyName).Public;
            property.IsReadonly = true;
            var getter = Accessor(AccessorType.GET);
            getter.Statements.Add(new Custom($"return {fieldModel.Name};"));
            property.AddAccessor(getter);
            AddRootDescriptions(property, fieldModel.DescriptionItems, static (target, description) =>
                target.AddRootDescription(description));
            structDeclaration.AddMember(property);
        }
    }

    private void AddHandleReferenceProperty(TypeDeclaration declaration, FieldModel field)
    {
        if (field.Size is not 8 || field.Alignment is not 8 || !field.Type.IsIntrusiveHandle)
        {
            throw new NotSupportedException($"Cyclic handle storage is not proved for {recordDecl.Type.CppTypeName}.{field.Name}.");
        }

        var storage = $"__handle{field.Offset}";
        while (recordDecl.FieldModels.Any(candidate => candidate.Name == storage))
        {
            storage += "_";
        }

        declaration.AddMember(Field(new DataType("nint"), storage).Private);
        var type = field.Type.CSharpPInvokeType.ToCode();
        var readOnly = field.Type.Transport.ValueIsConst;
        var modifier = readOnly ? "ref readonly " : "ref ";
        var property = Property(new DataType(modifier + type), field.Name).Public
            .AddAttribute(Attribute(new DataType("global::System.Diagnostics.CodeAnalysis.UnscopedRefAttribute")))
            .AddAttribute(Attribute(new DataType("global::TedToolkit.CppBindings.NativeTypeNameAttribute"))
                .AddArgument(Argument(field.Type.CppTypeName.ToLiteral())));
        property.IsReadonly = true;
        var storageReference = $"global::System.Runtime.CompilerServices.Unsafe.AsRef(in {storage})";
        var getter = Accessor(AccessorType.GET);
        getter.Statements.Add(new Custom(
            $"return ref global::System.Runtime.CompilerServices.Unsafe.As<nint, {type}>(ref {storageReference});"));
        property.AddAccessor(getter);
        AddRootDescriptions(property, field.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        property.AddRootDescription(new DescriptionRemarks([
            new DescriptionText("This reference aliases native handle storage and neither retains nor releases its target. "
                + "The caller must preserve the original owner's lifetime and obey native invalidation rules."),
        ]));
        declaration.AddMember(property);
    }

    private long AddPadding(
        TypeDeclaration structDeclaration,
        ref long currentOffset,
        long targetOffset,
        ref int paddingIndex)
    {
        var managedAlignment = 1L;
        while (currentOffset < targetOffset)
        {
            var alignment = Math.Max(recordDecl.Alignment, 1);
            var unit = alignment;
            while (currentOffset % unit != 0 || unit > targetOffset - currentOffset)
            {
                unit /= 2;
            }

            var count = (targetOffset - currentOffset) / unit;
            if (currentOffset % alignment != 0)
            {
                count = Math.Min(count, (alignment - (currentOffset % alignment)) / unit);
            }

            var type = unit switch
            {
                8 => "ulong",
                4 => "uint",
                2 => "ushort",
                _ => "byte",
            };
            var name = $"__padding{paddingIndex++}";
            structDeclaration.AddMember(count is 1
                ? Field(new DataType(type), name).Private
                : Field(new DataType("fixed " + type), $"{name}[{count}]").Private);
            currentOffset += unit * count;
            managedAlignment = Math.Max(managedAlignment, unit);
        }

        return managedAlignment;
    }

    private static void AddRootDescriptions<TTarget>(
        TTarget target,
        IReadOnlyList<IRootDescriptionItem> descriptions,
        Action<TTarget, IRootDescriptionItem> addDescription)
    {
        foreach (var description in descriptions)
        {
            addDescription(target, description);
        }
    }
}