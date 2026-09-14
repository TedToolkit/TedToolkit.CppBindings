// -----------------------------------------------------------------------
// <copyright file="BindingNameExtensions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Normalizes provider-supplied native names for deterministic emitted identifiers.
/// </summary>
internal static class BindingNameExtensions
{
    /// <summary>
    /// Converts a native type spelling into a generated identifier without losing indirection identity.
    /// </summary>
    /// <param name="name">The native type spelling.</param>
    /// <returns>The normalized identifier.</returns>
    internal static string ToGeneratedTypeName(this string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder();
        var needsSeparator = false;
        var endsWithUnderscore = false;
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsLetterOrDigit(character))
            {
                if (builder.Length is 0 && char.IsNumber(character))
                {
                    _ = builder.Append('_');
                }

                if (needsSeparator && builder.Length > 0 && !endsWithUnderscore)
                {
                    _ = builder.Append('_');
                }

                _ = builder.Append(character);
                needsSeparator = false;
                endsWithUnderscore = false;
                continue;
            }

            var token = character switch
            {
                '*' => "Ptr",
                '&' when index + 1 < name.Length && name[index + 1] == '&' => "RRef",
                '&' => "Ref",
                _ => null,
            };
            if (token is null)
            {
                needsSeparator = builder.Length > 0;
                continue;
            }

            if (character == '&' && token == "RRef")
            {
                index++;
            }

            if (builder.Length > 0 && !endsWithUnderscore)
            {
                _ = builder.Append('_');
            }

            _ = builder.Append(token);
            needsSeparator = true;
            endsWithUnderscore = false;
        }

        return builder.ToString();
    }
}