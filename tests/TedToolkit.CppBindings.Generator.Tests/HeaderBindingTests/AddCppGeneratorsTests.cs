// -----------------------------------------------------------------------
// <copyright file="AddCppGeneratorsTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator.Tests.HeaderBindingTests;

/// <summary>
/// Verifies header-driven static binding through the shared generation pipeline.
/// </summary>
internal sealed class AddCppGeneratorsTests
{
    private readonly DirectoryPath _root = DirectoryPath.CreateTempSubdirectory("CppBindings.CLinkage.");

    /// <summary>
    /// Removes the headers and output owned by this test.
    /// </summary>
    [After(Test)]
    public void DeleteTemporaryOutput()
    {
        _root.Delete(recursive: true);
    }

    /// <summary>
    /// Verifies C linkage filtering, shared record layouts, wrapper names, and matching table slots.
    /// </summary>
    /// <returns>A task that completes after the paired generated outputs are checked.</returns>
    [Test]
    public async Task Should_publish_static_bindings_and_matching_slots_Async()
    {
        var geometry = _root / new FileName("Geometry.h");
        var utilities = _root / new FileName("Utilities.h");
        var dependency = _root / new FileName("Dependency.h");
        await dependency.WriteAllTextAsync("extern \"C\" void IncludedOnly();").ConfigureAwait(false);
        await geometry.WriteAllTextAsync(
            """
            #include "Dependency.h"
            struct Point { int x; int y; };
            extern "C" int Add(int left, int right) noexcept;
            int Add(int left, int right) noexcept;
            namespace Alias { extern "C" int Add(int left, int right) noexcept; }
            int Ordinary(int left);
            extern "C" Point Translate(Point point, int offset) noexcept;
            extern "C" const Point Copy(Point points[3]) noexcept;
            extern "C" Point* Identity(Point* point) noexcept;
            extern "C" Point& Borrow(Point& point) noexcept;
            struct Packet { int size; };
            extern "C" int Packet(Packet* packet) noexcept;
            struct c { int value; };
            extern "C" const c& Peek(const c& value) noexcept;
            namespace Native::Geometry
            {
                extern "C" {
                    const int& Current(const int& sample);
                    static void LocalOnly();
                    struct Record { static void Member(); };
                }
            }
            """).ConfigureAwait(false);
        await utilities.WriteAllTextAsync(
            """
            extern "C" bool Open(const wchar_t* location, bool enabled);
            extern "C" void Reset(float*& points);
            """).ConfigureAwait(false);
        var options = new GenerationOptions()
        {
            CSharpFolder = _root / "managed",
            CppFolder = _root / "native",
            Headers = [new(geometry), new(utilities, "Control"),],
            NativeInputs = NativeSources.FromHeaders(_root, [], dependencyLibraryDirectories: [_root,]),
            LinkLibraries = ["fixture",],
        };
        var pipeline = await CreateBuilder().AddCppGenerators(options)
            .BuildAsync().ConfigureAwait(false);

        var summary = await pipeline.RunAsync().ConfigureAwait(false);

        await Assert.That(summary.Status).IsEqualTo(Status.Successful);
        var managed = await (options.CSharpFolder / "Headers" / new FileName("Functions.g.cs"))
            .ReadAllTextAsync().ConfigureAwait(false);
        var layout = await (options.CSharpFolder / "Headers" / new FileName("Layouts.g.cs"))
            .ReadAllTextAsync().ConfigureAwait(false);
        var native = await (options.CppFolder / "Headers" / new FileName("Functions.cpp"))
            .ReadAllTextAsync().ConfigureAwait(false);
        var table = await (options.CppFolder / new FileName("NativeFunctionTable.cpp"))
            .ReadAllTextAsync().ConfigureAwait(false);
        var cmake = await (options.CppFolder / new FileName("CMakeLists.txt")).ReadAllTextAsync().ConfigureAwait(false);
        await Assert.That(managed).Contains("class GeometryWrapper");
        await Assert.That(managed).Contains("static int Add(");
        await Assert.That(managed).Contains("int left,");
        await Assert.That(managed).Contains("int right)");
        await Assert.That(managed).Contains("static ref readonly int Current(");
        await Assert.That(managed).Contains("in int sample)");
        await Assert.That(managed).Contains("class Control");
        await Assert.That(managed).Contains("static bool Open(");
        await Assert.That(managed).Contains("ushort* location,");
        await Assert.That(managed).Contains("bool enabled)");
        await Assert.That(managed).Contains("static void Reset(");
        await Assert.That(managed).Contains("ref float* points)");
        await Assert.That(layout).Contains("struct Point");
        await Assert.That(layout).Contains("int x;");
        await Assert.That(layout).Contains("int y;");
        await Assert.That(managed).Contains("static Point Translate(");
        await Assert.That(managed).Contains("static Point Copy(");
        await Assert.That(managed).Contains("Point* points)");
        await Assert.That(managed).Contains("Point point,");
        await Assert.That(managed).Contains("static Point* Identity(");
        await Assert.That(managed).Contains("static ref Point Borrow(");
        await Assert.That(managed).DoesNotContain("struct Point");
        await Assert.That(managed).DoesNotContain("UtilitiesWrapper");
        await Assert.That(managed).DoesNotContain("Ordinary");
        await Assert.That(managed).DoesNotContain("IncludedOnly");
        await Assert.That(managed).DoesNotContain("LocalOnly");
        await Assert.That(managed).DoesNotContain("Member");
        await Assert.That(native).Contains("(&::Native::Geometry::Current)");
        await Assert.That(native).Contains("(&::Add)");
        await Assert.That(native).DoesNotContain("::Alias::Add");
        await Assert.That(native).Contains("sizeof(struct Point) == 8");
        await Assert.That(native).Contains("offsetof(struct Point, y) == 4");
        await Assert.That(native).Contains("const struct Point* argument0");
        await Assert.That(native).Contains("struct Point* result");
        await Assert.That(native).Contains("static_cast<const struct Point (*)(struct Point *)>(&::Copy)");
        await Assert.That(native).Contains("static_cast<int (*)(struct Packet *)>(&::Packet)");
        await Assert.That(native).Contains("static_cast<const struct c& (*)(const struct c&)>(&::Peek)");
        await Assert.That(managed).Contains("NativeApi.GetFunction(1))(left, right)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Add)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Current)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Reset)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Translate)");
        await Assert.That(cmake).Contains("target_link_directories(");
        await Assert.That(cmake).Contains(_root.FullName.Replace('\\', '/'));
        await Assert.That(cmake).Contains("\"fixture\"");
    }

    /// <summary>
    /// Verifies the generated signatures and ABI check against the selected compiler target.
    /// </summary>
    /// <param name="rid">The target runtime identifier.</param>
    /// <param name="integer">The C# projection of C++ long.</param>
    /// <param name="wideChar">The C# projection of wchar_t.</param>
    /// <param name="widths">The generated native width assertion.</param>
    /// <returns>A task that completes after checking the paired output.</returns>
    [Test]
    [Arguments("win-x64", "int", "ushort", "sizeof(long) == 4 && sizeof(wchar_t) == 2")]
    [Arguments("linux-x64", "long", "int", "sizeof(long) == 8 && sizeof(wchar_t) == 4")]
    public async Task Should_project_scalar_widths_for_the_target_Async(string rid, string integer, string wideChar, string widths)
    {
        var header = _root / new FileName("Widths.h");
        await header.WriteAllTextAsync("extern \"C\" long Echo(long value, const wchar_t* code) noexcept;")
            .ConfigureAwait(false);
        var options = new GenerationOptions()
        {
            CSharpFolder = _root / "managed",
            CppFolder = _root / "native",
            RuntimeIdentifier = rid,
            Headers = [new(header),],
        };
        var pipeline = await CreateBuilder().AddCppGenerators(options).BuildAsync().ConfigureAwait(false);

        var summary = await pipeline.RunAsync().ConfigureAwait(false);

        await Assert.That(summary.Status).IsEqualTo(Status.Successful);
        var managed = await (options.CSharpFolder / "Headers" / new FileName("Functions.g.cs"))
            .ReadAllTextAsync().ConfigureAwait(false);
        var native = await (options.CppFolder / "Headers" / new FileName("Functions.cpp"))
            .ReadAllTextAsync().ConfigureAwait(false);
        await Assert.That(managed).Contains("static " + integer + " Echo(");
        await Assert.That(managed).Contains(wideChar + "* code)");
        await Assert.That(native).Contains(widths);
    }

    /// <summary>
    /// Verifies that a selected or transitively included header survives a failed output overlap check.
    /// </summary>
    /// <param name="transitive">Whether the endangered file is included by a selected header.</param>
    /// <returns>A task that completes after confirming the input is intact.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_preserve_input_headers_inside_output_Async(bool transitive)
    {
        var native = _root / "native";
        native.Create();
        var input = native / new FileName("Input.h");
        await input.WriteAllTextAsync("extern \"C\" int Sample() noexcept;").ConfigureAwait(false);
        var publicHeader = _root / new FileName("Public.h");
        await publicHeader.WriteAllTextAsync("#include \"native/Input.h\"").ConfigureAwait(false);
        var options = new GenerationOptions()
        {
            CSharpFolder = _root / "managed",
            CppFolder = native,
            Headers = [new(transitive ? publicHeader : input),],
        };
        var pipeline = await CreateBuilder().AddCppGenerators(options).BuildAsync().ConfigureAwait(false);

        Func<Task> run = () => pipeline.RunAsync();
        await Assert.That(run).Throws<Exception>()
            .WithMessageContaining("inside a generation output directory", StringComparison.Ordinal);
        await Assert.That(input.Exists).IsTrue();
    }

    /// <summary>
    /// Verifies that the default entry rejects exact version selection without a version resolver.
    /// </summary>
    /// <returns>A task that completes after checking the registration error.</returns>
    [Test]
    public async Task Should_reject_an_unverified_native_version_Async()
    {
        var options = new GenerationOptions()
        {
            CSharpFolder = _root / "managed",
            CppFolder = _root / "native",
            NativeLibraryVersion = new(1, 2),
        };

        await Assert.That(() => CreateBuilder().AddCppGenerators(options)).Throws<NotSupportedException>();
    }

    /// <summary>
    /// Verifies that a provider which ignores the configured inputs cannot publish an incomplete plan.
    /// </summary>
    /// <returns>A task that completes after checking the pipeline diagnostic.</returns>
    [Test]
    public async Task Should_reject_a_provider_plan_that_ignored_headers_Async()
    {
        var header = _root / new FileName("Input.h");
        await header.WriteAllTextAsync("extern \"C\" int Sample() noexcept;").ConfigureAwait(false);
        var options = new GenerationOptions()
        {
            CSharpFolder = _root / "managed",
            CppFolder = _root / "native",
            Headers = [new(header),],
        };
        var pipeline = await CreateBuilder().AddCppGenerators(options, new OptionsIgnoringProvider())
            .BuildAsync().ConfigureAwait(false);

        Func<Task> run = () => pipeline.RunAsync();
        await Assert.That(run).Throws<Exception>()
            .WithMessageContaining("did not process the requested public input headers", StringComparison.Ordinal);
        await Assert.That(options.CppFolder.Exists).IsFalse();
    }

    private static PipelineBuilder CreateBuilder()
    {
        var builder = Pipeline.CreateBuilder();
        builder.Options.PrintLogo = false;
        builder.Options.PrintResults = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.DefaultRetryCount = 0;
        builder.Options.ThrowOnPipelineFailure = true;
        return builder;
    }

    private sealed class OptionsIgnoringProvider : IGenerationProvider
    {
        public IReadOnlyList<Type> PreparationModules { get; } = [];

        public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new GenerationPlan([], [], []));
        }

        public Task<GenerationPlan> CreatePlanAsync(in GenerationOptions options, in CancellationToken cancellationToken)
        {
            return CreatePlanAsync(cancellationToken);
        }
    }
}