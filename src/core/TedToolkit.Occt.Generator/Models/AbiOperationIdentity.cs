// -----------------------------------------------------------------------
// <copyright file="AbiOperationIdentity.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Cysharp.Text;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the canonical semantic identity and exported symbol for one ABI-major-1 operation.
/// </summary>
internal sealed class AbiOperationIdentity
{
    private AbiOperationIdentity(string canonicalIdentity, string symbolName)
    {
        CanonicalIdentity = canonicalIdentity;
        SymbolName = symbolName;
    }

    /// <summary>
    /// Gets the canonical UTF-8 operation identity.
    /// </summary>
    public string CanonicalIdentity { get; }

    /// <summary>
    /// Gets the globally unique exported operation symbol.
    /// </summary>
    public string SymbolName { get; }

    /// <summary>
    /// Creates the ABI-major-1 identity for a completely mapped operation.
    /// </summary>
    /// <param name="operation">The semantic operation model.</param>
    /// <returns>The canonical identity and exported symbol.</returns>
    public static AbiOperationIdentity Create(AbiOperationModel operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ValidateToken(operation.OwnerId, nameof(operation.OwnerId));
        ValidateToken(operation.OperationId, nameof(operation.OperationId));

        var builder = ZString.CreateStringBuilder();
        try
        {
            builder.Append("v1|owner=");
            builder.Append(operation.OwnerId);
            builder.Append("|kind=");
            builder.Append(GetToken(operation.Kind));
            builder.Append("|operation=");
            builder.Append(operation.OperationId);
            builder.Append("|receiver=");
            builder.Append(GetToken(operation.Receiver));
            builder.Append("|parameters=");

            for (var index = 0; index < operation.Parameters.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendValueIdentity(ref builder, operation.Parameters[index].Direction,
                    operation.Parameters[index].Nullability, operation.Parameters[index].Ownership,
                    operation.Parameters[index].Type);
            }

            builder.Append("|result=");
            if (operation.Result is null)
            {
                builder.Append("none");
            }
            else
            {
                AppendValueIdentity(ref builder, operation.Result.Direction, operation.Result.Nullability,
                    operation.Result.Ownership, operation.Result.Type);
            }

            var canonicalIdentity = builder.ToString();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIdentity));
            var digest = Convert.ToHexStringLower(hash.AsSpan(0, 16));
            var symbolName = ZString.Concat("ted_occt_v1_", operation.OwnerId, "_", operation.OperationId, "__",
                digest);
            return new(canonicalIdentity, symbolName);
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void AppendValueIdentity(
        ref Utf16ValueStringBuilder builder,
        AbiDirection direction,
        AbiNullability nullability,
        AbiOwnership ownership,
        AbiTypeProjectionModel type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (string.IsNullOrWhiteSpace(type.TransportId))
        {
            throw new InvalidOperationException("An ABI transport identifier is required before operation naming.");
        }

        ValidateToken(type.TransportId, nameof(type.TransportId));
        builder.Append(GetToken(direction));
        builder.Append(':');
        builder.Append(GetToken(nullability));
        builder.Append(':');
        builder.Append(GetToken(ownership));
        builder.Append(':');
        builder.Append(type.TransportId);
    }

    private static void ValidateToken(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("ABI semantic identifiers must match [a-z][a-z0-9_]*.", parameterName);
        }

        foreach (var character in value.AsSpan(1))
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            throw new ArgumentException("ABI semantic identifiers must match [a-z][a-z0-9_]*.", parameterName);
        }
    }

    private static string GetToken(AbiOperationKind value)
    {
        return value switch
        {
            AbiOperationKind.Constructor => "constructor",
            AbiOperationKind.Method => "method",
            AbiOperationKind.Operator => "operator",
            AbiOperationKind.Conversion => "conversion",
            AbiOperationKind.Destroy => "destroy",
            AbiOperationKind.Retain => "retain",
            AbiOperationKind.Release => "release",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    private static string GetToken(AbiReceiverKind value)
    {
        return value switch
        {
            AbiReceiverKind.None => "none",
            AbiReceiverKind.BorrowedConst => "borrowed-const",
            AbiReceiverKind.BorrowedMutable => "borrowed-mutable",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
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

    private static string GetToken(AbiNullability value)
    {
        return value switch
        {
            AbiNullability.Required => "required",
            AbiNullability.Nullable => "nullable",
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