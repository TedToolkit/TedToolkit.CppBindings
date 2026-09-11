// -----------------------------------------------------------------------
// <copyright file="IOcctException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt;

/// <summary>
/// Represents the diagnostics associated with a projected native OCCT failure.
/// </summary>
/// <remarks>
/// This interface identifies a diagnostic shape, not trusted native provenance. The library-provided
/// <c>Occt*Exception</c> types are constructed only by Runtime.
/// </remarks>
public interface IOcctException : INativeException;