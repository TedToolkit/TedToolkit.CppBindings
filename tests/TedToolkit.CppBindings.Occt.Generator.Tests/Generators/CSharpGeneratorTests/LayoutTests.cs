// -----------------------------------------------------------------------
// <copyright file="LayoutTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CSharpGeneratorTests;

/// <summary>
/// Compiles and loads generated storage to check CLR size and alignment.
/// </summary>
internal sealed class LayoutTests
{
    /// <summary>
    /// Verifies unsupported overlapping or over-aligned storage is not emitted as an incorrect struct.
    /// </summary>
    /// <param name="alignment">Required native alignment.</param>
    /// <param name="overlap">Whether native fields overlap.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(4, true)]
    [Arguments(8, false)]
    [Arguments(16, false)]
    public async Task Should_reject_unproved_sequential_storage_Async(int alignment, bool overlap)
    {
        var record = new RecordModel()
        {
            DescriptionItems = [],
            FieldModels = overlap
                ? [CreateField("First", "int", 0, 4), CreateField("Last", "int", 0, 4),]
                : [CreateField("First", "int", 0, 4),],
            MethodModels = [],
            IsAbstract = false,
            IsStandardTransient = false,
            Size = alignment,
            Alignment = alignment,
            SourceHeader = "Storage.hxx",
            Type = new()
            {
                CppTypeName = "Storage",
                CSharpPInvokeType = new("Storage"),
                CSharpPublicType = new("Storage"),
            },
        };
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
            DeclOptions = [],
        });
        await Assert.That(async () =>
            {
                _ = await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            })
            .Throws<NotSupportedException>();
    }

    /// <summary>
    /// Verifies opaque native storage has bounded metadata and retains its alignment in a holder.
    /// </summary>
    /// <param name="size">Native storage size.</param>
    /// <param name="alignment">Native storage alignment.</param>
    /// <param name="fieldOffset">Optional first public field offset.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(16, 8, -1)]
    [Arguments(131088, 8, -1)]
    [Arguments(4, 4, -1)]
    [Arguments(12, 4, -1)]
    [Arguments(6, 2, -1)]
    [Arguments(3, 1, -1)]
    [Arguments(24, 8, 8)]
    [Arguments(16, 4, 0)]
    public async Task Should_load_aligned_opaque_storage_Async(int size, int alignment, int fieldOffset)
    {
        var record = new RecordModel()
        {
            DescriptionItems = [],
            FieldModels = fieldOffset < 0
                ? []
                : [CreateField("First", "byte", fieldOffset, 1), CreateField("Last", "int", fieldOffset + 8, 4),],
            MethodModels = [],
            IsAbstract = false,
            IsStandardTransient = false,
            Size = size,
            Alignment = alignment,
            SourceHeader = "Storage.hxx",
            Type = new()
            {
                CppTypeName = "Storage",
                CSharpPInvokeType = new("Storage"),
                CSharpPublicType = new("Storage"),
            },
        };
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe",
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
            DeclOptions = [],
        });
        var source = await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await Assert.That(source.Split("__padding", StringSplitOptions.None).Length - 1).IsLessThanOrEqualTo(8);
        var fieldChecks = fieldOffset < 0
            ? ""
            : $"&& (byte*)&holder.Value.First - (byte*)&holder.Value == {fieldOffset} "
              + $"&& (byte*)&holder.Value.Last - (byte*)&holder.Value == {fieldOffset + 8}";
        var probe = $$"""
            namespace LayoutProbe
            {
                [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
                public struct Holder { public byte Prefix; public Storage Value; }
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        Holder holder = default;
                        return sizeof(Storage) == {{size}}
                            && (byte*)&holder.Value - (byte*)&holder == {{alignment}}
                            {{fieldChecks}};
                    }
                }
            }
            """;
        await AssertCompiledStorageAsync(source, probe).ConfigureAwait(false);
    }

    /// <summary>
    /// Compiles emitted storage and executes the supplied managed layout and semantics oracle.
    /// </summary>
    /// <param name="source">Generated storage declaration.</param>
    /// <param name="probe">Companion source containing LayoutProbe.Probe.Check.</param>
    /// <param name="storageTypeName">Metadata name of the emitted storage declaration.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    internal static async Task AssertCompiledStorageAsync(
        string source, string probe, string storageTypeName = "LayoutProbe.Storage")
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var compilation = CSharpCompilation.Create(
            "LayoutProbe_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText(probe),],
            paths.Append(typeof(NativeTypeNameAttribute).Assembly.Location)
                .Append(typeof(IStandard_Transient).Assembly.Location)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(static path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var stream = new MemoryStream();
        await using var streamLifetime = stream.ConfigureAwait(false);
        var result = compilation.Emit(stream);
        await Assert.That(string.Join(Environment.NewLine, result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error))).IsEmpty();
        stream.Position = 0;
        var context = new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var type = assembly.GetType(storageTypeName, throwOnError: true)!;
            await Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Length)
                .IsLessThanOrEqualTo(8);
            var check = assembly.GetType("LayoutProbe.Probe", throwOnError: true)!.GetMethod("Check")!;
            await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
        }
        finally
        {
            context.Unload();
        }
    }

    private static FieldModel CreateField(string name, string type, int offset, int size)
    {
        return new()
        {
            DescriptionItems = [],
            Name = name,
            Offset = offset,
            Size = size,
            Alignment = size,
            Type = new()
            {
                CppTypeName = type,
                CSharpPInvokeType = new(type),
                CSharpPublicType = new(type),
            },
        };
    }
}