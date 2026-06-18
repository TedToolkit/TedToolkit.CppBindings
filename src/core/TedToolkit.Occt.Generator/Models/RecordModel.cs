// -----------------------------------------------------------------------
// <copyright file="TypeModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the projections of one parsed C++ type across native interop and generated C# surfaces.
/// </summary>
public sealed class RecordModel
{
    public long Size { get; set; }

    public required TypeModel Type { get; init; }
    public required IReadOnlyList<FieldModel> FieldModels { get; init; }
    public required IReadOnlyList<MethodModel> MethodModels { get; init; }
    public required IReadOnlyList<TypeModel> BaseTypes { get; init; }
}