// -----------------------------------------------------------------------
// <copyright file="GenerateCMakeAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Tests.Services.RecordLayoutServiceTests;

/// <summary>
/// Tests for the CMake generation path in <see cref="RecordLayoutService"/>.
/// </summary>
internal sealed class GenerateCMakeAsyncTest
{
    /// <summary>
    /// Verifies the generated CMake text embeds the probe output directory settings directly.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_inline_the_probe_output_directory_settings_Async()
    {
        var service = new RecordLayoutService(
            Microsoft.Extensions.Options.Options.Create(
                new GenerationOptions()
                {
                    DeclOptions = [],
                    CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                    CppFolder = new DirectoryInfo(Path.GetTempPath()),
                }),
            new TypeServiceStub(),
            new RecordServiceStub(),
            new VcpkgServiceStub());

        var generateCMakeAsyncMethod = typeof(RecordLayoutService).GetMethod(
            "GenerateCMakeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(generateCMakeAsyncMethod).IsNotNull();

        var task = (Task<string>)generateCMakeAsyncMethod!
            .Invoke(service, null)!;
        var cmake = await task.ConfigureAwait(false);

        await Assert.That(cmake).Contains("add_executable(occt_layout_probe layout_probe.cpp)");
        await Assert.That(cmake).Contains("set_target_properties(occt_layout_probe PROPERTIES");
        await Assert.That(cmake).Contains("RUNTIME_OUTPUT_DIRECTORY \"${CMAKE_BINARY_DIR}\"");
        await Assert.That(cmake).Contains("RUNTIME_OUTPUT_DIRECTORY_RELEASE \"${CMAKE_BINARY_DIR}\"");
    }

    private sealed class TypeServiceStub : ITypeService
    {
        public string GetCppName(ClangSharp.Type type)
        {
            throw new NotSupportedException();
        }

        public string GetCSharpName(ClangSharp.Type type)
        {
            throw new NotSupportedException();
        }

        public ClangSharp.Type DesugarType(ClangSharp.Type type)
        {
            throw new NotSupportedException();
        }

        public bool ShouldParse(ClangSharp.Type type)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordServiceStub : IRecordService
    {
        public string GetName(ClangSharp.CXXRecordDecl decl)
        {
            throw new NotSupportedException();
        }

        public ClangSharp.Type GetType(ClangSharp.CXXRecordDecl decl)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<ClangSharp.FieldDecl> GetFields(ClangSharp.CXXRecordDecl record)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<ClangSharp.CXXMethodDecl> GetMethods(ClangSharp.CXXRecordDecl record)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class VcpkgServiceStub : IVcpkgService
    {
        public string GetRoot()
        {
            throw new NotSupportedException();
        }

        public string GetIncludeFolder()
        {
            throw new NotSupportedException();
        }

        public string GetOcctIncludeFolder()
        {
            throw new NotSupportedException();
        }

        public string GetTriplet()
        {
            throw new NotSupportedException();
        }

        public Task<int> GetOcctCppVersionAsync()
        {
            return Task.FromResult(23);
        }
    }
}
