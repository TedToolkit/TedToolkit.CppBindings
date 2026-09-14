// -----------------------------------------------------------------------
// <copyright file="IBindingLayoutPolicy.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Decides whether compiler-proved native layout can be represented by a provider.
/// </summary>
public interface IBindingLayoutPolicy
{
    /// <summary>
    /// Evaluates one native layout.
    /// </summary>
    /// <param name="layout">The compiler-proved layout.</param>
    /// <returns>The admission result.</returns>
    public BindingAdmission Admit(BindingLayoutDescriptor layout);
}