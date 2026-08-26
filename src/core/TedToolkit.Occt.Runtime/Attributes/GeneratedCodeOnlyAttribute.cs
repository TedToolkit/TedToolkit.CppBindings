// -----------------------------------------------------------------------
// <copyright file="GeneratedCodeOnlyAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Runtime;

/// <summary>
/// Marks a public Runtime type or implementation hook as reserved for generated OCCT binding code.
/// </summary>
/// <remarks>
/// This metadata is compiler guidance rather than an authorization boundary. Runtime validation
/// remains authoritative when a marked member is invoked.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Constructor
    | AttributeTargets.Method
    | AttributeTargets.Property
    | AttributeTargets.Event,
    AllowMultiple = false,
    Inherited = false)]
public sealed class GeneratedCodeOnlyAttribute : Attribute;