// -----------------------------------------------------------------------
// <copyright file="MethodModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models.Declarations;

/// <summary>
/// Stores the normalized metadata for one generated method surface.
/// </summary>
internal class MethodModel
{
    /// <summary>
    /// Gets a value indicating whether the method returns <c>void</c>.
    /// </summary>
    public bool IsReturnVoid
    {
        get
        {
            return ReturnType.CppTypeName is "void";
        }
    }

    /// <summary>
    /// Gets the XML documentation description items for the method.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets the XML documentation description items for the return value.
    /// </summary>
    public required IReadOnlyList<IDescriptionItem> ReturnTypeDescriptionItems { get; init; }

    /// <summary>
    /// Gets a value indicating whether the native method is declared as noexcept.
    /// </summary>
    public required bool NoExceptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether the native method is const-qualified.
    /// </summary>
    public required bool IsConst { get; init; }

    /// <summary>
    /// Gets a value indicating whether the native method is static.
    /// </summary>
    public required bool IsStatic { get; init; }

    /// <summary>
    /// Gets the projected return type.
    /// </summary>
    public required TypeModel ReturnType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the wrapper should treat the method as returning <c>self</c>.
    /// </summary>
    public bool ReturnSelf { get; init; }

    /// <summary>
    /// Gets the normalized method name.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Gets the normalized method kind.
    /// </summary>
    public required MethodModelType Type { get; init; }

    /// <summary>
    /// Gets the normalized method parameters.
    /// </summary>
    public required IReadOnlyList<ParameterModel> Parameters { get; init; }
}