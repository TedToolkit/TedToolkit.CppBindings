// -----------------------------------------------------------------------
// <copyright file="AbiV1ConformanceCatalog.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Abi.Contracts;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Abi.Conformance;

/// <summary>
/// Supplies the frozen semantic operations and explicit mappings for the ABI-major-1 conformance slice.
/// </summary>
internal static class AbiV1ConformanceCatalog
{
    /// <summary>
    /// Creates the point, transient Cartesian-point, and UTF-8 operation contracts.
    /// </summary>
    /// <returns>The complete ABI-major-1 conformance operation set.</returns>
    public static IReadOnlyList<AbiOperationModel> CreateOperations()
    {
        return
        [
            CreateOperation("pnt2d", "create", AbiOperationKind.Constructor, AbiReceiverKind.None,
                "gp_Pnt2d::gp_Pnt2d", "gp_Pnt2d.hxx:64:3",
                [ValueParameter("x", Scalar("double", "f64", "double", DataType.Double)),
                    ValueParameter("y", Scalar("double", "f64", "double", DataType.Double)),],
                Result(AbiOwnership.Value, SemanticPoint("ted_occt_v1_pnt2d*"))),
            CreateOperation("pnt2d", "get_x", AbiOperationKind.Method, AbiReceiverKind.BorrowedConst,
                "gp_Pnt2d::X", "gp_Pnt2d.hxx:109:3",
                [BorrowedParameter("point", SemanticPoint("ted_occt_v1_pnt2d")),],
                Result(AbiOwnership.Value, Scalar("double", "f64", "double*", DataType.Double))),
            CreateOperation("pnt2d", "get_y", AbiOperationKind.Method, AbiReceiverKind.BorrowedConst,
                "gp_Pnt2d::Y", "gp_Pnt2d.hxx:114:3",
                [BorrowedParameter("point", SemanticPoint("ted_occt_v1_pnt2d")),],
                Result(AbiOwnership.Value, Scalar("double", "f64", "double*", DataType.Double))),
            CreateOperation("geom2d_cartesian_point", "create", AbiOperationKind.Constructor, AbiReceiverKind.None,
                "Geom2d_CartesianPoint::Geom2d_CartesianPoint", "Geom2d_CartesianPoint.hxx:37:3",
                [ValueParameter("point", SemanticPoint("ted_occt_v1_pnt2d")),],
                Result(AbiOwnership.Owned, CartesianHandle("ted_occt_v1_geom2d_cartesian_point**"))),
            CreateOperation("geom2d_cartesian_point", "get_point", AbiOperationKind.Method,
                AbiReceiverKind.BorrowedConst, "Geom2d_CartesianPoint::Pnt2d", "Geom2d_CartesianPoint.hxx:55:3",
                [BorrowedParameter("self", CartesianHandle("const ted_occt_v1_geom2d_cartesian_point*")),],
                Result(AbiOwnership.Value, SemanticPoint("ted_occt_v1_pnt2d*"))),
            CreateOperation("geom2d_cartesian_point", "set_point", AbiOperationKind.Method,
                AbiReceiverKind.BorrowedMutable, "Geom2d_CartesianPoint::SetPnt2d",
                "Geom2d_CartesianPoint.hxx:50:3",
                [BorrowedParameter("self", CartesianHandle("ted_occt_v1_geom2d_cartesian_point*")),
                    ValueParameter("point", SemanticPoint("ted_occt_v1_pnt2d")),], null),
            CreateOperation("geom2d_cartesian_point", "retain", AbiOperationKind.Retain,
                AbiReceiverKind.BorrowedConst, "Geom2d_CartesianPoint retain", "generated:retain",
                [BorrowedParameter("self", CartesianHandle("const ted_occt_v1_geom2d_cartesian_point*")),],
                Result(AbiOwnership.Owned, CartesianHandle("ted_occt_v1_geom2d_cartesian_point**"))),
            CreateOperation("geom2d_cartesian_point", "release", AbiOperationKind.Release,
                AbiReceiverKind.None, "Geom2d_CartesianPoint release", "generated:release",
                [OwnedInOutParameter("owner", CartesianHandle("ted_occt_v1_geom2d_cartesian_point**")),], null),
            CreateOperation("ascii_string", "copy_utf8", AbiOperationKind.Method, AbiReceiverKind.None,
                "TCollection_AsciiString UTF-8 copy", "TCollection_AsciiString.hxx:242:3",
                [BorrowedParameter("text", Utf8View()),], Result(AbiOwnership.Owned, OwnedBytes())),
        ];
    }

    private static AbiOperationModel CreateOperation(
        string ownerId,
        string operationId,
        AbiOperationKind kind,
        AbiReceiverKind receiver,
        string declaration,
        string sourceLocation,
        IReadOnlyList<AbiParameterModel> parameters,
        AbiResultModel? result)
    {
        return new()
        {
            OwnerId = ownerId,
            OperationId = operationId,
            Kind = kind,
            Receiver = receiver,
            Declaration = declaration,
            SourceLocation = sourceLocation,
            Parameters = parameters,
            Result = result,
        };
    }

    private static AbiParameterModel ValueParameter(string name, AbiTypeProjectionModel type)
    {
        return Parameter(name, AbiDirection.In, AbiOwnership.Value, type);
    }

    private static AbiParameterModel BorrowedParameter(string name, AbiTypeProjectionModel type)
    {
        return Parameter(name, AbiDirection.In, AbiOwnership.Borrowed, type);
    }

    private static AbiParameterModel OwnedInOutParameter(string name, AbiTypeProjectionModel type)
    {
        return Parameter(name, AbiDirection.InOut, AbiOwnership.Owned, type);
    }

    private static AbiParameterModel Parameter(
        string name,
        AbiDirection direction,
        AbiOwnership ownership,
        AbiTypeProjectionModel type)
    {
        return new()
        {
            Name = name,
            Direction = direction,
            Nullability = AbiNullability.Required,
            Ownership = ownership,
            Type = type,
        };
    }

    private static AbiResultModel Result(AbiOwnership ownership, AbiTypeProjectionModel type)
    {
        return new()
        {
            Direction = AbiDirection.Out,
            Nullability = AbiNullability.Required,
            Ownership = ownership,
            Type = type,
        };
    }

    private static AbiTypeProjectionModel Scalar(
        string sourceCppTypeName,
        string transportId,
        string cAbiTypeName,
        DataType managedType)
    {
        return Type(sourceCppTypeName, transportId, cAbiTypeName, sourceCppTypeName, managedType, managedType);
    }

    private static AbiTypeProjectionModel SemanticPoint(string cAbiTypeName)
    {
        return Type("gp_Pnt2d", "pnt2d", cAbiTypeName, "gp_Pnt2d", new("ted_occt_v1_pnt2d"),
            new("gp_Pnt2d"));
    }

    private static AbiTypeProjectionModel CartesianHandle(string cAbiTypeName)
    {
        return Type("opencascade::handle<Geom2d_CartesianPoint>", "geom2d_cartesian_point_handle", cAbiTypeName,
            "opencascade::handle<Geom2d_CartesianPoint>", new DataType("ted_occt_v1_geom2d_cartesian_point").Pointer,
            new("Geom2d_CartesianPoint"));
    }

    private static AbiTypeProjectionModel Utf8View()
    {
        return Type("TCollection_AsciiString", "bytes_view", "ted_occt_v1_bytes_view",
            "TCollection_AsciiString", new("ted_occt_v1_bytes_view"), DataType.String);
    }

    private static AbiTypeProjectionModel OwnedBytes()
    {
        return Type("TCollection_AsciiString", "owned_bytes", "ted_occt_v1_owned_bytes*",
            "TCollection_AsciiString", new("ted_occt_v1_owned_bytes"), DataType.String);
    }

    private static AbiTypeProjectionModel Type(
        string sourceCppTypeName,
        string transportId,
        string cAbiTypeName,
        string cppAdapterTypeName,
        DataType managedTransportType,
        DataType publicManagedType)
    {
        return new()
        {
            SourceCppTypeName = sourceCppTypeName,
            TransportId = transportId,
            CAbiTypeName = cAbiTypeName,
            CppAdapterTypeName = cppAdapterTypeName,
            ManagedTransportType = managedTransportType,
            PublicManagedType = publicManagedType,
        };
    }
}