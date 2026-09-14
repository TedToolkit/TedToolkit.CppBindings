// -----------------------------------------------------------------------
// <copyright file="ICppOwner.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Provides non-owning access to the native value held by a managed C++ object owner.
/// </summary>
/// <typeparam name="TValue">The exact-layout unmanaged native projection.</typeparam>
/// <remarks>
/// This contract identifies non-owning access for caller guidance and diagnostics. It does not
/// define acquisition, release, destruction, or disposal semantics, and is not a shared generated
/// handle receiver. The reference remains subject to the original native lifetime and invalidation
/// rules, including the implementing owner's disposal state.
/// </remarks>
public interface ICppOwner<TValue>
    where TValue : unmanaged
{
    /// <summary>
    /// Gets a non-owning reference to the live native value.
    /// </summary>
    public ref TValue Value { get; }
}