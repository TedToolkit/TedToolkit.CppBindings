// -----------------------------------------------------------------------
// <copyright file="TemplateProjectionModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

/// <summary>
/// Separates one managed template family from an exact native specialization.
/// </summary>
internal sealed class TemplateProjectionModel
{
    /// <summary>
    /// Gets or sets the common managed packing limit proved for all selected native specializations.
    /// </summary>
    public int ManagedPack { get; set; } = 8;

    /// <summary>
    /// Gets the former fully closed managed type name used as a replacement key.
    /// </summary>
    public required string FixedTypeName { get; init; }

    /// <summary>
    /// Gets the native template identity without concrete arguments.
    /// </summary>
    public required string NativeTemplateName { get; init; }

    /// <summary>
    /// Gets the native template pattern represented by the managed family.
    /// </summary>
    public required string NativeTypePattern { get; init; }

    /// <summary>
    /// Gets the non-generic managed family name, including every fixed argument.
    /// </summary>
    public required string FamilyName { get; init; }

    /// <summary>
    /// Gets the managed open type declaration.
    /// </summary>
    public required string DeclarationTypeName { get; init; }

    /// <summary>
    /// Gets the managed closed type used by this exact native specialization.
    /// </summary>
    public required string ClosedTypeName { get; set; }

    /// <summary>
    /// Gets the ordered argument projections.
    /// </summary>
    public required IReadOnlyList<TemplateArgumentProjection> Arguments { get; init; }

    /// <summary>
    /// Gets the ordered managed generic arguments.
    /// </summary>
    public IEnumerable<TemplateArgumentProjection> GenericArguments
    {
        get
        {
            return Arguments.Where(static argument => argument.Kind is TemplateArgumentProjectionKind.Generic);
        }
    }
}