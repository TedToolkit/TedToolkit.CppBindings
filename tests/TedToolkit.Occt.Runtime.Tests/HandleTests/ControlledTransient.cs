// -----------------------------------------------------------------------
// <copyright file="ControlledTransient.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.HandleTests;

/// <summary>
/// Represents exact-layout fixture storage with a controlled release counter.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ControlledTransient : IStandard_Transient
{
    /// <summary>
    /// Gets or sets the fixture value.
    /// </summary>
    internal int Value;

    /// <summary>
    /// Gets or sets the unmanaged release counter used by this fixture.
    /// </summary>
    internal int* ReleaseCount;
}