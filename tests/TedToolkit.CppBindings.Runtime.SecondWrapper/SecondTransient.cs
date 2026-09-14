// -----------------------------------------------------------------------
// <copyright file="SecondTransient.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.SecondWrapper;

/// <summary>
/// Represents the exact-layout transient value emitted by the second wrapper fixture.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SecondTransient : IStandard_Transient
{
    /// <summary>
    /// Gets or sets the fixture data stored in native memory.
    /// </summary>
    public int Value;

    /// <summary>
    /// Gets or sets the unmanaged release counter used by this fixture.
    /// </summary>
    internal int* ReleaseCount;
}