// -----------------------------------------------------------------------
// <copyright file="TypeModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Models.Types;

/// <summary>
/// Stores the projected type metadata used across native and managed generation.
/// </summary>
internal class TypeModel
{
    /// <summary>
    /// Gets or sets the projected C++ type used in generated <c>extern "C"</c> wrappers.
    /// </summary>
    public required string CppTypeName { get; init; }

    /// <summary>
    /// Gets or sets the projected C# type used for PInvoke and field layout generation.
    /// </summary>
    public required DataType CSharpPInvokeType { get; init; }

    /// <summary>
    /// Gets or sets the projected public C# API type.
    /// </summary>
    public required DataType CSharpPublicType { get; init; }

    /// <summary>
    /// Gets the additional native headers required to use the projected C++ type.
    /// </summary>
    public IReadOnlyList<string> RequiredHeaders { get; set; } = [];

    /// <summary>
    /// Gets the generated public C# type name.
    /// </summary>
    public string CSharpTypeName
    {
        get
        {
            return CSharpPublicType.ToCode();
        }
    }

    /// <summary>
    /// Gets the generated interface name for the projected public C# type.
    /// </summary>
    public string CSharpInterfaceName
    {
        get
        {
            return ZString.Concat("I", CSharpTypeName);
        }
    }
}