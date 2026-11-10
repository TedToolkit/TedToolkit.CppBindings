// -----------------------------------------------------------------------
// <copyright file="BindingFreeFunctionDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a static projection of an external C linkage function.
/// </summary>
/// <param name="ContainingType">The managed static containing type.</param>
/// <param name="MethodName">The managed method name.</param>
/// <param name="QualifiedNativeName">The globally qualified C++ name used for source lookup.</param>
/// <param name="ReturnType">The function's return projection.</param>
/// <param name="Parameters">The function's ordered parameter projections.</param>
/// <param name="NativeExport">The distinct C linkage name of the generated adapter.</param>
/// <param name="IsNoexcept">Whether the source declaration proves that the function cannot throw.</param>
internal sealed record BindingFreeFunctionDefinition(
    string ContainingType,
    string MethodName,
    string QualifiedNativeName,
    BindingFreeFunctionType ReturnType,
    IReadOnlyList<(string Name, BindingFreeFunctionType Type)> Parameters,
    string NativeExport,
    bool IsNoexcept) : BindingFiniteOperationDefinition(NativeExport, "");