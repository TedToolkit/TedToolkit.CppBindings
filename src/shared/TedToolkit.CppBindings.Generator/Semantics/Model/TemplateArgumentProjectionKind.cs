// -----------------------------------------------------------------------
// <copyright file="TemplateArgumentProjectionKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes how one C++ template argument participates in the managed type identity.
/// </summary>
public enum TemplateArgumentProjectionKind
{
    /// <summary>
    /// The argument remains a managed generic type parameter.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// The argument is encoded into the managed type name.
    /// </summary>
    Fixed = 1,
}