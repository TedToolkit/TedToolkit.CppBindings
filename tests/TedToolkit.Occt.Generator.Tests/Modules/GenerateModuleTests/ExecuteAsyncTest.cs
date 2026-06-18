// -----------------------------------------------------------------------
// <copyright file="ExecuteAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Microsoft.Extensions.Options;

using ModularPipelines.Context;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Tests.Modules.GenerateModuleTests;

/// <summary>
/// <see cref="GenerateModule"/> execution.
/// </summary>
internal sealed class ExecuteAsyncTest
{
    /// <summary>
    /// Verifies the module materializes the shared interop header into the C++ output folder even when no records are generated.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_copy_csharp_interop_header_into_cpp_output_folder_Async()
    {
        var rootDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var cppDirectory = rootDirectory.CreateSubdirectory("cpp");
        var csharpDirectory = rootDirectory.CreateSubdirectory("csharp");

        try
        {
            var module = new GenerateModule(
                Microsoft.Extensions.Options.Options.Create(new GenerationOptions
                {
                    DeclOptions = [],
                    CSharpFolder = csharpDirectory,
                    CppFolder = cppDirectory,
                }),
                new EmptyRecordModelManager(),
                new ThrowingGeneratorService());

            var context = Mock.Of<IModuleContext>();
            var executeAsyncMethod = typeof(GenerateModule).GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            await Assert.That(executeAsyncMethod).IsNotNull();

            var task = (Task<bool>)executeAsyncMethod!
                .Invoke(module, [context, CancellationToken.None])!;

            var result = await task.ConfigureAwait(false);
            var interopHeaderPath = Path.Combine(cppDirectory.FullName, "csharp_interop.h");

            await Assert.That(result).IsTrue();
            await Assert.That(File.Exists(interopHeaderPath)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(interopHeaderPath).ConfigureAwait(false))
                .Contains("#ifndef CSHARP_INTEROP_H");
        }
        finally
        {
            if (rootDirectory.Exists)
            {
                rootDirectory.Delete(true);
            }
        }
    }

    private sealed class EmptyRecordModelManager : IRecordModelManager
    {
        public IReadOnlyList<EnumModel> EnumModels { get; } = [];

        public IReadOnlyList<RecordModel> RecordModels { get; } = [];

        public void Add(ClangSharp.CXXRecordDecl record)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingGeneratorService : IGeneratorService
    {
        public TedToolkit.Occt.Generator.Generators.EnumGenerator GenerateCSharp(EnumModel enumModel)
        {
            throw new NotSupportedException();
        }

        public TedToolkit.Occt.Generator.Generators.CSharpGenerator GenerateCSharp(RecordModel record)
        {
            throw new NotSupportedException();
        }

        public TedToolkit.Occt.Generator.Generators.CppGenerator GenerateCpp(RecordModel record)
        {
            throw new NotSupportedException();
        }
    }
}
