// -----------------------------------------------------------------------
// <copyright file="NativeTypeNameAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Attributes;

/// <summary>
/// Associates a managed declaration with its original native type name.
/// </summary>
/// <param name="nativeTypeName">The native type name as it appears in the OCCT headers.</param>
[AttributeUsage(AttributeTargets.Struct
                | AttributeTargets.Interface
                | AttributeTargets.Parameter
                | AttributeTargets.ReturnValue
                | AttributeTargets.Field)]
public sealed class NativeTypeNameAttribute(string nativeTypeName) : Attribute
{
    /// <summary>
    /// Gets the native type name as it appears in the OCCT headers.
    /// </summary>
    public string NativeTypeName { get; } = nativeTypeName;
}