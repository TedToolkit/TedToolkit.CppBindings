// -----------------------------------------------------------------------
// <copyright file="IStandard_Transient.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents an unmanaged OCCT transient object that can delete itself.
/// </summary>
public interface IStandard_Transient
{
    /// <summary>
    /// Releases the unmanaged transient instance.
    /// </summary>
    internal void Delete();
}