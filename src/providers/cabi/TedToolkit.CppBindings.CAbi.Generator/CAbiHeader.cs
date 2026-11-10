// -----------------------------------------------------------------------
// <copyright file="CAbiHeader.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.CAbi.Generator;

/// <summary>
/// Identifies a header containing external C linkage functions to bind.
/// </summary>
/// <param name="Path">The source header path.</param>
/// <param name="WrapperClassName">The managed class name, or null to use the header stem followed by Wrapper.</param>
public sealed record CAbiHeader(FilePath Path, string? WrapperClassName = null);