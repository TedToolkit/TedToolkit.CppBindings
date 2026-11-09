// -----------------------------------------------------------------------
// <copyright file="INativeSourceProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Supplies the native inputs used to compose a binding provider.
/// </summary>
public interface INativeSourceProvider
{
    /// <summary>
    /// Resolves the headers and dependencies for the selected native source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for native input discovery.</param>
    /// <returns>The resolved native source inputs.</returns>
    NativeSourceInputs Resolve(CancellationToken cancellationToken);
}