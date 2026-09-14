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
/// <param name="Statuses">The native status enumerations projected to managed code.</param>
/// <param name="ValueTypes">The blittable values transported by the profile.</param>
/// <param name="Owners">The owned native object representations.</param>
/// <param name="OwnedResults">The status plus optional-owner results.</param>
/// <param name="CompositeResults">The results assembled from native output parameters.</param>
/// <param name="Operations">The ordered managed/native operations.</param>
/// <param name="NativePreamble">Provider-specific native includes and supporting declarations.</param>
/// <param name="NativeUnknownExceptionMessage">The provider-specific unknown native exception message.</param>
/// <param name="NativeExportOrder">The exact native function-table order shared by both languages.</param>
/// <param name="AdditionalNativeExports">Additional simple native operations.</param>
public sealed record BindingFiniteProfileApi(
    string ManagedRelativePath,
    string NativeRelativePath,
    IReadOnlyList<BindingStatusDefinition> Statuses,
    IReadOnlyList<BindingValueTypeDefinition> ValueTypes,
    IReadOnlyList<BindingOwnerDefinition> Owners,
    IReadOnlyList<BindingOwnedResultDefinition> OwnedResults,
    IReadOnlyList<BindingCompositeResultDefinition> CompositeResults,
    IReadOnlyList<BindingFiniteOperationDefinition> Operations,
    string NativePreamble,
    string NativeUnknownExceptionMessage,
    IReadOnlyList<string> NativeExportOrder,
    IReadOnlyList<BindingNativeExportDefinition> AdditionalNativeExports);