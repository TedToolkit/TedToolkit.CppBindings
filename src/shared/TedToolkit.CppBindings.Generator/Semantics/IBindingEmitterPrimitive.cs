// -----------------------------------------------------------------------
// <copyright file="IBindingEmitterPrimitive.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Emits one named provider primitive through the shared semantic engine.
/// </summary>
public interface IBindingEmitterPrimitive
{
    /// <summary>
    /// Gets the provider-unique primitive name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Emits the primitive for one semantic value.
    /// </summary>
    /// <param name="value">The provider-independent semantic value.</param>
    /// <returns>The emitted text.</returns>
    public string Emit(string value);
}