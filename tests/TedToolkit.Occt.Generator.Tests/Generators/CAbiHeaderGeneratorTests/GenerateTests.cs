// -----------------------------------------------------------------------
// <copyright file="GenerateTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Generators.CAbiHeaderGeneratorTests;

/// <summary>
/// Verifies <see cref="CAbiHeaderGenerator.Generate(IEnumerable{AbiOperationModel})"/>.
/// </summary>
internal sealed class GenerateTests
{
    /// <summary>
    /// Verifies that permuted semantic input produces one canonical fail-closed C11 header.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_the_same_restricted_header_for_every_input_order_Async()
    {
        var point = CreatePointOperation();
        var utf8 = CreateUtf8Operation();
        var unsupported = CreateUnsupportedOperation();

        var first = CAbiHeaderGenerator.Generate([utf8, unsupported, point,]);
        var second = CAbiHeaderGenerator.Generate([point, utf8, unsupported,]);

        await Assert.That(second.Header).IsEqualTo(first.Header);
        await Assert.That(first.Diagnostics).Count().IsEqualTo(1);
        await Assert.That(first.Diagnostics[0].Declaration).IsEqualTo("Owner::Unsupported");
        await Assert.That(first.Header).Contains("typedef struct ted_occt_v1_pnt2d");
        await Assert.That(first.Header).Contains("double x;\n    double y;");
        await Assert.That(first.Header).Contains(
            "typedef struct ted_occt_v1_geom2d_cartesian_point ted_occt_v1_geom2d_cartesian_point;");
        await Assert.That(first.Header).Contains("typedef struct ted_occt_v1_bytes_view");
        await Assert.That(first.Header).Contains("typedef struct ted_occt_v1_owned_bytes");
        await Assert.That(first.Header).Contains("typedef int32_t ted_occt_v1_error_kind;");
        await Assert.That(first.Header).Contains("ted_occt_v1_abi_version(void);");
        await Assert.That(first.Header).Contains(AbiOperationIdentity.Create(point).SymbolName);
        await Assert.That(first.Header).Contains(AbiOperationIdentity.Create(utf8).SymbolName);
        await Assert.That(first.Header).DoesNotContain("Owner::Unsupported");
        await Assert.That(first.Header).DoesNotContain("gp_Pnt2d");
        await Assert.That(first.Header).DoesNotContain("TCollection_AsciiString");
        await Assert.That(first.Header).DoesNotContain("std::");
        await Assert.That(first.Header).DoesNotContain("IReadOnlyList");
    }

    /// <summary>
    /// Verifies that the canonical header compiles as strict C11 and C++ without OCCT include paths.
    /// </summary>
    /// <returns>A task that completes when both compiler assertions have finished.</returns>
    [Test]
    [NotInParallel("abi-header-compiler")]
    public async Task Should_compile_as_c11_and_cpp_without_native_dependency_headers_Async()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            var generated = CAbiHeaderGenerator.Generate([CreatePointOperation(), CreateUtf8Operation(),]);
            var headerPath = Path.Combine(directory.FullName, "ted_toolkit_occt_v1.h");
            var cPath = Path.Combine(directory.FullName, "consumer.c");
            var cppPath = Path.Combine(directory.FullName, "consumer.cpp");
            await File.WriteAllTextAsync(headerPath, generated.Header).ConfigureAwait(false);
            await File.WriteAllTextAsync(cPath, CreateConsumerSource()).ConfigureAwait(false);
            await File.WriteAllTextAsync(cppPath, CreateConsumerSource()).ConfigureAwait(false);

            var cResult = await CompileAsync(directory, "/TC", "/std:c11", cPath, "consumer-c.obj")
                .ConfigureAwait(false);
            var cppResult = await CompileAsync(directory, "/TP", "/std:c++20", cppPath, "consumer-cpp.obj")
                .ConfigureAwait(false);

            await Assert.That(cResult.ExitCode).IsEqualTo(0);
            await Assert.That(cResult.StandardError).IsEmpty();
            await Assert.That(cppResult.ExitCode).IsEqualTo(0);
            await Assert.That(cppResult.StandardError).IsEmpty();
        }
        finally
        {
            if (directory.Exists)
            {
                directory.Delete(true);
            }
        }
    }

    private static async Task<CppCommandResult> CompileAsync(
        DirectoryInfo directory,
        string language,
        string standard,
        string sourcePath,
        string outputName)
    {
        using var process = new Process()
        {
            StartInfo = new()
            {
                FileName = "clang-cl",
                WorkingDirectory = directory.FullName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("/nologo");
        process.StartInfo.ArgumentList.Add(language);
        process.StartInfo.ArgumentList.Add(standard);
        process.StartInfo.ArgumentList.Add("/W4");
        process.StartInfo.ArgumentList.Add("/WX");
        process.StartInfo.ArgumentList.Add("/c");
        process.StartInfo.ArgumentList.Add(sourcePath);
        process.StartInfo.ArgumentList.Add($"/Fo{Path.Combine(directory.FullName, outputName)}");

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start clang-cl.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new(process.ExitCode, await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static string CreateConsumerSource()
    {
        return "#include \"ted_toolkit_occt_v1.h\"\n"
               + "static ted_occt_v1_pnt2d point = { 1.0, 2.0 };\n"
               + "int consume_header(void) { return point.x == 1.0 ? 0 : 1; }\n";
    }

    private static AbiOperationModel CreatePointOperation()
    {
        return new()
        {
            OwnerId = "pnt2d",
            OperationId = "create",
            Kind = AbiOperationKind.Constructor,
            Receiver = AbiReceiverKind.None,
            Declaration = "gp_Pnt2d::gp_Pnt2d",
            SourceLocation = "gp_Pnt2d.hxx:64:3",
            Parameters =
            [
                CreateParameter("x", "double", "f64", "double", DataType.Double, DataType.Double),
                CreateParameter("y", "double", "f64", "double", DataType.Double, DataType.Double),
            ],
            Result = new()
            {
                Direction = AbiDirection.Out,
                Nullability = AbiNullability.Required,
                Ownership = AbiOwnership.Value,
                Type = CreateType("gp_Pnt2d", "pnt2d", "ted_occt_v1_pnt2d*", "gp_Pnt2d",
                    new("ted_occt_v1_pnt2d"), new("gp_Pnt2d")),
            },
        };
    }

    private static AbiOperationModel CreateUtf8Operation()
    {
        return new()
        {
            OwnerId = "ascii_string",
            OperationId = "copy_utf8",
            Kind = AbiOperationKind.Method,
            Receiver = AbiReceiverKind.None,
            Declaration = "TCollection_AsciiString::ToCString",
            SourceLocation = "TCollection_AsciiString.hxx:242:3",
            Parameters =
            [
                CreateParameter("text", "TCollection_AsciiString", "bytes_view", "ted_occt_v1_bytes_view",
                    new("ted_occt_v1_bytes_view"), DataType.String),
            ],
            Result = new()
            {
                Direction = AbiDirection.Out,
                Nullability = AbiNullability.Required,
                Ownership = AbiOwnership.Owned,
                Type = CreateType("TCollection_AsciiString", "owned_bytes", "ted_occt_v1_owned_bytes*",
                    "TCollection_AsciiString", new("ted_occt_v1_owned_bytes"), DataType.String),
            },
        };
    }

    private static AbiOperationModel CreateUnsupportedOperation()
    {
        return new()
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
    }

    private static AbiParameterModel CreateParameter(
        string name,
        string sourceCppTypeName,
        string transportId,
        string cAbiTypeName,
        DataType managedTransportType,
        DataType publicManagedType)
    {
        return new()
        {
            Name = name,
            Direction = AbiDirection.In,
            Nullability = AbiNullability.Required,
            Ownership = AbiOwnership.Value,
            Type = CreateType(sourceCppTypeName, transportId, cAbiTypeName, sourceCppTypeName,
                managedTransportType, publicManagedType),
        };
    }

    private static AbiTypeProjectionModel CreateType(
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