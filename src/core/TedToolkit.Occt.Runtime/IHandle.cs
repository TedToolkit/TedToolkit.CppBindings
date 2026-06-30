// -----------------------------------------------------------------------
// <copyright file="IHandle.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Exposes access to a native OCCT handle target.
/// </summary>
/// <typeparam name="TElement">The unmanaged transient element type.</typeparam>
public interface IHandle<TElement>
    where TElement : unmanaged, IStandard_Transient
{
    /// <summary>
    /// Gets a managed reference to the native element.
    /// </summary>
    ref TElement Value { get; }

    /// <summary>
    /// Gets the native pointer for the underlying element.
    /// </summary>
    unsafe TElement* NativeHandle { get; }
}