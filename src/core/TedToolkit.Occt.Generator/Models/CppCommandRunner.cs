// -----------------------------------------------------------------------
// <copyright file="CppCommandRunner.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Executes one native build command.
/// </summary>
/// <param name="fileName">The command-line tool name.</param>
/// <param name="arguments">The command arguments.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The observed command result.</returns>
internal delegate Task<CppCommandResult> CppCommandRunner(
    string fileName,
    IReadOnlyList<string> arguments,
    CancellationToken cancellationToken);