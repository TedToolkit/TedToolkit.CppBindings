// -----------------------------------------------------------------------
// <copyright file="SecondRaii.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.SecondWrapper;

/// <summary>
/// Represents an exact-layout non-transient RAII fixture from the second independent wrapper.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SecondRaii : IOcctRaii
{
    /// <summary>
    /// Gets or sets the fixture value.
    /// </summary>
    public int Value;

    /// <summary>
    /// Gets or sets the unmanaged destruction counter used by this fixture.
    /// </summary>
    internal int* DestroyCount;
}