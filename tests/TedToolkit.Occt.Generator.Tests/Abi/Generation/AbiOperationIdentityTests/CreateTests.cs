// -----------------------------------------------------------------------
// <copyright file="CreateTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Abi.Contracts;
using TedToolkit.Occt.Generator.Abi.Generation;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Abi.Generation.AbiOperationIdentityTests;

/// <summary>
/// Verifies <see cref="AbiOperationIdentity.Create(AbiOperationModel)"/>.
/// </summary>
internal sealed class CreateTests
{
    /// <summary>
    /// Verifies that operation identity follows the exact ABI-major-1 canonical form and digest rule.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_create_the_versioned_symbol_from_canonical_semantic_identity_Async()
    {
        var operation = CreateOperation("const gp_Pnt2d&", "gp_Pnt2d", "Point2d");

        var identity = AbiOperationIdentity.Create(operation);

        await Assert.That(identity.CanonicalIdentity).IsEqualTo(
            "v1|owner=pnt2d|kind=method|operation=coordinates|receiver=borrowed-const|"
            + "parameters=in:required:borrowed:pnt2d|result=out:required:value:f64");
        await Assert.That(identity.SymbolName)
            .IsEqualTo("ted_occt_v1_pnt2d_coordinates__cc785b8f34903fde6e12fbf71afdf636");
    }

    /// <summary>
    /// Verifies that raw C++ and public managed spellings cannot influence an ABI operation symbol.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_ignore_source_and_public_managed_spellings_when_semantics_match_Async()
    {
        var first = AbiOperationIdentity.Create(CreateOperation("const gp_Pnt2d&", "gp_Pnt2d", "Point2d"));
        var second = AbiOperationIdentity.Create(CreateOperation("PointAlias const&", "PointAlias", "RenamedPoint"));

        await Assert.That(second.CanonicalIdentity).IsEqualTo(first.CanonicalIdentity);
        await Assert.That(second.SymbolName).IsEqualTo(first.SymbolName);
    }

    private static AbiOperationModel CreateOperation(
        string sourceCppTypeName,
        string cppAdapterTypeName,
        string publicManagedTypeName)
    {
        return new()
        {
            OwnerId = "pnt2d",
            OperationId = "coordinates",
            Kind = AbiOperationKind.Method,
            Receiver = AbiReceiverKind.BorrowedConst,
            Declaration = "gp_Pnt2d::Coord",
            SourceLocation = "gp_Pnt2d.hxx:255",
            Parameters =
            [
                new()
                {
                    Name = "point",
                    Direction = AbiDirection.In,
                    Nullability = AbiNullability.Required,
                    Ownership = AbiOwnership.Borrowed,
                    Type = new()
                    {
                        SourceCppTypeName = sourceCppTypeName,
                        TransportId = "pnt2d",
                        CAbiTypeName = "const ted_occt_v1_pnt2d*",
                        CppAdapterTypeName = cppAdapterTypeName,
                        ManagedTransportType = new("ted_occt_v1_pnt2d"),
                        PublicManagedType = new(publicManagedTypeName),
                    },
                },
            ],
            Result = new()
            {
                Direction = AbiDirection.Out,
                Nullability = AbiNullability.Required,
                Ownership = AbiOwnership.Value,
                Type = new()
                {
                    SourceCppTypeName = "double",
                    TransportId = "f64",
                    CAbiTypeName = "double*",
                    CppAdapterTypeName = "double",
                    ManagedTransportType = DataType.Double,
                    PublicManagedType = DataType.Double,
                },
            },
        };
    }
}