// -----------------------------------------------------------------------
// <copyright file="BindingFiniteProfileValidator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Validates the complete finite-profile graph before semantic plan publication.
/// </summary>
internal static class BindingFiniteProfileValidator
{
    /// <summary>
    /// Validates one finite-profile API and its optional resolved native slots.
    /// </summary>
    /// <param name="api">The finite-profile API.</param>
    /// <param name="profile">The provider emission profile.</param>
    /// <param name="slots">The resolved native slots, when available.</param>
    /// <exception cref="ArgumentException">The finite-profile graph is invalid.</exception>
    /// <exception cref="InvalidOperationException">An emitted export has no resolved native slot.</exception>
    internal static void Validate(
        BindingFiniteProfileApi api,
        BindingEmissionProfile profile,
        IReadOnlyDictionary<string, int>? slots = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(profile);
        RequireText(api.ManagedRelativePath, nameof(api.ManagedRelativePath));
        RequireText(api.NativeRelativePath, nameof(api.NativeRelativePath));
        RequireText(api.NativePreamble, nameof(api.NativePreamble));
        RequireText(api.NativeUnknownExceptionMessage, nameof(api.NativeUnknownExceptionMessage));
        RequireCollection(api.Statuses, nameof(api.Statuses));
        RequireCollection(api.ValueTypes, nameof(api.ValueTypes));
        RequireCollection(api.Owners, nameof(api.Owners));
        RequireCollection(api.OwnedResults, nameof(api.OwnedResults));
        RequireCollection(api.CompositeResults, nameof(api.CompositeResults));
        RequireCollection(api.Operations, nameof(api.Operations));
        RequireCollection(api.NativeExportOrder, nameof(api.NativeExportOrder));
        RequireCollection(api.AdditionalNativeExports, nameof(api.AdditionalNativeExports));

        foreach (var status in api.Statuses)
        {
            ArgumentNullException.ThrowIfNull(status);
            RequireIdentifier(status.Name, nameof(status.Name));
            RequireNonEmpty(status.Members, $"{status.Name}.Members");
            foreach (var member in status.Members)
            {
                RequireIdentifier(member.Key, $"{status.Name}.Members");
            }

            EnsureUnique(status.Members.Select(static member => member.Key), $"{status.Name} member");
        }

        EnsureUnique(api.Statuses.Select(static value => value.Name), "status type");
        foreach (var value in api.ValueTypes)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireIdentifier(value.Name, nameof(value.Name));
            RequireIdentifier(value.NativeName, nameof(value.NativeName));
            RequirePositive(value.Size, nameof(value.Size));
            RequirePositive(value.Alignment, nameof(value.Alignment));
            RequireNonEmpty(value.Fields, $"{value.Name}.Fields");
            foreach (var field in value.Fields)
            {
                ArgumentNullException.ThrowIfNull(field);
                RequireText(field.Type, $"{value.Name}.Fields.Type");
                RequireIdentifier(field.Name, $"{value.Name}.Fields.Name");
            }

            EnsureUnique(value.Fields.Select(static field => field.Name), $"{value.Name} field");
        }

        EnsureUnique(api.ValueTypes.Select(static value => value.Name), "value type");
        foreach (var owner in api.Owners)
        {
            ArgumentNullException.ThrowIfNull(owner);
            RequireIdentifier(owner.Name, nameof(owner.Name));
            RequireIdentifier(owner.NativeName, nameof(owner.NativeName));
            RequirePositive(owner.Size, nameof(owner.Size));
            RequirePositive(owner.Alignment, nameof(owner.Alignment));
            RequireIdentifier(owner.DestroyExport, nameof(owner.DestroyExport));
        }

        EnsureUnique(api.Owners.Select(static value => value.Name), "owner");
        foreach (var result in api.OwnedResults)
        {
            ArgumentNullException.ThrowIfNull(result);
            RequireIdentifier(result.Name, nameof(result.Name));
            RequireReference(api.Statuses, result.StatusName, static value => value.Name, "status");
            RequireReference(api.Owners, result.OwnerName, static value => value.Name, "owner");
            RequireIdentifier(result.StatusProperty, nameof(result.StatusProperty));
            RequireIdentifier(result.OwnerProperty, nameof(result.OwnerProperty));
        }

        EnsureUnique(api.OwnedResults.Select(static value => value.Name), "owned result");
        foreach (var result in api.CompositeResults)
        {
            ArgumentNullException.ThrowIfNull(result);
            RequireIdentifier(result.Name, nameof(result.Name));
            RequireNonEmpty(result.Fields, $"{result.Name}.Fields");
            foreach (var field in result.Fields)
            {
                ArgumentNullException.ThrowIfNull(field);
                RequireText(field.NativeType, $"{result.Name}.Fields.NativeType");
                RequireText(field.ManagedType, $"{result.Name}.Fields.ManagedType");
                RequireIdentifier(field.Name, $"{result.Name}.Fields.Name");
                RequireText(field.InitialValue, $"{result.Name}.Fields.InitialValue");
                RequireText(field.Projection, $"{result.Name}.Fields.Projection");
            }

            EnsureUnique(result.Fields.Select(static field => field.Name), $"{result.Name} field");

            RequireCollection(result.ComputedProperties ?? [], $"{result.Name}.ComputedProperties");
            foreach (var property in result.ComputedProperties ?? [])
            {
                ArgumentNullException.ThrowIfNull(property);
                RequireText(property.Type, $"{result.Name}.ComputedProperties.Type");
                RequireIdentifier(property.Name, $"{result.Name}.ComputedProperties.Name");
                RequireText(property.Expression, $"{result.Name}.ComputedProperties.Expression");
            }
        }

        EnsureUnique(api.CompositeResults.Select(static value => value.Name), "composite result");
        foreach (var operation in api.Operations)
        {
            ValidateOperation(api, operation);
        }

        EnsureUnique(api.Operations.Select(GetManagedOperationIdentity), "managed operation");

        foreach (var export in api.NativeExportOrder)
        {
            RequireIdentifier(export, nameof(api.NativeExportOrder));
        }

        foreach (var export in api.AdditionalNativeExports)
        {
            RequireIdentifier(export.Name, nameof(export.Name));
            RequireText(export.ReturnType, nameof(export.ReturnType));
            ArgumentNullException.ThrowIfNull(export.Parameters);
            RequireText(export.Body, nameof(export.Body));
        }

        var exports = api.Owners.Select(static value => value.DestroyExport)
            .Concat(api.Operations.SelectMany(GetExports))
            .Concat(api.AdditionalNativeExports.Select(static value => value.Name))
            .Prepend(profile.NativeErrorClearExport)
            .ToArray();
        EnsureUnique(exports, "finite profile export");
        if (!exports.SequenceEqual(api.NativeExportOrder, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "A finite profile export order must contain every emitted native operation exactly once and in emission order.",
                nameof(api));
        }

        if (slots is null)
        {
            return;
        }

        foreach (var export in exports)
        {
            if (!slots.ContainsKey(export))
            {
                throw new InvalidOperationException($"Finite profile export '{export}' has no shared function-table slot.");
            }
        }
    }

    private static void ValidateOperation(BindingFiniteProfileApi api, BindingFiniteOperationDefinition operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        RequireIdentifier(operation.NativeExport, nameof(operation.NativeExport));
        RequireText(operation.NativeBody, nameof(operation.NativeBody));
        switch (operation)
        {
            case BindingBufferOwnerOperationDefinition value:
                RequireIdentifier(value.MethodName, nameof(value.MethodName));
                RequireReference(api.Owners, value.OwnerName, static item => item.Name, "owner");
                RequireText(value.NativeReturnType, nameof(value.NativeReturnType));
                RequireText(value.NativeFailureExpression, nameof(value.NativeFailureExpression));
                RequireNonEmpty(value.Buffers, $"{value.MethodName}.Buffers");
                foreach (var buffer in value.Buffers)
                {
                    ValidateBuffer(buffer);
                }

                if (value.OwnedResultName is null)
                {
                    if (value.StatusName is not null || value.SuccessMember is not null)
                    {
                        throw new ArgumentException("A direct owner operation cannot declare conditional status metadata.");
                    }

                    if (value.NativeReturnType != "void")
                    {
                        throw new ArgumentException("A direct owner operation must use the void native return type.");
                    }
                }
                else
                {
                    var status = RequireReference(api.Statuses, value.StatusName, static item => item.Name, "status");
                    var result = RequireReference(
                        api.OwnedResults,
                        value.OwnedResultName,
                        static item => item.Name,
                        "owned result");
                    if (result.OwnerName != value.OwnerName || result.StatusName != value.StatusName)
                    {
                        throw new ArgumentException(
                            $"Owned result '{result.Name}' does not match operation '{value.MethodName}'.");
                    }

                    if (!status.Members.Any(member => member.Key == value.SuccessMember))
                    {
                        throw new ArgumentException($"Status '{status.Name}' has no success member '{value.SuccessMember}'.");
                    }
                }

                break;

            case BindingCompositeOperationDefinition value:
                ValidateMethod(api, value.ContainingType, value.MethodName, value.Owners, value.Values);
                RequireReference(api.CompositeResults, value.ResultName, static item => item.Name, "composite result");
                break;

            case BindingOwnedOperationDefinition value:
                ValidateMethod(api, value.ContainingType, value.MethodName, value.Owners, value.Values);
                RequireReference(api.Owners, value.ResultOwnerName, static item => item.Name, "owner");
                break;

            case BindingScalarOperationDefinition value:
                ValidateMethod(api, value.ContainingType, value.MethodName, value.Owners, value.Values);
                RequireText(value.ManagedReturnType, nameof(value.ManagedReturnType));
                RequireText(value.NativeReturnType, nameof(value.NativeReturnType));
                RequireText(value.ManagedTransportType ?? value.ManagedReturnType, nameof(value.ManagedTransportType));
                RequireText(value.NativeFailureExpression, nameof(value.NativeFailureExpression));
                break;

            case BindingTwoPhaseOperationDefinition value:
                RequireIdentifier(value.ContainingType, nameof(value.ContainingType));
                RequireIdentifier(value.MethodName, nameof(value.MethodName));
                ValidateOwner(api, value.Owner);
                var meshResult = RequireReference(
                    api.CompositeResults,
                    value.ResultName,
                    static item => item.Name,
                    "composite result");
                RequireNonEmpty(value.Buffers, $"{value.MethodName}.Buffers");
                RequireText(value.OverflowMessage, nameof(value.OverflowMessage));
                RequireIdentifier(value.CountExport, nameof(value.CountExport));
                RequireText(value.CountNativeBody, nameof(value.CountNativeBody));
                foreach (var buffer in value.Buffers)
                {
                    RequireIdentifier(buffer.PropertyName, nameof(buffer.PropertyName));
                    RequireText(buffer.ElementType, nameof(buffer.ElementType));
                    RequireText(buffer.NativeElementType, nameof(buffer.NativeElementType));
                    RequireIdentifier(buffer.CountName, nameof(buffer.CountName));
                    if (buffer.PointerName is not null)
                    {
                        RequireIdentifier(buffer.PointerName, nameof(buffer.PointerName));
                    }

                    if (!meshResult.Fields.Any(field => field.Name == buffer.PropertyName
                                                        && field.ManagedType == buffer.ElementType + "[]"))
                    {
                        throw new ArgumentException(
                            $"Two-phase buffer '{buffer.PropertyName}' has no matching array result field.");
                    }
                }

                break;

            default:
                throw new ArgumentException($"Unsupported finite operation '{operation.GetType().Name}'.");
        }
    }

    private static void ValidateMethod(
        BindingFiniteProfileApi api,
        string containingType,
        string methodName,
        IReadOnlyList<BindingOwnerParameterDefinition> owners,
        IReadOnlyList<BindingValueParameterDefinition> values)
    {
        RequireIdentifier(containingType, nameof(containingType));
        RequireIdentifier(methodName, nameof(methodName));
        RequireCollection(owners, nameof(owners));
        RequireCollection(values, nameof(values));
        foreach (var owner in owners)
        {
            ValidateOwner(api, owner);
        }

        foreach (var value in values)
        {
            RequireIdentifier(value.Name, nameof(value.Name));
            RequireText(value.ManagedType, nameof(value.ManagedType));
            RequireText(value.NativeType, nameof(value.NativeType));
            RequireText(value.ManagedTransportType ?? value.ManagedType, nameof(value.ManagedTransportType));
            RequireText(value.ManagedArgumentExpression, nameof(value.ManagedArgumentExpression));
        }

        EnsureUnique(owners.Select(static value => value.Name).Concat(values.Select(static value => value.Name)),
            $"{methodName} parameter");
    }

    private static void ValidateOwner(BindingFiniteProfileApi api, BindingOwnerParameterDefinition owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        RequireIdentifier(owner.Name, nameof(owner.Name));
        RequireReference(api.Owners, owner.OwnerName, static value => value.Name, "owner");
    }

    private static void ValidateBuffer(BindingBufferDefinition buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        RequireIdentifier(buffer.Name, nameof(buffer.Name));
        RequireText(buffer.ElementType, nameof(buffer.ElementType));
        RequireText(buffer.NativeElementType, nameof(buffer.NativeElementType));
        RequirePositive(buffer.ElementsPerItem, nameof(buffer.ElementsPerItem));
        RequireText(buffer.LengthError, nameof(buffer.LengthError));
        if (buffer.PointerName is not null)
        {
            RequireIdentifier(buffer.PointerName, nameof(buffer.PointerName));
        }

        if (!buffer.RequiresIndicesBelowFirstBufferItemCount)
        {
            return;
        }

        RequireText(buffer.IndexError, nameof(buffer.IndexError));
    }

    private static IEnumerable<string> GetExports(BindingFiniteOperationDefinition operation)
    {
        if (operation is BindingTwoPhaseOperationDefinition twoPhase)
        {
            yield return twoPhase.CountExport;
        }

        yield return operation.NativeExport;
    }

    private static string GetManagedOperationIdentity(BindingFiniteOperationDefinition operation)
    {
        return operation switch
        {
            BindingBufferOwnerOperationDefinition value => value.OwnerName + "." + value.MethodName,
            BindingCompositeOperationDefinition value => value.ContainingType + "." + value.MethodName,
            BindingOwnedOperationDefinition value => value.ContainingType + "." + value.MethodName,
            BindingScalarOperationDefinition value => value.ContainingType + "." + value.MethodName,
            BindingTwoPhaseOperationDefinition value => value.ContainingType + "." + value.MethodName,
            _ => throw new ArgumentException($"Unsupported finite operation '{operation.GetType().Name}'."),
        };
    }

    private static T RequireReference<T>(
        IReadOnlyList<T> values,
        string? name,
        Func<T, string> selector,
        string kind)
    {
        RequireIdentifier(name, kind);
        var matches = values.Where(value => selector(value) == name).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new ArgumentException($"Finite profile references unknown {kind} '{name}'.");
    }

    private static void EnsureUnique(IEnumerable<string> values, string kind)
    {
        var duplicate = values.GroupBy(static value => value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is null)
        {
            return;
        }

        throw new ArgumentException($"Duplicate {kind} '{duplicate.Key}'.");
    }

    private static void RequireCollection<T>(IReadOnlyList<T>? values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        if (!values.Any(static value => value is null))
        {
            return;
        }

        throw new ArgumentException($"Collection '{name}' cannot contain null values.", name);
    }

    private static void RequireNonEmpty<T>(IReadOnlyList<T>? values, string name)
    {
        RequireCollection(values, name);
        if (values!.Count != 0)
        {
            return;
        }

        throw new ArgumentException($"Collection '{name}' cannot be empty.", name);
    }

    private static void RequireIdentifier(string? value, string name)
    {
        RequireText(value, name);
        if (SyntaxFacts.IsValidIdentifier(value!))
        {
            return;
        }

        throw new ArgumentException($"'{value}' is not a valid identifier.", name);
    }

    private static void RequireText(string? value, string name)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        throw new ArgumentException($"'{name}' cannot be empty.", name);
    }

    private static void RequirePositive(int value, string name)
    {
        if (value > 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(name, value, "The value must be positive.");
    }
}