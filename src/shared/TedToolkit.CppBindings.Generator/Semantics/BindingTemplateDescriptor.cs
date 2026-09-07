// -----------------------------------------------------------------------
// <copyright file="BindingTemplateDescriptor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one concrete or open template projection.
/// </summary>
public sealed class BindingTemplateDescriptor
{
    /// <summary>
    /// Initializes a new immutable template descriptor.
    /// </summary>
    /// <param name="familyName">The canonical template family name.</param>
    /// <param name="arguments">The ordered native template arguments.</param>
    /// <param name="isClosed">Whether every argument names a concrete native type.</param>
    public BindingTemplateDescriptor(string familyName, IEnumerable<string> arguments, bool isClosed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ArgumentNullException.ThrowIfNull(arguments);
        FamilyName = familyName;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        IsClosed = isClosed;
    }

    /// <summary>
    /// Gets the canonical template family name.
    /// </summary>
    public string FamilyName { get; }

    /// <summary>
    /// Gets the copied ordered native template arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets a value indicating whether every argument names a concrete native type.
    /// </summary>
    public bool IsClosed { get; }
}