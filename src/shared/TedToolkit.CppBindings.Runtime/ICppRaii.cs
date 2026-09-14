// -----------------------------------------------------------------------
// <copyright file="ICppRaii.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Marks an exact-layout unmanaged projection whose non-transient native object requires RAII
/// destruction.
/// </summary>
/// <remarks>
/// This marker classifies native representation only. It does not construct, own, destroy, or
/// dispose the represented object.
/// </remarks>
public interface ICppRaii;