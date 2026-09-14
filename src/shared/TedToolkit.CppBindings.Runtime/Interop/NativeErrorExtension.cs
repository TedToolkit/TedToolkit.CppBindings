// -----------------------------------------------------------------------
// <copyright file="NativeErrorExtension.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Creates a Provider-specific exception for one otherwise-unrecognized local native-error kind.
/// </summary>
/// <param name="kind">The Provider-local native-error kind.</param>
/// <param name="message">The copied native message or deterministic fallback.</param>
/// <param name="nativeTypeName">The copied native exception type name.</param>
/// <param name="nativeStackTrace">The copied native stack text.</param>
/// <returns>The Provider exception, or <see langword="null"/> when the kind is not recognized.</returns>
[GeneratedCodeOnly]
public delegate Exception? NativeErrorExtension(
    int kind,
    string message,
    string? nativeTypeName,
    string? nativeStackTrace);