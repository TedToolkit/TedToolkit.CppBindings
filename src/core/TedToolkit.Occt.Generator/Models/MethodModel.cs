// -----------------------------------------------------------------------
// <copyright file="MethodModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the normalized metadata for one generated method surface.
/// </summary>
internal partial class MethodModel
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

    /// <summary>
    /// Gets the managed invoke name for the method.
    /// </summary>
    /// <returns>The normalized invoke name.</returns>
    public string GetInvokeName()
    {
        if (Type is not (MethodModelType.Operator or MethodModelType.Implicit or MethodModelType.Explicit))
        {
            return MethodName;
        }

        using var builder = ZString.CreateStringBuilder();
        var upperNext = false;

        foreach (var c in MethodName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
                continue;
            }

            upperNext = true;
            builder.Append(c switch
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
    /// Gets the generated interop entry-point name for the method.
    /// </summary>
    /// <param name="model">The owning record model.</param>
    /// <returns>The generated interop name.</returns>
    public string GetMethodInteropName(RecordModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        using var builder = ZString.CreateStringBuilder();
        builder.Append(model.Type.CppTypeName.ToValidCSharpName());
        builder.Append("_");
        builder.Append(GetInvokeName());
        foreach (var parameterModel in Parameters)
        {
            builder.Append('_');
            builder.Append(NormalizeInteropTypeName(parameterModel.Type.CppTypeName).ToValidCSharpName());
        }

        return builder.ToString();
    }

    private static string NormalizeInteropTypeName(string cppTypeName)
    {
        ArgumentNullException.ThrowIfNull(cppTypeName);

        return ConstKeywordRegex()
            .Replace(cppTypeName, "")
            .Trim();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bconst\b")]
    private static partial System.Text.RegularExpressions.Regex ConstKeywordRegex();
}