// -----------------------------------------------------------------------
// <copyright file="DeclOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Options;

/// <summary>
/// Describes a declaration name to include during generation.
/// </summary>
public sealed record DeclOptions
{
    /// <summary>
    /// Gets the output file name for the declaration.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclOptions"/> class.
    /// </summary>
    /// <param name="enum">The declaration enum value.</param>
    public DeclOptions(Enum @enum)
    {
        ArgumentNullException.ThrowIfNull(@enum);
        FileName = @enum.ToString();
    }

    public DeclOptions(string fileName)
    {
        FileName = fileName;
    }
}