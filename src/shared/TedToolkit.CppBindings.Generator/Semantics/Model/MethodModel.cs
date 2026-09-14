// -----------------------------------------------------------------------
// <copyright file="MethodModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Stores the normalized metadata for one generated method surface.
/// </summary>
public class MethodModel
{
    /// <summary>
    /// Gets native default expressions appended by an overload-priority projection.
    /// </summary>
    public IReadOnlyList<string> NativeDefaultArguments { get; init; } = [];

    /// <summary>
    /// Gets the declaration priority used when default arguments produce the same public signature.
    /// </summary>
    public int OverloadPriority { get; init; }

    /// <summary>
    /// Gets the readable native operation name.
    /// </summary>
    /// <returns>The operation name used by the C export.</returns>
    public string GetNativeOperationName()
    {
        if (Type is MethodModelType.NEW)
        {
            return "Create";
        }

        if (Type is MethodModelType.DELETE or MethodModelType.VALUE_DELETE)
        {
            return "Destroy";
        }

        if (Type is not MethodModelType.OPERATOR)
        {
            return MethodName;
        }

        var builder = new StringBuilder("Operator");
        var upperNext = false;
        foreach (var character in MethodName)
        {
            if (char.IsLetterOrDigit(character) || character is '_')
            {
                _ = builder.Append(upperNext ? char.ToUpperInvariant(character) : character);
                upperNext = false;
                continue;
            }

            upperNext = true;
            _ = builder.Append(character switch
            {
                ' ' => "",
                '=' => "Equal",
                '+' => "Plus",
                '-' => "Minus",
                '*' => "Star",
                '/' => "Slash",
                '%' => "Percent",
                '!' => "Bang",
                '<' => "Less",
                '>' => "Greater",
                '&' => "Ampersand",
                '|' => "Pipe",
                '^' => "Caret",
                '~' => "Tilde",
                '(' => "LeftParen",
                ')' => "RightParen",
                '[' => "LeftBracket",
                ']' => "RightBracket",
                ',' => "Comma",
                _ => "Char",
            });
        }

        return builder.ToString();
    }

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
    /// Gets the complete native C export name assigned by Model normalization.
    /// </summary>
    public string NativeExportName { get; set; } = "";

    /// <summary>
    /// Gets the exact C++ declaration name used for native invocation.
    /// </summary>
    public string NativeMethodName { get; init; } = "";

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
    /// Gets a value indicating whether the native method is volatile-qualified.
    /// </summary>
    public bool IsVolatile { get; init; }

    /// <summary>
    /// Gets the native member-function reference qualifier, if present.
    /// </summary>
    public string RefQualifier { get; init; } = "";

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