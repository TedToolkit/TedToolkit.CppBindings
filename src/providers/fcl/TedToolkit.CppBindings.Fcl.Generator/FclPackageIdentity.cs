// -----------------------------------------------------------------------
// <copyright file="FclPackageIdentity.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Describes the vcpkg version and optional port revision of FCL.</summary>
internal sealed record FclPackageIdentity(string Version, string? PortVersion);
