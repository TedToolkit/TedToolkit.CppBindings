// -----------------------------------------------------------------------
// <copyright file="BindingDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Supplies one normalized record declaration to the Shared semantic engine.
/// </summary>
public sealed class BindingDeclaration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingDeclaration"/> class with copied graph identities.
    /// </summary>
    /// <param name="record">The normalized record, members, transport, layout, lifetime, and template facts.</param>
    /// <param name="managedSourceGroup">The semantic identity shared by declarations emitted into one managed source.</param>
    /// <param name="isRoot">Whether this declaration is an explicit provider root.</param>
    /// <param name="dependencies">Native type identities required by this declaration.</param>
    public BindingDeclaration(
        RecordModel record,
        string managedSourceGroup,
        bool isRoot,
        IEnumerable<string> dependencies)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedSourceGroup);
        ArgumentNullException.ThrowIfNull(dependencies);
        Record = record;
        Type = new(record.Type.CppTypeName);
        Layout = new(record.Type.CppTypeName, record.Size, record.Alignment);
        Template = record.TemplateProjection is { } projection
            ? new(
                projection.NativeTemplateName,
                projection.Arguments.Select(static argument => argument.NativeArgument).ToArray(),
                record.IsClosedTemplateSpecialization)
            : null;
        ManagedSourceGroup = managedSourceGroup;
        IsRoot = isRoot;
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        NativeExports = Array.AsReadOnly(NativeExportInventory.GetDeclarationExports(record));
    }

    /// <summary>
    /// Gets the normalized declaration used by both Shared emitters.
    /// </summary>
    public RecordModel Record { get; }

    /// <summary>
    /// Gets the provider-independent C++ type.
    /// </summary>
    public CppTypeDescriptor Type { get; }

    /// <summary>
    /// Gets the compiler-proved native layout.
    /// </summary>
    public BindingLayoutDescriptor Layout { get; }

    /// <summary>
    /// Gets the template projection, when present.
    /// </summary>
    public BindingTemplateDescriptor? Template { get; }

    /// <summary>
    /// Gets the semantic identity shared by declarations emitted into one managed source.
    /// </summary>
    public string ManagedSourceGroup { get; }

    /// <summary>
    /// Gets a value indicating whether this declaration is an explicit provider root.
    /// </summary>
    public bool IsRoot { get; }

    /// <summary>
    /// Gets the native identities required by this declaration.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// Gets the native exports derived from the normalized declaration.
    /// </summary>
    public IReadOnlyList<string> NativeExports { get; }
}