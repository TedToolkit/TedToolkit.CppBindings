// -----------------------------------------------------------------------
// <copyright file="ValidateTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Abi.Contracts;
using TedToolkit.Occt.Generator.Abi.Validation;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Abi.Validation.AbiContractValidatorTests;

/// <summary>
/// Verifies <see cref="AbiContractValidator.Validate(AbiOperationModel)"/>.
/// </summary>
internal sealed class ValidateTests
{
    /// <summary>
    /// Verifies that an incomplete transport mapping fails closed with one complete deterministic diagnostic.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_reject_an_incomplete_projection_without_falling_back_to_existing_spellings_Async()
    {
        var operation = new AbiOperationModel()
        {
            OwnerId = "owner",
            OperationId = "unsupported",
            Kind = AbiOperationKind.Method,
            Receiver = AbiReceiverKind.None,
            Declaration = "Owner::Unsupported",
            SourceLocation = "Owner.hxx:42:7",
            Parameters =
            [
                new()
                {
                    Name = "items",
                    Direction = AbiDirection.In,
                    Nullability = AbiNullability.Required,
                    Ownership = AbiOwnership.Borrowed,
                    Type = new()
                    {
                        SourceCppTypeName = "const std::vector<int>&",
                        TransportId = null,
                        CAbiTypeName = null,
                        CppAdapterTypeName = null,
                        ManagedTransportType = null,
                        PublicManagedType = new("IReadOnlyList<int>"),
                    },
                },
            ],
            Result = null,
        };

        var diagnostic = AbiContractValidator.Validate(operation);

        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Code).IsEqualTo("TEDOCCTABI001");
        await Assert.That(diagnostic.Declaration).IsEqualTo("Owner::Unsupported");
        await Assert.That(diagnostic.SourceLocation).IsEqualTo("Owner.hxx:42:7");
        await Assert.That(diagnostic.SourceType).IsEqualTo("const std::vector<int>&");
        await Assert.That(diagnostic.Direction).IsEqualTo(AbiDirection.In);
        await Assert.That(diagnostic.Ownership).IsEqualTo(AbiOwnership.Borrowed);
        await Assert.That(diagnostic.MissingRule).IsEqualTo("abi-transport");
        await Assert.That(diagnostic.ToString()).IsEqualTo(
            "TEDOCCTABI001: declaration='Owner::Unsupported'; location='Owner.hxx:42:7'; "
            + "value='items'; source-type='const std::vector<int>&'; direction='in'; "
            + "ownership='borrowed'; missing-rule='abi-transport'");
    }
}