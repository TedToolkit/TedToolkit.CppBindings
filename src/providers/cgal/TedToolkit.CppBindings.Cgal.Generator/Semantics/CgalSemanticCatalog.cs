// -----------------------------------------------------------------------
// <copyright file="CgalSemanticCatalog.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Converts discovered finite-profile declarations into the one Shared semantic model authority.
/// </summary>
internal static class CgalSemanticCatalog
{
    private const string Namespace = "TedToolkit::CppBindings::Cgal";

    private static readonly Dictionary<string, ArtifactSpec> Artifacts =
        new(StringComparer.Ordinal)
        {
            ["point-2"] = new("Point_2", null),
            ["point-2-cartesian"] = new("Point_2.Cartesian", "Cgal_Point2_Cartesian"),
            ["point-2-constructor"] = new("Point_2.Create", "Cgal_Point2_Create"),
            ["point-2-x"] = new("Point_2.X", null),
            ["point-2-y"] = new("Point_2.Y", null),
            ["point-3"] = new("Point_3", null),
            ["point-3-cartesian"] = new("Point_3.Cartesian", "Cgal_Point3_Cartesian"),
            ["point-3-constructor"] = new("Point_3.Create", "Cgal_Point3_Create"),
            ["point-3-x"] = new("Point_3.X", null),
            ["point-3-y"] = new("Point_3.Y", null),
            ["point-3-z"] = new("Point_3.Z", null),
            ["segment-2"] = new("Segment_2", null),
            ["segment-2-constructor"] = new("Segment_2.Create", "Cgal_Segment2_Create"),
            ["segment-2-source"] = new("Segment_2.Source", "Cgal_Segment2_Source"),
            ["segment-2-target"] = new("Segment_2.Target", "Cgal_Segment2_Target"),
            ["segment-2-squared-length"] = new("Segment_2.SquaredLength", "Cgal_Segment2_SquaredLength"),
            ["squared-distance-2"] = new("Kernel_API.SquaredDistance(Point_2,Point_2)", "Cgal_Point2_SquaredDistance"),
            ["squared-distance-3"] = new("Kernel_API.SquaredDistance(Point_3,Point_3)", "Cgal_Point3_SquaredDistance"),
            ["intersection-segment-2"] = new("Kernel_API.Intersect", "Cgal_Segment2_Intersection"),
        };

    /// <summary>
    /// Reports whether the provider can project one discovered declaration identity.
    /// </summary>
    /// <param name="declarationId">The declaration identity.</param>
    /// <returns><see langword="true"/> when a Shared model projection exists.</returns>
    internal static bool IsSupported(string declarationId)
    {
        return Artifacts.ContainsKey(declarationId);
    }

    /// <summary>
    /// Creates the non-empty declaration graph consumed by Shared.
    /// </summary>
    /// <param name="admitted">The discovered admitted partition.</param>
    /// <returns>The declaration roots and dependencies.</returns>
    internal static IReadOnlyList<BindingDeclaration> CreateDeclarations(
        IReadOnlyCollection<CgalDeclarationDisposition> admitted)
    {
        var ids = admitted.Select(static item => item.Id).ToHashSet(StringComparer.Ordinal);
        var point2 = CreatePoint2(ids);
        var point3 = CreatePoint3(ids);
        var segment2 = CreateSegment2(ids, point2);
        var intersection = CreateIntersection(ids, point2, segment2);
        var kernel = CreateKernel(ids, point2, point3, segment2, intersection);
        var declarations = new List<BindingDeclaration>();
        Add(declarations, point2, ids.Contains("point-2"), []);
        Add(declarations, point3, ids.Contains("point-3"), []);
        Add(declarations, segment2, ids.Contains("segment-2"), [point2,]);
        Add(declarations, intersection, false, [point2, segment2,]);
        Add(declarations, kernel, kernel is not null, [point2, point3, segment2, intersection,]);
        return Array.AsReadOnly(declarations.ToArray());
    }

    /// <summary>
    /// Derives per-declaration managed artifact identities.
    /// </summary>
    /// <param name="admitted">The admitted declarations.</param>
    /// <returns>The managed artifact inventory.</returns>
    internal static IReadOnlyList<CgalArtifactInventoryEntry> CreateManagedArtifacts(
        IReadOnlyCollection<CgalDeclarationDisposition> admitted)
    {
        return CreateArtifacts(admitted, ".g.cs", useNativeSymbol: false);
    }

    /// <summary>
    /// Derives per-declaration native artifact identities.
    /// </summary>
    /// <param name="admitted">The admitted declarations.</param>
    /// <returns>The native artifact inventory.</returns>
    internal static IReadOnlyList<CgalArtifactInventoryEntry> CreateNativeArtifacts(
        IReadOnlyCollection<CgalDeclarationDisposition> admitted)
    {
        return CreateArtifacts(admitted, ".cpp", useNativeSymbol: true);
    }

    private static ReadOnlyCollection<CgalArtifactInventoryEntry> CreateArtifacts(
        IReadOnlyCollection<CgalDeclarationDisposition> admitted,
        string suffix,
        bool useNativeSymbol)
    {
        return Array.AsReadOnly(admitted.OrderBy(static item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                var artifact = Artifacts[item.Id];
                var separator = artifact.Symbol.IndexOf('.', StringComparison.Ordinal);
                var record = separator < 0 ? artifact.Symbol : artifact.Symbol[..separator];
                var path = suffix == ".g.cs"
                    ? record + suffix
                    : $"{Namespace.Replace("::", "_", StringComparison.Ordinal)}_{record}{suffix}";
                var symbol = useNativeSymbol ? artifact.NativeExport ?? artifact.Symbol : artifact.Symbol;
                return new CgalArtifactInventoryEntry(item.Id, path, symbol);
            }).ToArray());
    }

    private static void Add(
        List<BindingDeclaration> declarations,
        RecordModel? record,
        bool isRoot,
        IReadOnlyList<RecordModel?> dependencies)
    {
        if (record is null)
        {
            return;
        }

        declarations.Add(new(
            record,
            record.Type.CSharpTypeName,
            isRoot,
            dependencies.Where(static item => item is not null)
                .Select(static item => item!.Type.CppTypeName)));
    }

    private static RecordModel? CreatePoint2(HashSet<string> ids)
    {
        if (!ids.Contains("point-2"))
        {
            return null;
        }

        var record = CreateRecord("Point_2", 16, 8);
        record.FieldModels =
        [
            Field("X", 0, Double()),
            Field("Y", 8, Double()),
        ];
        var methods = new List<MethodModel>();
        if (ids.Contains("point-2-constructor"))
        {
            methods.Add(Constructor("Cgal_Point2_Create", [Parameter("x", Double()), Parameter("y", Double()),]));
        }

        if (ids.Contains("point-2-cartesian"))
        {
            methods.Add(Method("Cartesian", "Cgal_Point2_Cartesian", Double(), false, [Parameter("index", Int()),]));
        }

        record.MethodModels = Array.AsReadOnly(methods.ToArray());
        return record;
    }

    private static RecordModel? CreatePoint3(HashSet<string> ids)
    {
        if (!ids.Contains("point-3"))
        {
            return null;
        }

        var record = CreateRecord("Point_3", 24, 8);
        record.FieldModels =
        [
            Field("X", 0, Double()),
            Field("Y", 8, Double()),
            Field("Z", 16, Double()),
        ];
        var methods = new List<MethodModel>();
        if (ids.Contains("point-3-constructor"))
        {
            methods.Add(Constructor(
                "Cgal_Point3_Create",
                [Parameter("x", Double()), Parameter("y", Double()), Parameter("z", Double()),]));
        }

        if (ids.Contains("point-3-cartesian"))
        {
            methods.Add(Method("Cartesian", "Cgal_Point3_Cartesian", Double(), false, [Parameter("index", Int()),]));
        }

        record.MethodModels = Array.AsReadOnly(methods.ToArray());
        return record;
    }

    private static RecordModel? CreateSegment2(HashSet<string> ids, RecordModel? point2)
    {
        if (!ids.Contains("segment-2") || point2 is null)
        {
            return null;
        }

        var pointType = RecordType(point2);
        var record = CreateRecord("Segment_2", 32, 8);
        record.FieldModels =
        [
            Field("Source", 0, pointType, 16, 8),
            Field("Target", 16, pointType, 16, 8),
        ];
        var methods = new List<MethodModel>();
        if (ids.Contains("segment-2-constructor"))
        {
            methods.Add(Constructor(
                "Cgal_Segment2_Create",
                [Parameter("source", pointType), Parameter("target", pointType),]));
        }

        if (ids.Contains("segment-2-source"))
        {
            methods.Add(Method("Source", "Cgal_Segment2_Source", pointType, false, []));
        }

        if (ids.Contains("segment-2-target"))
        {
            methods.Add(Method("Target", "Cgal_Segment2_Target", pointType, false, []));
        }

        if (ids.Contains("segment-2-squared-length"))
        {
            methods.Add(Method("SquaredLength", "Cgal_Segment2_SquaredLength", Double(), false, []));
        }

        record.MethodModels = Array.AsReadOnly(methods.ToArray());
        return record;
    }

    private static RecordModel? CreateIntersection(
        HashSet<string> ids,
        RecordModel? point2,
        RecordModel? segment2)
    {
        if (!ids.Contains("intersection-segment-2") || point2 is null || segment2 is null)
        {
            return null;
        }

        var record = CreateRecord("Segment_2_Intersection_Transport", 40, 8);
        record.FieldModels =
        [
            Field("Tag", 0, Int(), 4, 4),
            Field("AX", 8, Double()),
            Field("AY", 16, Double()),
            Field("BX", 24, Double()),
            Field("BY", 32, Double()),
        ];
        record.MethodModels = [];
        return record;
    }

    private static RecordModel? CreateKernel(
        HashSet<string> ids,
        RecordModel? point2,
        RecordModel? point3,
        RecordModel? segment2,
        RecordModel? intersection)
    {
        var methods = new List<MethodModel>();
        if (ids.Contains("squared-distance-2") && point2 is not null)
        {
            var type = RecordType(point2);
            methods.Add(Method(
                "SquaredDistance",
                "Cgal_Point2_SquaredDistance",
                Double(),
                true,
                [Parameter("left", type), Parameter("right", type),]));
        }

        if (ids.Contains("squared-distance-3") && point3 is not null)
        {
            var type = RecordType(point3);
            methods.Add(Method(
                "SquaredDistance",
                "Cgal_Point3_SquaredDistance",
                Double(),
                true,
                [Parameter("left", type), Parameter("right", type),]));
        }

        if (ids.Contains("intersection-segment-2") && segment2 is not null && intersection is not null)
        {
            var type = RecordType(segment2);
            methods.Add(Method(
                "Intersect",
                "Cgal_Segment2_Intersection",
                RecordType(intersection),
                true,
                [Parameter("left", type), Parameter("right", type),]));
        }

        if (methods.Count == 0)
        {
            return null;
        }

        var record = CreateRecord("Kernel_API", 1, 1);
        record.FieldModels = [];
        record.MethodModels = Array.AsReadOnly(methods.ToArray());
        return record;
    }

    private static RecordModel CreateRecord(string name, long size, long alignment)
    {
        return new()
        {
            DescriptionItems = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            SourceHeader = "CgalProfileAdapter.hpp",
            Type = new()
            {
                CppTypeName = $"{Namespace}::{name}",
                CppValueTypeName = $"{Namespace}::{name}",
                CSharpPInvokeType = new(name),
                CSharpPublicType = new(name),
                IsRecord = true,
            },
            Size = size,
            Alignment = alignment,
            ObjectKind = NativeObjectKind.Value,
            FieldModels = [],
            MethodModels = [],
        };
    }

    private static FieldModel Field(
        string name,
        long offset,
        TypeModel type,
        long size = 8,
        long alignment = 8)
    {
        return new()
        {
            DescriptionItems = [],
            Name = name,
            Offset = offset,
            Size = size,
            Alignment = alignment,
            Type = type,
        };
    }

    private static MethodModel Constructor(string export, IReadOnlyList<ParameterModel> parameters)
    {
        return new()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NativeExportName = export,
            NoExceptions = true,
            IsConst = false,
            IsStatic = false,
            ReturnType = Void(),
            MethodName = "Create",
            Type = MethodModelType.NEW,
            Parameters = parameters,
        };
    }

    private static MethodModel Method(
        string name,
        string export,
        TypeModel result,
        bool isStatic,
        IReadOnlyList<ParameterModel> parameters)
    {
        return new()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NativeExportName = export,
            NoExceptions = false,
            IsConst = !isStatic,
            IsStatic = isStatic,
            ReturnType = result,
            MethodName = name,
            Type = MethodModelType.NORMAL,
            Parameters = parameters,
        };
    }

    private static ParameterModel Parameter(string name, TypeModel type)
    {
        return new()
        {
            DescriptionItems = [],
            Name = name,
            Type = type,
        };
    }

    private static TypeModel Double()
    {
        return Scalar("double");
    }

    private static TypeModel Int()
    {
        return Scalar("int");
    }

    private static TypeModel Void()
    {
        return Scalar("void");
    }

    private static TypeModel Scalar(string name)
    {
        return new()
        {
            CppTypeName = name,
            CppValueTypeName = name,
            CSharpPInvokeType = new(name),
            CSharpPublicType = new(name),
        };
    }

    private static TypeModel RecordType(RecordModel record)
    {
        return new()
        {
            ReferencedRecord = record,
            CppTypeName = record.Type.CppTypeName,
            CppValueTypeName = record.Type.CppTypeName,
            CSharpPInvokeType = record.Type.CSharpPInvokeType,
            CSharpPublicType = record.Type.CSharpPublicType,
            IsRecord = true,
        };
    }

    private sealed record ArtifactSpec(string Symbol, string? NativeExport);
}
