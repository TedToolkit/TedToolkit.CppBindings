// -----------------------------------------------------------------------
// <copyright file="BindingNativeErrorCatchEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Emits the fixed provider-neutral standard C++ native-error catch sequence.
/// </summary>
public static class BindingNativeErrorCatchEmitter
{
    private static readonly IReadOnlyList<BindingNativeExceptionProjection> Projections =
    [
        Standard("std::bad_alloc", 6),
        Standard("std::out_of_range", 2),
        Standard("std::overflow_error", 7),
        Standard("std::underflow_error", 3),
        Standard("std::invalid_argument", 1),
        Standard("std::domain_error", 1),
        Standard("std::logic_error", 4),
        Standard("std::exception", 8),
    ];

    /// <summary>
    /// Renders the complete standard catch sequence, including the unknown fallback.
    /// </summary>
    /// <param name="setter">The native error setter expression.</param>
    /// <param name="error">The native error pointer expression.</param>
    /// <param name="failureStatement">The optional statement executed after every setter.</param>
    /// <param name="indentation">The indentation applied to each catch.</param>
    /// <param name="stackExpression">The optional final setter argument used for native stack text.</param>
    /// <param name="unknownMessageExpression">The expression used as the unknown failure message.</param>
    /// <returns>The deterministic C++ catch sequence.</returns>
    public static string Render(
        string setter,
        string error,
        string? failureStatement = null,
        string indentation = "    ",
        string? stackExpression = null,
        string unknownMessageExpression = "nullptr")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setter);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentNullException.ThrowIfNull(indentation);
        ArgumentException.ThrowIfNullOrWhiteSpace(unknownMessageExpression);

        var builder = new StringBuilder();
        foreach (var projection in Projections)
        {
            AppendCatch(builder, projection, setter, error, failureStatement, indentation, stackExpression);
        }

        _ = builder.Append(indentation).Append("catch (...)\n")
            .Append(indentation).Append("{\n")
            .Append(indentation).Append("    ").Append(setter).Append('(').Append(error)
            .Append(", 255, nullptr, ").Append(unknownMessageExpression);
        if (stackExpression is not null)
        {
            _ = builder.Append(", ").Append(stackExpression);
        }

        _ = builder.Append(");\n");
        AppendFailure(builder, failureStatement, indentation);
        return builder.Append(indentation).Append("}\n").ToString();
    }

    /// <summary>
    /// Determines whether a C++ exception type belongs to the fixed Shared catch sequence.
    /// </summary>
    /// <param name="cppType">The fully qualified C++ exception type.</param>
    /// <returns><see langword="true"/> when Shared owns the catch.</returns>
    internal static bool IsSharedExceptionType(string cppType)
    {
        var normalized = NormalizeType(cppType);
        return Projections.Any(projection => string.Equals(projection.CppType, normalized, StringComparison.Ordinal));
    }

    private static string NormalizeType(string cppType)
    {
        var normalized = string.Concat(cppType.Where(static character => !char.IsWhiteSpace(character)));
        return normalized.StartsWith("::", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    private static void AppendCatch(
        StringBuilder builder,
        BindingNativeExceptionProjection projection,
        string setter,
        string error,
        string? failureStatement,
        string indentation,
        string? stackExpression)
    {
        _ = builder.Append(indentation).Append("catch (const ").Append(projection.CppType)
            .Append("& exception)\n").Append(indentation).Append("{\n")
            .Append(indentation).Append("    ").Append(setter).Append('(').Append(error).Append(", ")
            .Append(projection.Code).Append(", ").Append(projection.NativeTypeExpression).Append(", ")
            .Append(projection.MessageExpression);
        if (stackExpression is not null)
        {
            _ = builder.Append(", ").Append(stackExpression);
        }

        _ = builder.Append(");\n");
        AppendFailure(builder, failureStatement, indentation);
        _ = builder.Append(indentation).Append("}\n");
    }

    private static void AppendFailure(StringBuilder builder, string? failureStatement, string indentation)
    {
        if (string.IsNullOrWhiteSpace(failureStatement))
        {
            return;
        }

        _ = builder.Append(indentation).Append("    ").Append(failureStatement).Append('\n');
    }

    private static BindingNativeExceptionProjection Standard(string type, int code)
    {
        return new(type, code, $"\"{type}\"", "exception.what()");
    }
}