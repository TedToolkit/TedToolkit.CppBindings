// -----------------------------------------------------------------------
// <copyright file="HeaderRecordDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.RoslynHelper.Syntaxes;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Reuses existing object identities and proves new value layouts requested by header functions.
/// </summary>
/// <param name="existingDeclarations">The record declarations already supplied to the semantic model.</param>
internal sealed class HeaderRecordDiscovery(IReadOnlyList<BindingDeclaration> existingDeclarations)
{
    private readonly Dictionary<string, BindingDeclaration> _existing = existingDeclarations.ToDictionary(
        static declaration => declaration.Type.NativeName.TrimStart(':'), StringComparer.Ordinal);

    private readonly Dictionary<string, (RecordModel Layout, string NativeType)> _layouts = new(StringComparer.Ordinal);

    private readonly HashSet<string> _referenced = new(StringComparer.Ordinal);

    /// <summary>
    /// Resolves a compiler record to its unique shared layout model.
    /// </summary>
    /// <param name="record">The compiler record type.</param>
    /// <param name="context">The signature or field using the type.</param>
    /// <returns>The shared record model.</returns>
    /// <exception cref="InvalidOperationException">The compiler evidence disagrees with the existing layout.</exception>
    /// <exception cref="NotSupportedException">The record has no complete supported layout.</exception>
    internal RecordModel Resolve(RecordType record, string context)
    {
        var native = record.Handle.UnqualifiedType;
        var name = native.Spelling.CString.TrimStart(':');
        if (_existing.TryGetValue(name, out var declaration))
        {
            if (native.SizeOf > 0 && (native.SizeOf != declaration.Layout.Size || native.AlignOf != declaration.Layout.Alignment))
            {
                throw new InvalidOperationException($"'{context}' disagrees with the existing layout for '{name}'.");
            }

            _ = _referenced.Add(name);
            return declaration.Record;
        }

        if (_layouts.TryGetValue(name, out var layout))
        {
            return layout.Layout;
        }

        if (record.Decl.Definition is not CXXRecordDecl definition || native.SizeOf <= 0)
        {
            throw new NotSupportedException($"'{context}' needs the complete declaration of '{name}'.");
        }

        if (!native.IsPODType || definition.IsUnion || definition.NumBases != 0
            || native.AlignOf is not (1 or 2 or 4 or 8) || !HeaderCompilerDiscovery.IsManagedIdentifier(definition.Name))
        {
            throw new NotSupportedException($"'{context}' needs a proved value layout or an existing binding for '{name}'.");
        }

        return CreateLayout(definition, native, name);
    }

    /// <summary>
    /// Completes the semantic roots, including provider records reached from the header signatures.
    /// </summary>
    /// <param name="sourceGroup">The managed source group for newly proved layouts.</param>
    /// <returns>The unified declaration inventory.</returns>
    /// <exception cref="InvalidOperationException">Two declarations project to the same managed layout name.</exception>
    internal BindingDeclaration[] CompleteDeclarations(string sourceGroup)
    {
        var names = existingDeclarations.SelectMany(static declaration =>
            new[] { declaration.Record.Type.CSharpTypeName, declaration.Record.Type.CSharpInterfaceName, })
            .ToHashSet(StringComparer.Ordinal);
        _ = names.Add("NativeApi");
        foreach (var (layout, _) in _layouts.Values)
        {
            if (!names.Add(layout.Type.CSharpTypeName) || !names.Add(layout.Type.CSharpInterfaceName))
            {
                throw new InvalidOperationException($"'{layout.Type.CppTypeName}' conflicts with an existing managed layout name.");
            }
        }

        return existingDeclarations.Select(IncludeReferencedRecord)
            .Concat(_layouts.Values.Select(entry => new BindingDeclaration(entry.Layout, sourceGroup, false, [])
                { IsHeaderLayout = true, }))
            .ToArray();
    }

    /// <summary>
    /// Emits build-time checks of Clang's admitted size, alignment, and accessible field offsets.
    /// </summary>
    /// <returns>The native layout assertions.</returns>
    internal string GetNativeLayoutAssertions()
    {
        var builder = new StringBuilder();
        foreach (var (record, name) in _layouts.Values.OrderBy(static entry => entry.NativeType, StringComparer.Ordinal))
        {
            _ = builder.Append("static_assert(std::is_standard_layout<").Append(name).Append(">::value")
                .Append(" && std::is_trivially_copyable<").Append(name).Append(">::value")
                .Append(" && sizeof(").Append(name).Append(") == ").Append(record.Size)
                .Append(" && alignof(").Append(name).Append(") == ").Append(record.Alignment)
                .Append(", \"Header record layout mismatch\");\n");
            foreach (var field in record.FieldModels.Where(static field => !field.IsManagedStoragePrivate))
            {
                _ = builder.Append("static_assert(offsetof(").Append(name).Append(", ").Append(field.Name)
                    .Append(") == ").Append(field.Offset).Append(", \"Header field offset mismatch\");\n");
            }
        }

        return builder.ToString();
    }

    private RecordModel CreateLayout(CXXRecordDecl declaration, CXType native, string name)
    {
        declaration.Location.GetFileLocation(out var header, out _, out _, out _);
        var type = new DataType(declaration.Name);
        var model = new RecordModel()
        {
            Type = new()
            {
                CppTypeName = name,
                CppValueTypeName = name,
                CSharpPInvokeType = type,
                CSharpPublicType = type,
                IsRecord = true,
            },
            DescriptionItems = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            SourceHeader = header.Name.CString,
            Size = native.SizeOf,
            Alignment = native.AlignOf,
            ObjectKind = NativeObjectKind.Value,
            MethodModels = [],
        };
        _layouts.Add(name, (model, HeaderCompilerDiscovery.NativeTypeSpelling(native)));
        model.Type.ReferencedRecord = model;
        model.FieldModels = declaration.Fields.Select(field => ProjectField(field, name)).ToArray();
        return model;
    }

    private FieldModel ProjectField(FieldDecl field, string recordName)
    {
        var native = field.Type.Handle.CanonicalType;
        var offset = field.Handle.OffsetOfField;
        if (field.IsBitField || offset < 0 || offset % 8 != 0 || native.SizeOf <= 0 || native.AlignOf <= 0
            || !HeaderCompilerDiscovery.IsManagedIdentifier(field.Name))
        {
            throw new NotSupportedException($"'{recordName}.{field.Name}' has no supported sequential field layout.");
        }

        var type = HeaderCompilerDiscovery.ProjectType(field.Type, recordName + "." + field.Name, this);
        if (type.Transport.FirstIndirection is TypeIndirectionKind.LValueReference)
        {
            throw new NotSupportedException($"'{recordName}.{field.Name}' uses reference storage.");
        }

        return new()
        {
            Name = field.Name,
            Offset = offset / 8,
            Size = native.SizeOf,
            Alignment = native.AlignOf,
            IsManagedStoragePrivate = native.IsConstQualified || field.Access != CX_CXXAccessSpecifier.CX_CXXPublic,
            DescriptionItems = [],
            Type = type,
        };
    }

    private BindingDeclaration IncludeReferencedRecord(BindingDeclaration declaration)
    {
        return declaration.IsRoot || !_referenced.Contains(declaration.Type.NativeName.TrimStart(':'))
            ? declaration
            : new BindingDeclaration(declaration.Record, declaration.ManagedSourceGroup, false, declaration.Dependencies)
                { IsHeaderRoot = true, };
    }
}