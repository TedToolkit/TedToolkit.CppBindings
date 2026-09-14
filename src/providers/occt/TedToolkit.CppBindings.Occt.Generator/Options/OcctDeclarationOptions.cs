// -----------------------------------------------------------------------
// <copyright file="OcctDeclarationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator;

/// <summary>
/// Describes a declaration name to include during generation.
/// </summary>
public sealed record OcctDeclarationOptions
{
    /// <summary>
    /// Gets the output file name for the declaration.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OcctDeclarationOptions"/> class.
    /// </summary>
    /// <param name="enum">The declaration enum value.</param>
    public OcctDeclarationOptions(Enum @enum)
    {
        ArgumentNullException.ThrowIfNull(@enum);
        FileName = @enum.ToString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OcctDeclarationOptions"/> class.
    /// </summary>
    /// <param name="fileName">The declaration file name to include.</param>
    public OcctDeclarationOptions(string fileName)
    {
        FileName = fileName;
    }
}