// -----------------------------------------------------------------------
// <copyright file="ICppStableAddress.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Marks exact-layout RAII projections that require the same address from construction through destruction.
/// </summary>
/// <remarks>
/// <see cref="Owned{T}"/> reserves pinned managed storage for these projections because native code may
/// retain their address or pointers into their storage between calls. This does not extend the owner's
/// lifetime or permit access after disposal. Public native base interfaces propagate the requirement.
/// </remarks>
public interface ICppStableAddress : ICppRaii;