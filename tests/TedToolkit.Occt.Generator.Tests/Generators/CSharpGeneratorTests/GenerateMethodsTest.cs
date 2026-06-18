// -----------------------------------------------------------------------
// <copyright file="GenerateMethodsTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;

namespace TedToolkit.Occt.Generator.Tests.Generators.CSharpGeneratorTests;

/// <summary>
/// Tests for method generation in <see cref="CSharpGenerator"/>.
/// </summary>
internal sealed class GenerateMethodsTest
{
    /// <summary>
    /// Verifies an instance C++ method is projected to a public wrapper and a private native entry point.
    /// </summary>
    /// <returns>A task that completes when the assertions have finished.</returns>
    [Test]
    public async Task Should_generate_public_wrapper_and_native_pinvoke_for_instance_method_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct gp_Pnt2d
            {
                void SetCoord(int theIndex, double theXi);
            };
            """);

        var recordDecl = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static x => x.Name == "gp_Pnt2d");

        var typeService = new TypeService(
            Microsoft.Extensions.Options.Options.Create(
                new GenerationOptions()
                {
                    DeclOptions = [],
                    CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                    CppFolder = new DirectoryInfo(Path.GetTempPath()),
                }),
            new Resolver([]));
        var generator = new CSharpGenerator(
            recordDecl,
            new RecordService(typeService, new FieldService()),
            new RecordLayoutServiceStub(),
            Microsoft.Extensions.Options.Options.Create(
                new GenerationOptions()
                {
                    DeclOptions = [],
                    CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                    CppFolder = new DirectoryInfo(Path.GetTempPath()),
                }),
            typeService,
            new FieldService());

        var code = await generator.GenerateAsync(CancellationToken.None);

        await Assert.That(code).Contains("public void SetCoord(int theIndex, double theXi)");
        await Assert.That(code).Contains("fixed (gp_Pnt2d* selfPtr = &this)");
        await Assert.That(code).Contains("SetCoordNative(selfPtr, theIndex, theXi);");
        await Assert.That(code).Contains("[DllImport(\"Name\", CallingConvention = CallingConvention.Cdecl, EntryPoint = \"gp_Pnt2d_SetCoord_int_double\")]");
        await Assert.That(code).Contains("private static extern void SetCoordNative(gp_Pnt2d* self, int theIndex, double theXi);");
    }

    private static TranslationUnit ParseTranslationUnit(string source)
    {
        using var file = CXUnsavedFile.Create("test.cpp", source);
        var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            "test.cpp",
            ["-std=c++20", "-x", "c++",],
            [file,],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        return TranslationUnit.GetOrCreate(translationUnit);
    }

    private sealed class RecordLayoutServiceStub : IRecordLayoutService
    {
        public Task PrepareAsync(IEnumerable<CXXRecordDecl> records, ModularPipelines.Context.Domains.IShellContext shell, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public long GetSize(CXXRecordDecl record)
        {
            return 1;
        }

        public long GetOffset(CXXRecordDecl record, FieldDecl field)
        {
            throw new NotSupportedException();
        }
    }
}
