// -----------------------------------------------------------------------
// <copyright file="AbiProjectionDiagnostic.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using TedToolkit.Occt.Generator.Abi.Contracts;

namespace TedToolkit.Occt.Generator.Abi.Validation;

/// <summary>
/// Describes one deterministic reason that an operation cannot enter the C ABI.
/// </summary>
internal sealed class AbiProjectionDiagnostic
{
    /// <summary>
    /// Gets the stable diagnostic identifier.
    /// </summary>
    public string Code { get; } = "TEDOCCTABI001";

    /// <summary>
    /// Gets the rejected source declaration.
    /// </summary>
    public required string Declaration { get; init; }

    /// <summary>
    /// Gets the rejected declaration's source location.
    /// </summary>
    public required string SourceLocation { get; init; }

    /// <summary>
    /// Gets the rejected value name.
    /// </summary>
    public required string ValueName { get; init; }

    /// <summary>
    /// Gets the source C++ type spelling used for diagnostics only.
    /// </summary>
    public required string SourceType { get; init; }

    /// <summary>
    /// Gets the rejected value direction.
    /// </summary>
    public required AbiDirection Direction { get; init; }

    /// <summary>
    /// Gets the rejected value ownership contract.
    /// </summary>
    public required AbiOwnership Ownership { get; init; }

    /// <summary>
    /// Gets the missing explicit projection rule.
    /// </summary>
    public required string MissingRule { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return ZString.Concat(Code, ": declaration='", Declaration, "'; location='", SourceLocation,
            "'; value='", ValueName, "'; source-type='", SourceType, "'; direction='", GetToken(Direction),
            "'; ownership='", GetToken(Ownership), "'; missing-rule='", MissingRule, "'");
    }

    private static string GetToken(AbiDirection value)
    {
        return value switch
        {
            AbiDirection.In => "in",
            AbiDirection.Out => "out",
            AbiDirection.InOut => "inout",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    private static string GetToken(AbiOwnership value)
    {
        return value switch
        {
            AbiOwnership.Value => "value",
            AbiOwnership.Borrowed => "borrowed",
            AbiOwnership.Owned => "owned",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }
}