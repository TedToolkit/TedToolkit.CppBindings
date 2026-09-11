// -----------------------------------------------------------------------
// <copyright file="IBindingTemplatePolicy.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Decides whether a template projection is part of a provider's supported profile.
/// </summary>
public interface IBindingTemplatePolicy
{
    /// <summary>
    /// Evaluates one template projection.
    /// </summary>
    /// <param name="template">The template descriptor.</param>
    /// <returns>The admission result.</returns>
    public BindingAdmission Admit(BindingTemplateDescriptor template);
}