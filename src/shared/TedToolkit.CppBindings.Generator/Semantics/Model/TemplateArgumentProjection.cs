// -----------------------------------------------------------------------
// <copyright file="TemplateArgumentProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Stores the managed projection of one native template argument.
/// </summary>
public sealed class TemplateArgumentProjection
{
    /// <summary>
    /// Gets or sets the record required when this argument is exposed as a managed generic type.
    /// </summary>
    public RecordModel? ReferencedRecord { get; set; }

    /// <summary>
    /// Gets the source template parameter name.
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// Gets the native argument spelling used for fixed identity and diagnostics.
    /// </summary>
    public required string NativeArgument { get; init; }

    /// <summary>
    /// Gets the closed managed argument when this is a generic type parameter.
    /// </summary>
    public string ClosedCSharpType { get; set; } = "";

    /// <summary>
    /// Gets how the argument participates in the managed type identity.
    /// </summary>
    public required TemplateArgumentProjectionKind Kind { get; init; }
}