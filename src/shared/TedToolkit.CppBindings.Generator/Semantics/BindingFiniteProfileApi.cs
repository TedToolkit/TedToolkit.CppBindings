// -----------------------------------------------------------------------
// <copyright file="BindingFiniteProfileApi.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one finite handwritten native profile through reusable transport and result semantics.
/// </summary>
/// <param name="ManagedRelativePath">The output-relative managed source path.</param>
/// <param name="NativeRelativePath">The output-relative native adapter source path.</param>
/// <param name="Status">The native status enumeration projected to managed code.</param>
/// <param name="ValueType">The blittable value transported by the profile.</param>
/// <param name="Owner">The owned native object representation.</param>
/// <param name="OwnedResult">The status plus optional-owner result.</param>
/// <param name="CompositeResult">The result assembled from native output parameters.</param>
/// <param name="Factory">The buffer-backed owned factory operation.</param>
/// <param name="CompositeOperation">The operation that assembles a composite managed result.</param>
/// <param name="NativePreamble">Provider-specific native includes and supporting declarations.</param>
/// <param name="FactoryBody">The provider-specific native factory body.</param>
/// <param name="CompositeOperationBody">The provider-specific native composite-operation body.</param>
/// <param name="NativeUnknownExceptionMessage">The provider-specific unknown native exception message.</param>
/// <param name="NativeExportOrder">The exact native function-table order shared by both languages.</param>
/// <param name="AdditionalNativeExports">Additional simple native operations.</param>
public sealed record BindingFiniteProfileApi(
    string ManagedRelativePath,
    string NativeRelativePath,
    BindingStatusDefinition Status,
    BindingValueTypeDefinition ValueType,
    BindingOwnerDefinition Owner,
    BindingOwnedResultDefinition OwnedResult,
    BindingCompositeResultDefinition CompositeResult,
    BindingOwnedFactoryDefinition Factory,
    BindingCompositeOperationDefinition CompositeOperation,
    string NativePreamble,
    string FactoryBody,
    string CompositeOperationBody,
    string NativeUnknownExceptionMessage,
    IReadOnlyList<string> NativeExportOrder,
    IReadOnlyList<BindingNativeExportDefinition> AdditionalNativeExports);