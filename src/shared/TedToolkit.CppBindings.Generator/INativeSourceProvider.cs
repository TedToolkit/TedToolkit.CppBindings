// -----------------------------------------------------------------------
// <copyright file="INativeSourceProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Supplies the native headers used to discover binding declarations.
/// </summary>
public interface INativeSourceProvider
{
    /// <summary>
    /// Resolves and validates the header inputs for the selected native dependency.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for native input discovery.</param>
    /// <returns>The include root and its public header inventory.</returns>
    NativeSourceInputs Resolve(CancellationToken cancellationToken);
}