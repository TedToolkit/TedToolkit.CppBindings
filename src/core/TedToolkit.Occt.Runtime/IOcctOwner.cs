// -----------------------------------------------------------------------
// <copyright file="IOcctOwner.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Provides non-owning access to the native value held by a managed OCCT owner.
/// </summary>
/// <typeparam name="TValue">The exact-layout unmanaged native projection.</typeparam>
/// <remarks>
/// This interface unifies generated invocation only. It does not define acquisition, release,
/// destruction, or disposal semantics. The returned reference remains valid only while the
/// implementing owner is alive and undisposed.
/// </remarks>
public interface IOcctOwner<TValue>
    where TValue : unmanaged
{
    /// <summary>
    /// Gets a non-owning reference to the live native value.
    /// </summary>
    public ref TValue Value { get; }
}
