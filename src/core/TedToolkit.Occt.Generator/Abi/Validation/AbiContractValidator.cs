// -----------------------------------------------------------------------
// <copyright file="AbiContractValidator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Abi.Contracts;

namespace TedToolkit.Occt.Generator.Abi.Validation;

/// <summary>
/// Rejects ABI operations whose source, transport, adapter, or managed projection is incomplete.
/// </summary>
internal static class AbiContractValidator
{
    /// <summary>
    /// Validates one operation before naming or emission.
    /// </summary>
    /// <param name="operation">The candidate ABI operation.</param>
    /// <returns>The deterministic rejection diagnostic, or <see langword="null"/> when exportable.</returns>
    public static AbiProjectionDiagnostic? Validate(AbiOperationModel operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        foreach (var parameter in operation.Parameters)
        {
            var diagnostic = ValidateType(operation, parameter.Name, parameter.Direction, parameter.Ownership,
                parameter.Type);
            if (diagnostic is not null)
            {
                return diagnostic;
            }
        }

        if (operation.Result is not null)
        {
            var diagnostic = ValidateType(operation, "result", operation.Result.Direction, operation.Result.Ownership,
                operation.Result.Type);
            if (diagnostic is not null)
            {
                return diagnostic;
            }
        }

        return null;
    }

    private static AbiProjectionDiagnostic? ValidateType(
        AbiOperationModel operation,
        string valueName,
        AbiDirection direction,
        AbiOwnership ownership,
        AbiTypeProjectionModel type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var missingRule = GetMissingRule(type);

        return missingRule is null
            ? null
            : new()
            {
                Declaration = operation.Declaration,
                SourceLocation = operation.SourceLocation,
                ValueName = valueName,
                SourceType = type.SourceCppTypeName,
                Direction = direction,
                Ownership = ownership,
                MissingRule = missingRule,
            };
    }

    private static string? GetMissingRule(AbiTypeProjectionModel type)
    {
        if (string.IsNullOrWhiteSpace(type.SourceCppTypeName))
        {
            return "source-cpp";
        }

        if (string.IsNullOrWhiteSpace(type.TransportId) || string.IsNullOrWhiteSpace(type.CAbiTypeName))
        {
            return "abi-transport";
        }

        if (string.IsNullOrWhiteSpace(type.CppAdapterTypeName))
        {
            return "cpp-adapter";
        }

        if (type.ManagedTransportType is null)
        {
            return "managed-transport";
        }

        return type.PublicManagedType is null ? "public-managed" : null;
    }
}