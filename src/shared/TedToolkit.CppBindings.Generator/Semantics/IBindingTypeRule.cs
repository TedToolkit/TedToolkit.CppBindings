// -----------------------------------------------------------------------
// <copyright file="IBindingTypeRule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Projects one provider-independent C++ type when the rule recognizes it.
/// </summary>
public interface IBindingTypeRule
{
    /// <summary>
    /// Attempts to project a type.
    /// </summary>
    /// <param name="type">The native type descriptor.</param>
    /// <param name="projection">The completed projection when the rule matches.</param>
    /// <returns><see langword="true"/> when the rule matched.</returns>
    public bool TryResolve(CppTypeDescriptor type, out BindingTypeProjection? projection);
}