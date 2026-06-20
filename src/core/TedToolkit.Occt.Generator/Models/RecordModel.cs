// -----------------------------------------------------------------------
// <copyright file="TypeModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the projections of one parsed C++ type across native interop and generated C# surfaces.
/// </summary>
public sealed class RecordModel
{
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    public RecordModel? Base { get; set; }
    public required TypeModel Type { get; init; }

    public required long Size { get; init; }

    public IReadOnlyList<FieldModel> FieldModels { get; set; }
    public IReadOnlyList<MethodModel> MethodModels { get; set; }
}