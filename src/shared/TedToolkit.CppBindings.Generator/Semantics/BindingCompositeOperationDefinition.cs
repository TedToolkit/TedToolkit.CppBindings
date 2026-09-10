// -----------------------------------------------------------------------
// <copyright file="BindingCompositeOperationDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes an operation over two owners, one value, and multiple native outputs.
/// </summary>
/// <param name="ContainingType">The public static containing type.</param>
/// <param name="MethodName">The public operation name.</param>
/// <param name="NativeExport">The native operation export.</param>
/// <param name="FirstOwnerParameter">The first owner parameter name.</param>
/// <param name="SecondOwnerParameter">The second owner parameter name.</param>
/// <param name="ValueParameter">The value parameter name.</param>
public sealed record BindingCompositeOperationDefinition(
    string ContainingType,
    string MethodName,
    string NativeExport,
    string FirstOwnerParameter,
    string SecondOwnerParameter,
    string ValueParameter);