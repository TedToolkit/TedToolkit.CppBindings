// -----------------------------------------------------------------------
// <copyright file="BindingAdmission.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Reports whether a semantic declaration is admitted and why it was rejected.
/// </summary>
/// <param name="IsAdmitted">Whether the declaration may be emitted.</param>
/// <param name="Reason">A stable rejection reason, or <see langword="null"/> when admitted.</param>
public sealed record BindingAdmission(bool IsAdmitted, string? Reason)
{
    /// <summary>
    /// Gets an admitted result.
    /// </summary>
    public static BindingAdmission Admitted { get; } = new(true, null);

    /// <summary>
    /// Creates a rejected result with a non-empty reason.
    /// </summary>
    /// <param name="reason">The provider-owned rejection reason.</param>
    /// <returns>The rejected result.</returns>
    public static BindingAdmission Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(false, reason);
    }
}