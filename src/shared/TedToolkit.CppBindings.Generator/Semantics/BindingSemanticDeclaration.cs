// -----------------------------------------------------------------------
// <copyright file="BindingSemanticDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Represents one declaration admitted into Shared's normalized dependency closure.
/// </summary>
public sealed class BindingSemanticDeclaration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingSemanticDeclaration"/> class.
    /// </summary>
    /// <param name="source">The provider declaration admitted into the closure.</param>
    /// <param name="type">The normalized type projection.</param>
    internal BindingSemanticDeclaration(BindingDeclaration source, BindingTypeProjection type)
    {
        Source = source;
        Type = type;
    }

    /// <summary>
    /// Gets the normalized native-to-managed type projection.
    /// </summary>
    public BindingTypeProjection Type { get; }

    /// <summary>
    /// Gets the compiler-proved native layout.
    /// </summary>
    public BindingLayoutDescriptor Layout
    {
        get
        {
            return Source.Layout;
        }
    }

    /// <summary>
    /// Gets the admitted template projection, when present.
    /// </summary>
    public BindingTemplateDescriptor? Template
    {
        get
        {
            return Source.Template;
        }
    }

    /// <summary>
    /// Gets the managed source-group identity.
    /// </summary>
    public string ManagedSourceGroup
    {
        get
        {
            return Source.ManagedSourceGroup;
        }
    }

    /// <summary>
    /// Gets the native dependency identities.
    /// </summary>
    public IReadOnlyList<string> Dependencies
    {
        get
        {
            return Source.Dependencies;
        }
    }

    /// <summary>
    /// Gets the native exports owned by this declaration.
    /// </summary>
    public IReadOnlyList<string> NativeExports
    {
        get
        {
            return Source.NativeExports;
        }
    }

    /// <summary>
    /// Gets the normalized source declaration used by both Shared emitters.
    /// </summary>
    internal BindingDeclaration Source { get; }
}