// -----------------------------------------------------------------------
// <copyright file="RecordLayoutModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Generated;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Prepares native C++ record layout data before generation starts.
/// </summary>
/// <param name="recordManager">The record queue manager.</param>
/// <param name="recordLayoutService">The native record layout service.</param>
[DependsOn<RecordModule>]
public sealed class RecordLayoutModule(
    IRecordManager recordManager,
    IRecordLayoutService recordLayoutService) : Module<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await recordLayoutService.PrepareAsync(recordManager.GetRecords(), context.Shell, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}