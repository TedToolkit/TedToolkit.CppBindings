// -----------------------------------------------------------------------
// <copyright file="NativeErrorClear.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents the non-throwing same-library entry point that consumes one native error owner slot.
/// </summary>
/// <param name="error">The authoritative native error owner slot to consume.</param>
internal delegate void NativeErrorClear(ref NativeError error);