// -----------------------------------------------------------------------
// <copyright file="NativeResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Cgal.Runtime.Tests;

/// <summary>
/// Matches the fixture's value-only result transport.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeResult
{
    /// <summary>
    /// Stores the declared or unknown alternative tag.
    /// </summary>
    internal int Tag;

    /// <summary>
    /// Stores the first point's x coordinate.
    /// </summary>
    internal double AX;

    /// <summary>
    /// Stores the first point's y coordinate.
    /// </summary>
    internal double AY;

    /// <summary>
    /// Stores the second point's x coordinate.
    /// </summary>
    internal double BX;

    /// <summary>
    /// Stores the second point's y coordinate.
    /// </summary>
    internal double BY;
}