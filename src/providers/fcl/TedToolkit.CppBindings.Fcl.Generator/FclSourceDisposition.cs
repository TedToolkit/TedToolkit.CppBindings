// -----------------------------------------------------------------------
// <copyright file="FclSourceDisposition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Describes one installed FCL header and its finite-profile reachability.</summary>
internal sealed record FclSourceDisposition(string Header, string Disposition);
