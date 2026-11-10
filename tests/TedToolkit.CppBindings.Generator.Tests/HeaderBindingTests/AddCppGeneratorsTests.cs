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
            extern "C" Point* Identity(Point* point) noexcept;
            extern "C" Point& Borrow(Point& point) noexcept;
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
        };
        var builder = Pipeline.CreateBuilder();
        builder.Options.PrintLogo = false;
        builder.Options.PrintResults = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.DefaultRetryCount = 0;
        builder.Options.ThrowOnPipelineFailure = true;
        var pipeline = await builder.AddCppGenerators(options)
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
        await Assert.That(native).Contains("sizeof(Point) == 8");
        await Assert.That(native).Contains("offsetof(Point, y) == 4");
        await Assert.That(native).Contains("const Point* argument0");
        await Assert.That(native).Contains("Point* result");
        await Assert.That(managed).Contains("NativeApi.GetFunction(1))(left, right)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Add)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Current)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Reset)");
        await Assert.That(table).Contains("(&TedToolkit_FreeFunction_Call_Translate)");
    }
}