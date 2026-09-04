// -----------------------------------------------------------------------
// <copyright file="AddTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;
using TedToolkit.CppBindings.Occt.Generator.Services;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;
using TedToolkit.CppBindings.Occt.Generator.Services.Rules;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Services.RecordModelManagerTests;

/// <summary>
/// Verifies <see cref="RecordModelManager.Add(ClangSharp.CXXRecordDecl)"/>.
/// </summary>
internal sealed class AddTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies parsed unions retain exact shared storage and typed interior references.
    /// </summary>
    /// <param name="wide">Whether the union has differently sized members.</param>
    /// <param name="readOnly">Whether the first member is const.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Should_preserve_overlapping_union_fields_Async(bool wide, bool readOnly)
    {
        var nativeType = wide ? "long long" : "int";
        var managedType = wide ? "long" : "int";
        var qualifier = readOnly ? "const " : "";
        var nativeDeclaration = $$"""
            union Storage { {{qualifier}}{{nativeType}} First; {{(wide ? "char" : "float")}} Last; };
            struct Container { char Prefix; Storage Value; int Suffix; };
            static_assert(__builtin_offsetof(Storage, First) == 0);
            static_assert(__builtin_offsetof(Storage, Last) == 0);
            """;
        using var translationUnit = ParseTranslationUnit(nativeDeclaration);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static declaration => declaration.Name == "Container"));
        var records = manager.RecordModels.ToArray();
        var storage = records.Single(static record => record.Type.CppTypeName == "Storage");
        var container = records.Single(static record => record.Type.CppTypeName == "Container");
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe", DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var sources = new List<string>();
        foreach (var record in records)
        {
            sources.Add(await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
                .ConfigureAwait(false));
        }

        var nativeBytes = await GetNativeBitFieldBytesAsync("", "value.Last = 42;", "value.Last == 42",
            storage, nativeDeclaration).ConfigureAwait(false);
        var writeFirst = readOnly ? "" : "value.First = 123; if (Read(in value.First) != 123) return false;";
        var suffixOffset = container.FieldModels.Single(static field => field.Name == "Suffix").Offset;
        var probe = $$"""
            namespace LayoutProbe
            {
                public sealed class Heap { public Container Value; }
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        Storage value = default;
                        Container container = default;
                        if (sizeof(Storage) != {{storage.Size}} || sizeof(Container) != {{container.Size}}
                            || (byte*)&container.Value - (byte*)&container != {{storage.Alignment}}
                            || (byte*)&container.Suffix - (byte*)&container != {{suffixOffset}})
                            return false;
                        {{writeFirst}}
                        new System.Span<byte>(&value, sizeof(Storage)).Fill(0xa5);
                        value.Last = 42;
                        if (System.Convert.ToHexString(new System.ReadOnlySpan<byte>(&value, sizeof(Storage))) != "{{nativeBytes}}")
                            return false;
                        ref readonly var first = ref value.First;
                        if (System.Runtime.CompilerServices.Unsafe.ByteOffset(
                            ref System.Runtime.CompilerServices.Unsafe.As<Storage, byte>(ref value),
                            ref System.Runtime.CompilerServices.Unsafe.As<{{managedType}}, byte>(
                                ref System.Runtime.CompilerServices.Unsafe.AsRef(in first))) != 0) return false;
                        var property = typeof(Storage).GetProperty("First")!;
                        if (property.PropertyType != typeof({{managedType}}).MakeByRefType()
                            || !System.Attribute.IsDefined(property, typeof(TedToolkit.CppBindings.NativeTypeNameAttribute))
                            || System.Array.Exists(property.GetMethod!.ReturnParameter.GetRequiredCustomModifiers(),
                                type => type.FullName == "System.Runtime.InteropServices.InAttribute") != {{(readOnly ? "true" : "false")}})
                            return false;
                        var moved = false;
                        for (var attempt = 0; attempt < 8 && !moved; attempt++)
                        {
                            var heap = new Heap();
                            heap.Value.Prefix = 11;
                            heap.Value.Suffix = 29;
                            ref readonly var view = ref heap.Value.Value.First;
                            var before = Address(in view);
                            System.GC.Collect(2, System.GCCollectionMode.Forced, true, true);
                            System.GC.WaitForPendingFinalizers();
                            moved = before != Address(in view);
                            if (!System.Runtime.CompilerServices.Unsafe.AreSame(
                                ref System.Runtime.CompilerServices.Unsafe.AsRef(in view),
                                ref System.Runtime.CompilerServices.Unsafe.AsRef(in heap.Value.Value.First))) return false;
                            heap.Value.Value.Last = 42;
                            if (heap.Value.Prefix != 11 || heap.Value.Suffix != 29
                                || Read(in view) != Read(in heap.Value.Value.First)) return false;
                            System.GC.KeepAlive(heap);
                        }
                        return moved && typeof(Storage).GetField("First") is null
                            && typeof(Storage).GetProperty("Last") is not null
                            && typeof(Container).GetField("Value") is not null
                            && typeof(Container).GetField("Prefix") is not null
                            && typeof(Container).GetField("Suffix") is not null;
                    }
                    private static {{managedType}} Read(in {{managedType}} value) => value;
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                    private static nint Address(in {{managedType}} value)
                    {
                        fixed ({{managedType}}* pointer = &value) return (nint)pointer;
                    }
                }
            }
            """;
        await AssertNet8StorageAsync(string.Join("\n", sources), probe).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies overlapping pointer slots preserve pointer-level rather than pointee-level constness.
    /// </summary>
    /// <param name="constantPointer">Whether the pointer slot itself is const.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_preserve_overlapping_pointer_fields_Async(bool constantPointer)
    {
        var pointerType = constantPointer ? "int* const" : "const int*";
        using var translationUnit = ParseTranslationUnit($"union Storage {{ {pointerType} Pointer; long long Number; }};");
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>().Single());
        var record = manager.RecordModels.Single();
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe", DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var source = await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None).ConfigureAwait(false);
        var write = constantPointer ? "" : "int target = 79; value.Pointer = &target; if (*value.Pointer != 79) return false;";
        var probe = $$"""
            namespace LayoutProbe
            {
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        Storage value = default;
                        {{write}}
                        value.Number = 0;
                        ref readonly var slot = ref value.Pointer;
                        if (slot != null || sizeof(Storage) != {{record.Size}}) return false;
                        var property = typeof(Storage).GetProperty("Pointer")!;
                        return property.PropertyType == typeof(int*).MakeByRefType()
                            && System.Array.Exists(property.GetMethod!.ReturnParameter.GetRequiredCustomModifiers(),
                                type => type.FullName == "System.Runtime.InteropServices.InAttribute")
                                == {{(constantPointer ? "true" : "false")}};
                    }
                }
            }
            """;
        await AssertNet8StorageAsync(source, probe).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies overlapping bitfields and closed template unions retain their native bytes.
    /// </summary>
    /// <param name="template">Whether the fixture contains differently sized template specializations.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_preserve_composed_union_storage_Async(bool template)
    {
        var nativeDeclaration = template
            ? "template<class T> union Choice { T First; int Second; }; struct Storage { Choice<int> Small; Choice<double> Wide; };"
            : "union Storage { unsigned int Bits : 3; unsigned int Whole; };";
        var writes = template ? "value.Small.First = 19; value.Wide.First = 3.5;" : "value.Bits = 3;";
        var condition = template ? "value.Small.First == 19 && value.Wide.First == 3.5" : "value.Bits == 3";
        using var translationUnit = ParseTranslationUnit(nativeDeclaration);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static declaration => declaration.Name == "Storage"));
        var records = manager.RecordModels.ToArray();
        var storage = records.Single(static record => record.Type.CppTypeName == "Storage");
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe", DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var sources = new List<string>();
        foreach (var record in records)
        {
            sources.Add(await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
                .ConfigureAwait(false));
        }

        var nativeBytes = await GetNativeBitFieldBytesAsync("", writes, condition, storage, nativeDeclaration)
            .ConfigureAwait(false);
        var shapeCheck = template
            ? "typeof(Storage).GetField(\"Small\")!.FieldType.GetProperty(\"First\") is not null"
            : "!typeof(Storage).GetProperty(\"Bits\")!.PropertyType.IsByRef && typeof(Storage).GetField(\"Whole\") is null";
        var probe = $$"""
            namespace LayoutProbe
            {
                [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
                public struct Holder { public byte Prefix; public Storage Value; }
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        Storage value = default;
                        Holder holder = default;
                        new System.Span<byte>(&value, sizeof(Storage)).Fill(0xa5);
                        {{writes}}
                        return ({{condition}}) && ({{shapeCheck}}) && sizeof(Storage) == {{storage.Size}}
                            && (byte*)&holder.Value - (byte*)&holder == {{storage.Alignment}}
                            && System.Convert.ToHexString(new System.ReadOnlySpan<byte>(&value, sizeof(Storage))) == "{{nativeBytes}}";
                    }
                }
            }
            """;
        await AssertNet8StorageAsync(string.Join("\n", sources), probe).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies generated cyclic handles remain typed aliases without changing loadable fields.
    /// </summary>
    /// <param name="byValueTail">Whether the return edge contains the list by value.</param>
    /// <param name="readOnly">Whether the handle storage is const.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Should_load_cyclic_handle_storage_Async(bool byValueTail, bool readOnly)
    {
        var tail = byValueTail ? "Storage" : "opencascade::handle<Storage>";
        var qualifier = readOnly ? "const " : "";
        var nativeDeclaration = $$"""
            namespace opencascade { template<class T> struct handle { T* Pointer; }; }
            struct Node;
            struct Leaf { int Value; };
            struct Self { opencascade::handle<Self> Next; };
            struct Storage {
                char Prefix;
                {{qualifier}}opencascade::handle<Node> Head;
                opencascade::handle<Leaf> Other;
                Self SelfReference;
                int Value;
            };
            struct Node { {{tail}} Tail; int Value; };
            """;
        using var translationUnit = ParseTranslationUnit(nativeDeclaration);
        var manager = CreateManager(new HandleTypeRule());
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Storage"));
        var records = manager.RecordModels.ToArray();
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe", DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var sources = new List<string>();
        foreach (var record in records)
        {
            sources.Add(await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
                .ConfigureAwait(false));
        }

        var storage = records.Single(static record => record.Type.CppTypeName == "Storage");
        var headOffset = storage.FieldModels.Single(static field => field.Name == "Head").Offset;
        var write = readOnly ? "" : $"value.Head = default; if (*(nint*)((byte*)&value + {headOffset}) != 0) return false;";
        var nativeWrite = readOnly ? "value.Value = 17;" : "value.Head = {}; value.Value = 17;";
        var readOnlyLiteral = readOnly ? "true" : "false";
        var nativeBytes = await GetNativeBitFieldBytesAsync("", nativeWrite, "value.Value == 17", storage, nativeDeclaration)
            .ConfigureAwait(false);
        var probe = $$"""
            namespace LayoutProbe
            {
                public sealed class Heap { public byte Prefix; public Storage Value; }
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        _ = typeof(Storage).Assembly.GetTypes();
                        Storage value = default;
                        Node node = default;
                        *(Node**)((byte*)&value + {{headOffset}}) = &node;
                        ref readonly var head = ref value.Head;
                        head.Value.Value = 79;
                        if (node.Value != 79 || sizeof(Storage) != {{storage.Size}}) return false;
                        if (System.Runtime.CompilerServices.Unsafe.ByteOffset(
                            ref System.Runtime.CompilerServices.Unsafe.As<Storage, byte>(ref value),
                            ref System.Runtime.CompilerServices.Unsafe.As<TedToolkit.CppBindings.Occt.handle<Node>, byte>(
                                ref System.Runtime.CompilerServices.Unsafe.AsRef(in head))) != {{headOffset}}) return false;
                        new System.Span<byte>(&value, sizeof(Storage)).Fill(0xa5);
                        {{write}}
                        value.Value = 17;
                        if (System.Convert.ToHexString(new System.ReadOnlySpan<byte>(&value, sizeof(Storage)))
                            != "{{nativeBytes}}") return false;
                        var getter = typeof(Storage).GetProperty("Head")!.GetMethod!;
                        if (System.Array.Exists(getter.ReturnParameter.GetRequiredCustomModifiers(),
                            type => type.FullName == "System.Runtime.InteropServices.InAttribute")
                            != {{readOnlyLiteral}}) return false;
                        var heap = new Heap();
                        ref readonly var heapHead = ref heap.Value.Head;
                        System.GC.Collect(2, System.GCCollectionMode.Forced, true, true);
                        System.GC.WaitForPendingFinalizers();
                        if (!System.Runtime.CompilerServices.Unsafe.AreSame(
                            ref System.Runtime.CompilerServices.Unsafe.AsRef(in heapHead),
                            ref System.Runtime.CompilerServices.Unsafe.AsRef(in heap.Value.Head))) return false;
                        return typeof(Storage).GetProperty("Head") is not null
                            && typeof(Storage).GetField("Other") is not null
                            && typeof(Self).GetField("Next") is not null;
                    }
                }
            }
            """;
        await Generators.CSharpGeneratorTests.LayoutTests.AssertCompiledStorageAsync(string.Join("\n", sources), probe)
            .ConfigureAwait(false);
        await AssertNet8StorageAsync(string.Join("\n", sources), probe).ConfigureAwait(false);
    }

    private static async Task AssertNet8StorageAsync(string source, string probe)
    {
        var directory = Directory.CreateTempSubdirectory("CppBindings.Cycles.Net8.");
        try
        {
            var runtime = System.Security.SecurityElement.Escape(typeof(NativeTypeNameAttribute).Assembly.Location);
            var occtRuntime = System.Security.SecurityElement.Escape(typeof(IStandard_Transient).Assembly.Location);
            var project = $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
                    <AllowUnsafeBlocks>true</AllowUnsafeBlocks><NuGetAudit>false</NuGetAudit>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include="TedToolkit.CppBindings.Runtime"><HintPath>{{runtime}}</HintPath></Reference>
                    <Reference Include="TedToolkit.CppBindings.Occt.Runtime"><HintPath>{{occtRuntime}}</HintPath></Reference>
                  </ItemGroup>
                </Project>
                """;
            var projectPath = Path.Combine(directory.FullName, "Probe.csproj");
            await File.WriteAllTextAsync(projectPath, project).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Storage.cs"), source).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Probe.cs"), probe).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Program.cs"), """
                if (!LayoutProbe.Probe.Check()) throw new System.InvalidOperationException("Cycle probe failed.");
                System.Console.WriteLine("net8 cycle probe passed");
                """).ConfigureAwait(false);
            var output = await RunBitFieldProbeAsync("dotnet",
                ["run", "--project", projectPath, "-c", "Release", "--disable-build-servers",], 90).ConfigureAwait(false);
            await Assert.That(output).Contains("net8 cycle probe passed");
        }
        finally
        {
            await DeleteProbeDirectoryAsync(directory).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Verifies alignment admission preserves unrelated members and independently representable nested types.
    /// </summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Should_exclude_only_unrepresentable_alignment_dependencies_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct alignas(16) Aligned {
                int Data;
                Aligned();
                ~Aligned();
                struct Nested { int Value; };
            };
            struct Api {
                Aligned* Pointer;
                int Value;
                Aligned::Nested Nested;
                const Aligned& Borrow();
                Aligned Copy(Aligned value);
                void Use(Aligned* value);
                int Keep() const;
            };
            """, "__occt__/test.cpp");
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Api"));
        var records = manager.RecordModels.ToArray();
        await Assert.That(records.Any(static record => record.Type.CppTypeName == "Aligned")).IsFalse();
        await Assert.That(records.Any(static record => record.Type.CppTypeName == "Aligned::Nested")).IsTrue();
        var api = records.Single(static record => record.Type.CppTypeName == "Api");
        await Assert.That(api.FieldModels.Select(static field => field.Name)).IsEquivalentTo(["Value", "Nested",]);
        await Assert.That(api.MethodModels.Select(static method => method.MethodName)).IsEquivalentTo(["Keep",]);
        var exports = NativeExportInventory.GetExports(records);
        await Assert.That(exports).Contains("Api_Keep");
        await Assert.That(exports).DoesNotContain("Api_Borrow");
        await Assert.That(exports).DoesNotContain("Api_Copy");
        await Assert.That(exports).DoesNotContain("Api_Use");
        await Assert.That(exports).DoesNotContain("Aligned_Create");
        await Assert.That(manager.UnsupportedDeclarations.Count).IsEqualTo(5);
        var diagnostics = string.Join("\n", manager.UnsupportedDeclarations);
        await Assert.That(diagnostics).Contains("record Aligned: native alignment 16");
        await Assert.That(diagnostics).Contains("field Pointer in Api: required type Aligned");
        await Assert.That(diagnostics).Contains("operation Borrow (Api_Borrow) in Api");
        await Assert.That(manager.RecordModels.ToArray()).IsEquivalentTo(records);
        await Assert.That(string.Join("\n", manager.UnsupportedDeclarations)).IsEqualTo(diagnostics);

        api.ObjectKind = NativeObjectKind.Value;
        var native = await new CppGenerator(api).GenerateAsync(CancellationToken.None).ConfigureAwait(false);
        await Assert.That(native).Contains("Api_Keep(");
        await Assert.That(native).DoesNotContain("Api_Borrow(");
        await Assert.That(native).DoesNotContain("Api_Copy(");
        await Assert.That(native).DoesNotContain("Api_Use(");
    }

    /// <summary>
    /// Verifies an unused unrepresentable template argument retains a closed layout, not a dangling generic type.
    /// </summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Should_keep_representable_closed_templates_after_alignment_admission_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct alignas(16) Aligned { int Data; };
            template<class T> struct Box { int Value; };
            struct Api { Box<Aligned> First; Box<int> Last; };
            """);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Api"));
        var records = manager.RecordModels.ToArray();
        var closed = records.Single(static record => record.Type.CppTypeName == "Box<Aligned>");
        await Assert.That(closed.TemplateProjection).IsNull();
        await Assert.That(closed.Type.CSharpTypeName).IsEqualTo("Box_Aligned");
        var api = records.Single(static record => record.Type.CppTypeName == "Api");
        await Assert.That(api.FieldModels.Select(static field => field.Type.CSharpTypeName))
            .IsEquivalentTo(["Box_Aligned", "Box<int>",]);
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe", DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var sources = new List<string>();
        foreach (var record in records)
        {
            sources.Add(await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
                .ConfigureAwait(false));
        }

        const string Probe = """
            namespace LayoutProbe
            {
                public struct Holder { public byte Prefix; public Api Value; }
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        Holder holder = default;
                        holder.Value.First.Value = 17;
                        holder.Value.Last.Value = 29;
                        return sizeof(Api) == 8 && (byte*)&holder.Value - (byte*)&holder == 4
                            && holder.Value.First.Value == 17 && holder.Value.Last.Value == 29;
                    }
                }
            }
            """;
        await Generators.CSharpGeneratorTests.LayoutTests.AssertCompiledStorageAsync(
            string.Join("\n", sources), Probe, "LayoutProbe.Api").ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies public inheritance dependencies propagate independently of native-only header dependencies.
    /// </summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Should_close_rejected_base_dependencies_without_header_filtering_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Base { int Value; };
            struct Derived : Base { int Extra; };
            struct Leaf : Derived { int Tail; };
            struct Api { Leaf* Pointer; int Keep; };
            """, "__occt__/test.cpp");
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Api"));
        var native = manager.NativePreparationRecords.ToArray();

        // Isolate the dependency rule from the earlier native alignment rule.
        native.Single(static record => record.Type.CppTypeName == "Base").Alignment = 16;
        var records = manager.RecordModels.ToArray();
        await Assert.That(records.Select(static record => record.Type.CppTypeName)).IsEquivalentTo(["Api",]);
        await Assert.That(records.Single().FieldModels.Select(static field => field.Name)).IsEquivalentTo(["Keep",]);
        var diagnostics = string.Join("\n", manager.UnsupportedDeclarations);
        await Assert.That(diagnostics).Contains("record Derived: required public base Base");
        await Assert.That(diagnostics).Contains("record Leaf: required public base Derived");
        await Assert.That(diagnostics).Contains("field Pointer in Api: required type Leaf");
    }

    /// <summary>
    /// Verifies one generic representation preserves every admitted specialization's native packing.
    /// </summary>
    /// <param name="pack">Native packing limit.</param>
    /// <param name="byteRepresentative">Whether to render the byte-aligned specialization.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    [Test]
    [Arguments(1, true)]
    [Arguments(1, false)]
    [Arguments(2, true)]
    [Arguments(8, true)]
    public async Task Should_preserve_generic_family_alignment_Async(int pack, bool byteRepresentative)
    {
        using var translationUnit = ParseTranslationUnit($$"""
            #pragma pack(push, {{pack}})
            template<typename T> struct Storage { T First; T Last; };
            #pragma pack(pop)
            struct Api { Storage<char> Small; Storage<double> Large; };
            """);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Api"));
        var records = manager.RecordModels.Where(static record => record.TemplateProjection?.FamilyName == "Storage")
            .ToArray();
        await Assert.That(records.Length).IsEqualTo(2);
        var record = records.Single(candidate => candidate.Type.CppTypeName.Contains("char", StringComparison.Ordinal)
            == byteRepresentative);
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe",
            DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
        });
        var source = await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var probe = $$"""
            namespace LayoutProbe
            {
                public struct SmallHolder { public byte Prefix; public Storage<sbyte> Value; }
                public struct LargeHolder { public byte Prefix; public Storage<double> Value; }
                public static unsafe class Probe
                {
                    public static bool Check()
                    {
                        SmallHolder small = default;
                        LargeHolder large = default;
                        return sizeof(Storage<sbyte>) == 2 && sizeof(Storage<double>) == 16
                            && (byte*)&small.Value - (byte*)&small == 1
                            && (byte*)&large.Value - (byte*)&large == {{pack}};
                    }
                }
            }
            """;
        await Generators.CSharpGeneratorTests.LayoutTests.AssertCompiledStorageAsync(source, probe, "LayoutProbe.Storage`1")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies actual C++ bitfields retain their widths and do not overwrite adjacent bits.
    /// </summary>
    /// <param name="fields">Native field declarations.</param>
    /// <param name="checks">Managed writes and observable checks.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("unsigned Flag : 1;", "value.Flag = 3; return value.Flag == 1;")]
    [Arguments("unsigned First : 1; unsigned Last : 3;",
        "value.First = 1; value.Last = 15; return value.First == 1 && value.Last == 7;")]
    [Arguments("signed First : 3; unsigned Last : 5;",
        "value.First = -3; value.Last = 21; return value.First == -3 && value.Last == 21;")]
    [Arguments("bool First : 1; bool Last : 1;",
        "value.First = true; value.Last = true; value.First = false; return !value.First && value.Last;")]
    [Arguments("unsigned First : 31; unsigned Last : 2;",
        "value.First = 0xffffffffU; value.Last = 3; return value.First == 2147483647 && value.Last == 3;")]
    [Arguments("unsigned : 2; unsigned Flag : 1; unsigned : 0; unsigned Last : 3;",
        "value.Flag = 3; value.Last = 9; return value.Flag == 1 && value.Last == 1;")]
    [Arguments("unsigned long long Wide : 64;",
        "value.Wide = 0xffffffffffffffffUL; return value.Wide == 0xffffffffffffffffUL;")]
    [Arguments("signed First : 3; unsigned Last : 5;",
        "value.First = 7; value.Last = 17; return value.First == -1 && value.Last == 17;")]
    [Arguments("char Prefix; unsigned First : 1; unsigned Last : 3; char Tail;",
        "value.First = 1; value.Last = 12; return value.First == 1 && value.Last == 4;")]
    [Arguments("const unsigned Flag : 1;", "return value.Flag == 0;")]
    public async Task Should_preserve_native_bitfields_Async(string fields, string checks)
    {
        await AssertNativeBitFieldsAsync(fields, checks, checks).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies native long bitfields retain their platform-sized managed projection.
    /// </summary>
    /// <param name="fields">Native field declarations.</param>
    /// <param name="managedChecks">Managed writes and observable checks.</param>
    /// <param name="nativeChecks">Equivalent native writes and observable checks.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("long First : 3; unsigned long Last : 5;",
        "value.First = new System.Runtime.InteropServices.CLong(7); value.Last = new System.Runtime.InteropServices.CULong(49U); return value.First.Value == -1 && value.Last.Value == 17;",
        "value.First = 7; value.Last = 49; return value.First == -1 && value.Last == 17;")]
    [Arguments("long First : 32; unsigned long Last : 32;",
        "value.First = new System.Runtime.InteropServices.CLong(-2147483648); value.Last = new System.Runtime.InteropServices.CULong(0xffffffffU); return value.First.Value == -2147483648 && value.Last.Value == 0xffffffffU;",
        "value.First = -2147483648; value.Last = 0xffffffffU; return value.First == -2147483648 && value.Last == 0xffffffffU;")]
    public async Task Should_preserve_native_long_bitfields_Async(string fields, string managedChecks, string nativeChecks)
    {
        await AssertNativeBitFieldsAsync(fields, managedChecks, nativeChecks).ConfigureAwait(false);
    }

    private static async Task AssertNativeBitFieldsAsync(string fields, string checks, string nativeChecks)
    {
        using var translationUnit = ParseTranslationUnit("struct Storage { " + fields + " };");
        var declaration = translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>().Single();
        var manager = CreateManager();
        manager.Add(declaration);
        var record = manager.RecordModels.Single();
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            CSharpNamespace = "LayoutProbe",
            DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
        });
        var source = await new CSharpGenerator(record, options).GenerateAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var returnIndex = checks.LastIndexOf("return ", StringComparison.Ordinal);
        var writes = checks[..returnIndex];
        var condition = checks[(returnIndex + 7)..].TrimEnd(';');
        var nativeReturnIndex = nativeChecks.LastIndexOf("return ", StringComparison.Ordinal);
        var nativeBytes = await GetNativeBitFieldBytesAsync(fields, nativeChecks[..nativeReturnIndex],
            nativeChecks[(nativeReturnIndex + 7)..].TrimEnd(';'), record).ConfigureAwait(false);
        var readOnly = record.FieldModels.Any(static field => field.IsReadOnlyBitField);
        var initialize = readOnly ? "" : "new System.Span<byte>(&value, sizeof(Storage)).Fill(0xa5);";
        var storageCheck = readOnly
            ? "!typeof(Storage).GetProperty(\"Flag\")!.CanWrite"
            : "System.Convert.ToHexString(new System.ReadOnlySpan<byte>(&value, sizeof(Storage)))"
              + $" == \"{nativeBytes}\"";
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
                        if (sizeof(Storage) != {{record.Size}}
                            || (byte*)&holder.Value - (byte*)&holder != {{record.Alignment}}) return false;
                        Storage value = default;
                        {{initialize}}
                        {{writes}}
                        return ({{condition}}) && {{storageCheck}};
                    }
                }
            }
            """;
        await Generators.CSharpGeneratorTests.LayoutTests.AssertCompiledStorageAsync(source, probe)
            .ConfigureAwait(false);
    }

    private static async Task<string> GetNativeBitFieldBytesAsync(
        string fields, string writes, string condition, RecordModel record, string? nativeDeclaration = null)
    {
        var directory = Directory.CreateTempSubdirectory("CppBindings.Bitfields.");
        try
        {
            var sourcePath = Path.Combine(directory.FullName, "probe.cpp");
            var executablePath = Path.Combine(directory.FullName, "probe.exe");
            var initialize = record.FieldModels.Any(static field => field.IsReadOnlyBitField)
                ? ""
                : "std::memset(&value, 0xa5, sizeof(value));";
            var source = $$"""
                #include <cstdio>
                #include <cstring>
                {{nativeDeclaration ?? ("struct Storage { " + fields + " };")}}
                struct Holder { char Prefix; Storage Value; };
                static_assert(sizeof(Storage) == {{record.Size}});
                static_assert(alignof(Storage) == {{record.Alignment}});
                static_assert(__builtin_offsetof(Holder, Value) == {{record.Alignment}});
                int main()
                {
                    Storage value{};
                    {{initialize}}
                    {{writes}}
                    if (!({{condition}})) return 1;
                    for (unsigned i = 0; i < sizeof(value); ++i)
                        std::printf("%02X", reinterpret_cast<unsigned char*>(&value)[i]);
                }
                """;
            await File.WriteAllTextAsync(sourcePath, source).ConfigureAwait(false);
            _ = await RunBitFieldProbeAsync("clang++",
                ["-std=c++20", "-Wno-bitfield-constant-conversion", sourcePath, "-o", executablePath,])
                .ConfigureAwait(false);
            return await RunBitFieldProbeAsync(executablePath, []).ConfigureAwait(false);
        }
        finally
        {
            await DeleteProbeDirectoryAsync(directory).ConfigureAwait(false);
        }
    }

    private static async Task DeleteProbeDirectoryAsync(DirectoryInfo directory)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                directory.Delete(recursive: true);
                return;
            }
            catch (Exception error) when (attempt < 4 && error is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> RunBitFieldProbeAsync(string executable, string[] arguments, int timeoutSeconds = 30)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Native probe did not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            await Assert.That(process.ExitCode).IsEqualTo(0).Because(error + output);
            return output;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Verifies reference members describe stored addresses rather than their referents.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_measure_reference_member_storage_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Payload { long long Values[4]; };
            using PayloadReference = const Payload&;
            struct Holder
            {
                PayloadReference Large;
                char& Small;
                Payload&& Movable;
                int Tail;
            };
            """);
        var declaration = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>().Single(static record => record.Name == "Holder");
        var manager = CreateManager();
        manager.Add(declaration);
        var holder = manager.RecordModels.Single(static record => record.Type.CppTypeName == "Holder");

        foreach (var field in holder.FieldModels.Where(static field => field.Name != "Tail"))
        {
            await Assert.That(field.Size).IsEqualTo(8);
            await Assert.That(field.Alignment).IsEqualTo(8);
        }

        await Assert.That(holder.FieldModels.Single(static field => field.Name == "Tail").Offset).IsEqualTo(24);
    }

    /// <summary>
    /// Verifies a used standard pair specialization receives a closed, compiler-probed model.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_include_used_standard_pair_specialization_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace std
            {
                template<typename TFirst, typename TSecond>
                struct pair
                {
                    TFirst first;
                    TSecond second;
                };
            }
            template struct std::pair<double, double>;

            struct Range
            {
                std::pair<double, double> Bounds() const;
            };
            """, "__occt__/test.cpp");
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Range");
        var manager = CreateManager();

        manager.Add(record);

        var pair = manager.RecordModels.Single(static model =>
            model.Type.CppTypeName == "std::pair<double, double>");
        await Assert.That(pair.FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["first", "second",]);
    }

    /// <summary>
    /// Verifies public enum-valued nested template specializations remain available.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_include_public_nested_template_with_qualified_enum_argument_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct NodeId
            {
                enum class Kind { Face };
                template<Kind Value> struct Typed { int Id; };
                Typed<Kind::Face> Face() const;
            };
            template struct NodeId::Typed<NodeId::Kind::Face>;
            """);
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "NodeId");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Select(static model => model.Type.CppTypeName))
            .Contains("NodeId::Typed<NodeId::Kind::Face>");
    }

    /// <summary>
    /// Verifies a specialization carrying a private forward-declared nested type is not public.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <exception cref="InvalidOperationException">The expected specialization cannot be found.</exception>
    [Test]
    public async Task Should_exclude_template_with_private_forward_declared_argument_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template<typename T> struct Allocator { T* Value; };
            class Cache
            {
            private:
                struct Slot;
                Allocator<Slot> Storage;
            };
            """);
        var cache = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Cache");
        var specialization = cache.Fields.Single().Type.CanonicalType.AsCXXRecordDecl
            ?? throw new InvalidOperationException("Allocator specialization was not found.");
        var manager = CreateManager();

        manager.Add(specialization);

        await Assert.That(manager.RecordModels.Select(static model => model.Type.CppTypeName))
            .DoesNotContain("Allocator<Cache::Slot>");
    }

    /// <summary>
    /// Verifies C++ standard stream ownership APIs are excluded while unrelated methods remain.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_include_representable_stream_and_handle_dependencies_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace std
            {
                template<typename T> struct basic_istream {};
                template<typename T> struct basic_ostream {};
                template<typename T> struct basic_streambuf {};
                template<typename T> struct shared_ptr { T* Value; };
            }

            template struct std::basic_istream<char>;
            template struct std::basic_ostream<char>;
            template struct std::basic_streambuf<char>;
            template struct std::shared_ptr<std::basic_istream<char>>;
            template struct std::shared_ptr<std::basic_ostream<char>>;
            template struct std::shared_ptr<std::basic_streambuf<char>>;

            template<typename T> struct NCollection_Handle { void* Value; };

            struct FileSystem
            {
                NCollection_Handle<int> InternalHandle;
                int IsSupportedPath() const;
                std::shared_ptr<std::basic_istream<char>> OpenIStream();
                std::shared_ptr<std::basic_ostream<char>> OpenOStream();
                void AcceptBuffer(const std::shared_ptr<std::basic_streambuf<char>>& value);
                NCollection_Handle<int> UnsupportedHandle();
                void AcceptHandle(const NCollection_Handle<int>& value);
            };
            """);
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "FileSystem");
        var manager = CreateManager();

        manager.Add(record);

        var fileSystem = manager.RecordModels.Single(static model => model.Type.CppTypeName == "FileSystem");

        await Assert.That(fileSystem.MethodModels.Select(static method => method.MethodName))
            .IsEquivalentTo([
                "IsSupportedPath",
                "OpenIStream",
                "OpenOStream",
                "AcceptBuffer",
                "UnsupportedHandle",
                "AcceptHandle",
            ]);
        await Assert.That(fileSystem.FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["InternalHandle",]);
        await Assert.That(manager.RecordModels.Select(static model => model.Type.CppTypeName))
            .Contains("std::shared_ptr<std::basic_istream<char>>");
    }

    /// <summary>
    /// Verifies enums referenced by fields are collected and documented.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_collect_referenced_enums_when_record_fields_use_them_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            //! Supported color kinds.
            enum class Quantity_TypeOfColor : unsigned char
            {
                //! RGB space.
                Quantity_TypeOfColor_RGB = 1,
                Quantity_TypeOfColor_sRGB = 2,
            };

            //! Holder summary.
            struct Holder
            {
                //! Field summary.
                Quantity_TypeOfColor Field;

                //! Method summary.
                //! @param value Value summary.
                //! @return Return summary.
                int Method(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder");

        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Count).IsEqualTo(1);
        await Assert.That(manager.EnumModels.Count).IsEqualTo(1);
        await Assert.That(manager.RecordModels.Single().FieldModels.Single().Type.CppTypeName)
            .IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(manager.EnumModels.Single().Name).IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(Render(manager.RecordModels.Single().DescriptionItems.Single()))
            .Contains("Holder summary.");
        await Assert.That(Render(manager.RecordModels.Single().FieldModels.Single().DescriptionItems.Single()))
            .Contains("Field summary.");
        await Assert.That(Render(manager.RecordModels.Single().MethodModels.Single().DescriptionItems.Single()))
            .Contains("Method summary.");
        await Assert.That(Render(manager.RecordModels.Single().MethodModels.Single().Parameters.Single().DescriptionItems.Single()))
            .Contains("Value summary.");
        await Assert.That(Render(new DescriptionReturns(manager.RecordModels.Single().MethodModels.Single().ReturnTypeDescriptionItems)))
            .Contains("Return summary.");
        await Assert.That(Render(manager.EnumModels.Single().DescriptionItems.Single()))
            .Contains("Supported color kinds.");
        await Assert.That(Render(manager.EnumModels.Single().Members
                .Single(static member => member.Name == "Quantity_TypeOfColor_RGB")
                .DescriptionItems.Single()))
            .Contains("RGB space.");
    }

    /// <summary>
    /// Verifies constructors, destructors, and operators are classified correctly.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_keep_only_declared_callable_methods_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Base
            {
                Base();
                ~Base();
                void BaseMethod();
            };

            struct Derived : Base
            {
                Derived();
                ~Derived();
                void OwnMethod();
                bool operator==(const Derived&) const;
                static void* operator new(unsigned long long size);
                static void operator delete(void* ptr);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Derived");

        var manager = CreateManager();

        manager.Add(record);

        var methodNames = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName))
            .ToArray();

        await Assert.That(methodNames).IsEquivalentTo([
            (MethodModelType.NEW, "New"),
            (MethodModelType.DELETE, "Delete"),
            (MethodModelType.NORMAL, "OwnMethod"),
            (MethodModelType.OPERATOR, "=="),
        ]);
    }

    /// <summary>
    /// Verifies non-callable methods are excluded from generated method models.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_non_callable_methods_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                void Reset() = delete;
                void Legacy() __attribute__((unavailable("legacy API")));
                void Write(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "StreamLike");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName, ParameterCount: m.Parameters.Count))
            .ToArray();

        await Assert.That(methods.Length).IsEqualTo(2);
        await Assert.That(methods).Contains((MethodModelType.NEW, "New", 0));
        await Assert.That(methods).Contains((MethodModelType.NORMAL, "Write", 1));
    }

    /// <summary>
    /// Verifies deleted copy constructors are excluded from generated constructor models.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_deleted_copy_constructor_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                StreamLike(const StreamLike&) = delete;
                void Write(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "StreamLike");

        var manager = CreateManager();

        manager.Add(record);

        var constructors = manager.RecordModels.Single().MethodModels
            .Where(static m => m.Type == MethodModelType.NEW)
            .Select(static m => m.Parameters.Count)
            .ToArray();

        await Assert.That(constructors).IsEquivalentTo([0,]);
    }

    /// <summary>
    /// Verifies constructors that take non-copyable record values are excluded.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_constructors_with_non_copyable_value_parameters_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                StreamLike(const StreamLike&) = delete;
            };

            struct Holder
            {
                Holder(StreamLike stream);
                Holder(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder");

        var manager = CreateManager();

        manager.Add(record);

        var constructors = manager.RecordModels.Single(static model => model.Type.CppTypeName == "Holder").MethodModels
            .Where(static m => m.Type == MethodModelType.NEW)
            .Select(static m => m.Parameters.Select(p => p.Type.CppTypeName).ToArray())
            .ToArray();

        await Assert.That(constructors.Length).IsEqualTo(1);
        await Assert.That(constructors.Single()).IsEquivalentTo(["int",]);
    }

    /// <summary>
    /// Verifies constructor defaults are expanded and overlapping signatures prefer the fuller declaration.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_apply_overload_priority_to_defaulted_constructors_Async()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cpp");
        const string source = """
            struct Array
            {
                Array(int increment = 256);
                Array(int count, int increment = 256);
            };
            """;
        await File.WriteAllTextAsync(filePath, source).ConfigureAwait(false);
        try
        {
            using var translationUnit = ParseTranslationUnit(source, filePath);
            var record = translationUnit.TranslationUnitDecl.CursorChildren
                .OfType<CXXRecordDecl>()
                .Single(static value => value.Name == "Array");
            var manager = CreateManager();

            manager.Add(record);

            var constructors = manager.RecordModels.Single().MethodModels
                .Where(static method => method.Type == MethodModelType.NEW)
                .OrderBy(static method => method.Parameters.Count)
                .ToArray();
            await Assert.That(constructors.Select(static method => method.Parameters.Count))
                .IsEquivalentTo([0, 1, 2,]);
            await Assert.That(constructors[0].NativeDefaultArguments).IsEmpty();
            await Assert.That(constructors[1].Parameters[0].Name).IsEqualTo("count");
            await Assert.That(constructors[1].NativeDefaultArguments).IsEquivalentTo(["256",]);
            await Assert.That(constructors[2].NativeDefaultArguments).IsEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Verifies methods with unnamed parameters are excluded from generated method models.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_methods_with_unnamed_parameters_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                void Write(int);
                void Flush();
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "StreamLike");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName, ParameterCount: m.Parameters.Count))
            .ToArray();

        await Assert.That(methods).IsEquivalentTo([
            (MethodModelType.NEW, "New", 0),
            (MethodModelType.NORMAL, "Flush", 0),
        ]);
    }

    /// <summary>
    /// Verifies fields whose types are anonymous declarations are excluded from the model.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_fields_with_anonymous_types_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Holder
            {
                enum
                {
                    Value,
                } InternalState;
                union
                {
                    double Floating;
                    long long Integer;
                } InternalStorage;
                int PublicValue;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Holder");

        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Single().FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["PublicValue",]);
    }

    /// <summary>
    /// Verifies implicit and explicit conversion operators are classified correctly.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_classify_conversion_operators_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Value
            {
                operator bool() const;
                explicit operator int() const;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName))
            .ToArray();

        await Assert.That(methods).IsEquivalentTo([
            (MethodModelType.IMPLICIT, "Implicit"),
            (MethodModelType.EXPLICIT, "Explicit"),
        ]);
    }

    /// <summary>
    /// Verifies records referenced by fields are collected transitively.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_collect_referenced_records_when_record_fields_use_them_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Child
            {
                int Value;
            };

            struct Parent
            {
                Child Field;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Parent");

        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Count).IsEqualTo(2);
        await Assert.That(manager.RecordModels.Select(static m => m.Type.CppTypeName))
            .IsEquivalentTo(["Child", "Parent",]);
        await Assert.That(manager.RecordModels.Single(static m => m.Type.CppTypeName == "Parent")
            .FieldModels.Single().Type.CppTypeName)
            .IsEqualTo("Child");
    }

    /// <summary>
    /// Verifies a derived representation does not redeclare fields owned by its base representation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_model_only_fields_declared_by_each_record_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Base
            {
                int SharedName;
            };

            struct Derived : Base
            {
                int SharedName;
            };
            """, "__occt__/test.cpp");
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Derived");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Single(static model => model.Type.CppTypeName == "Base")
            .FieldModels.Select(static field => field.Name)).IsEquivalentTo(["SharedName",]);
        await Assert.That(manager.RecordModels.Single(static model => model.Type.CppTypeName == "Derived")
            .FieldModels.Select(static field => field.Name)).IsEquivalentTo(["SharedName",]);
    }

    /// <summary>
    /// Verifies handle specializations are unwrapped to the referenced record before model creation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the handle field does not resolve to a C++ record.</exception>
    [Test]
    public async Task Should_unwrap_handle_specializations_before_adding_record_models_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace opencascade
            {
                template<typename T>
                class handle
                {
                public:
                    handle();
                    explicit handle(const T* value);
                    T* get() const;
                };
            }

            struct Geom2d_Curve
            {
                int Value;
            };

            struct Holder
            {
                opencascade::handle<Geom2d_Curve> Value;
            };
            """, "__occt__/test.cpp");

        var handleRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder")
            .Fields
            .Single()
            .Type
            .AsCXXRecordDecl
            ?? throw new InvalidOperationException("Handle field type did not resolve to a CXX record declaration.");

        var manager = CreateManager();

        var model = manager.Add(handleRecord);

        await Assert.That(model.Type.CppTypeName).IsEqualTo("Geom2d_Curve");
        await Assert.That(model.FieldModels.Single().Name).IsEqualTo("Value");
        await Assert.That(manager.RecordModels.Select(static m => m.Type.CppTypeName))
            .IsEquivalentTo(["Geom2d_Curve",]);
    }

    /// <summary>
    /// Verifies fields whose handle target is only forward declared are excluded from record generation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_handle_fields_with_incomplete_targets_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace opencascade
            {
                template<typename T>
                class handle
                {
                public:
                    handle();
                };
            }

            struct Holder
            {
                struct Base;
                opencascade::handle<Base> Internal;
                int Value;
            };
            """, "__occt__/test.cpp");

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder");
        var manager = CreateManager();

        var model = manager.Add(record);

        await Assert.That(model.FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["Value",]);
        await Assert.That(manager.RecordModels.Select(static item => item.Type.CppTypeName))
            .IsEquivalentTo(["Holder",]);
    }

    /// <summary>
    /// Verifies specializations that expose a private template argument are excluded as unnameable dependencies.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_specializations_with_private_template_arguments_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template<typename T>
            struct Wrapper
            {
                T Value;
            };

            struct Owner
            {
            private:
                enum class State
                {
                    Ready,
                };

            public:
                Wrapper<State> Internal;
                int Value;
            };
            """, "__occt__/test.cpp");

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Owner");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Select(static item => item.Type.CppTypeName))
            .IsEquivalentTo(["Owner",]);
        await Assert.That(manager.RecordModels.Single().FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["Value",]);
    }

    /// <summary>
    /// Verifies a private nested template remains excluded even when its concrete argument is public.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_private_template_with_public_argument_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Owner
            {
            private:
                template<typename T>
                struct Hidden
                {
                    T Value;
                };

                Hidden<int> Internal;

            public:
                int Value;
            };
            """, "__occt__/test.cpp");
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Owner");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Select(static item => item.Type.CppTypeName))
            .IsEquivalentTo(["Owner",]);
    }

    /// <summary>
    /// Verifies template specialization field types collect both the template and template-argument headers.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_collect_template_argument_headers_for_field_types_Async()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var pointHeaderPath = Path.Combine(folder.FullName, "gp_Pnt2d.hxx");
            var arrayHeaderPath = Path.Combine(folder.FullName, "NCollection_Array1.hxx");
            var sourcePath = Path.Combine(folder.FullName, "test.cpp");

            await File.WriteAllTextAsync(pointHeaderPath, """
                struct gp_Pnt2d
                {
                };
                """).ConfigureAwait(false);
            await File.WriteAllTextAsync(arrayHeaderPath, """
                template <typename T>
                struct NCollection_Array1
                {
                    T Value() const;
                };
                """).ConfigureAwait(false);
            await File.WriteAllTextAsync(sourcePath, """
                #include "gp_Pnt2d.hxx"
                #include "NCollection_Array1.hxx"

                struct Holder
                {
                    NCollection_Array1<gp_Pnt2d> Field;
                };
                """).ConfigureAwait(false);

            using var translationUnit =
                ParseTranslationUnit(await File.ReadAllTextAsync(sourcePath).ConfigureAwait(false), sourcePath);
            var record = translationUnit.TranslationUnitDecl.CursorChildren
                .OfType<CXXRecordDecl>()
                .Single(static r => r.Name == "Holder");

            var manager = CreateManager();

            manager.Add(record);

            var requiredHeaders = manager.RecordModels.Single(static m => m.Type.CppTypeName == "Holder")
                .FieldModels.Single()
                .Type.RequiredHeaders;
            await Assert.That(requiredHeaders).IsEquivalentTo(["NCollection_Array1.hxx", "gp_Pnt2d.hxx",]);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies placeholder template specializations are omitted while fixed specializations remain generatable.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_only_fixed_template_specializations_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename T = void>
            struct Traverse
            {
            };

            struct Owner
            {
                Traverse<> Placeholder;
                Traverse<int> Fixed;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var specializations = owner.Fields
            .Select(static field => field.Type.AsCXXRecordDecl?.Definition)
            .OfType<CXXRecordDecl>()
            .ToArray();
        var manager = CreateManager();

        foreach (var specialization in specializations)
        {
            manager.Add(specialization);
        }

        await Assert.That(manager.RecordModels.Select(static record => record.Type.CppTypeName))
            .IsEquivalentTo(["Traverse<int>",]);
    }

    /// <summary>
    /// Verifies an implementation-policy void argument does not hide a specialization with a fixed value type.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_fixed_specialization_with_void_policy_argument_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename TValue, typename TPolicy = void>
            struct Shared
            {
                TValue Value;
            };

            struct Owner
            {
                Shared<int> Field;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var manager = CreateManager();

        manager.Add(owner);

        await Assert.That(manager.RecordModels.Select(static record => record.Type.CppTypeName))
            .Contains("Shared<int>");
    }

    /// <summary>
    /// Verifies representable type arguments remain generic while non-type and void arguments stay fixed.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_partially_generalize_mixed_template_arguments_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename TValue, int Size, typename TPolicy = void>
            struct Buffer
            {
                TValue Value;
            };

            struct Owner
            {
                Buffer<double, 4> Field;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var manager = CreateManager();

        manager.Add(owner);

        var buffer = manager.RecordModels.Single(static record =>
            record.Type.CppTypeName.StartsWith("Buffer<", StringComparison.Ordinal));
        await Assert.That(buffer.Type.CSharpTypeName).IsEqualTo("Buffer_4_void<double>");
    }

    /// <summary>
    /// Verifies a default native type argument is applied exactly once to its managed generic family.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_apply_default_template_argument_once_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename TValue = double>
            struct Vector
            {
                TValue Value;
            };

            struct Owner
            {
                Vector<> DefaultValue;
                Vector<int> IntValue;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var manager = CreateManager();

        manager.Add(owner);

        var vectorTypes = manager.RecordModels
            .Where(static record => record.Type.CppTypeName.StartsWith("Vector<", StringComparison.Ordinal))
            .Select(static record => record.Type.CSharpTypeName)
            .ToArray();
        await Assert.That(vectorTypes).Contains("Vector<double>");
        await Assert.That(vectorTypes).Contains("Vector<int>");
        await Assert.That(vectorTypes.All(static type => !type.Contains("><", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// Verifies native arguments that collapse to the same C# type retain distinct closed identities.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_keep_colliding_managed_template_arguments_closed_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename TValue>
            struct Hasher
            {
            };

            struct Owner
            {
                Hasher<wchar_t> Wide;
                Hasher<char16_t> Utf16;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var manager = CreateManager();

        manager.Add(owner);

        var hashers = manager.RecordModels
            .Where(static record => record.Type.CppTypeName.StartsWith("Hasher<", StringComparison.Ordinal))
            .ToArray();
        await Assert.That(hashers).Count().IsEqualTo(2);
        await Assert.That(hashers.All(static record => record.TemplateProjection is null)).IsTrue();
        await Assert.That(hashers.Select(static record => record.Type.CSharpTypeName).Distinct()).Count()
            .IsEqualTo(2);
    }

    /// <summary>
    /// Verifies a specialization selected through a partial template remains available as an exact closed type.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_keep_partial_template_specialization_closed_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename TValue, bool Enabled>
            struct Optional;

            template <typename TValue>
            struct Optional<TValue, true>
            {
                TValue Value;
            };

            struct Owner
            {
                Optional<int, true> Field;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var manager = CreateManager();

        manager.Add(owner);

        var optional = manager.RecordModels.Single(static record =>
            record.Type.CppTypeName.StartsWith("Optional<", StringComparison.Ordinal));
        await Assert.That(optional.TemplateProjection).IsNull();
        await Assert.That(optional.Type.CSharpTypeName).IsEqualTo("Optional_int_true");
    }

    /// <summary>
    /// Verifies a void-backed BVH traversal keeps callable overloads without instantiating its invalid shortcut.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_only_void_backed_bvh_shortcut_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename TValue, int Dimension, typename TSet = void, typename TMetric = TValue>
            struct BVH_PairTraverse
            {
                int Select();
                int Select(int first, int second);
            };

            struct Owner
            {
                BVH_PairTraverse<double, 3> Traversal;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var manager = CreateManager();

        manager.Add(owner);

        var traversal = manager.RecordModels.Single(static record =>
            record.Type.CppTypeName.StartsWith("BVH_PairTraverse<", StringComparison.Ordinal));
        await Assert.That(traversal.MethodModels.Where(static method => method.MethodName == "Select"))
            .HasSingleItem();
        await Assert.That(traversal.MethodModels.Single(static method => method.MethodName == "Select").Parameters)
            .Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies non-const methods win when const and non-const overloads collapse to the same signature.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_prefer_non_const_method_when_csharp_signature_matches_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Value
            {
                int Coord();
                int Coord() const;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Where(static m => m.MethodName == "Coord")
            .ToArray();

        await Assert.That(methods).HasSingleItem();
        await Assert.That(methods.Single().IsConst).IsFalse();
    }

    /// <summary>
    /// 验证左值引用、右值引用和指针参数在 PInvoke 签名一致时会折叠为一个方法。
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_collapse_pointer_and_reference_overloads_when_pinvoke_signature_matches_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Geom_Surface
            {
            };

            struct Value
            {
                void Attach(Geom_Surface* value);
                void Attach(Geom_Surface& value);
                void Attach(Geom_Surface&& value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single(static m => m.Type.CppTypeName == "Value").MethodModels
            .Where(static m => m.MethodName == "Attach")
            .ToArray();

        await Assert.That(methods).HasSingleItem();
        await Assert.That(methods.Single().Parameters.Single().Type.CSharpPInvokeType.ToCode())
            .IsEqualTo("Geom_Surface*");
    }

    /// <summary>
    /// 验证赋值类操作符会标记为返回自身以匹配 pinvoke 包装。
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_mark_assignment_operator_as_return_self_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Value
            {
                Value& operator+=(const Value& other);
                Value& operator-=(const Value& other);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Where(static m => m.Type == MethodModelType.OPERATOR)
            .OrderBy(static m => m.MethodName)
            .ToArray();

        await Assert.That(methods.Select(static m => m.MethodName))
            .IsEquivalentTo(["+=", "-=",]);
        await Assert.That(methods.All(static m => m.ReturnType.CppTypeName == "Value &")).IsTrue();
        await Assert.That(methods.All(static m => m.ReturnSelf)).IsTrue();
    }

    /// <summary>
    /// Verifies Standard_Transient ancestry is projected independently from other inheritance.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_project_standard_transient_ancestry_only_for_matching_records_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Standard_Transient
            {
            };

            struct HeapOnlyBase
            {
            };

            struct HeapOnlyDerived : HeapOnlyBase
            {
                HeapOnlyDerived();
            };

            struct TransientDerived : Standard_Transient
            {
                TransientDerived();
            };

            template<typename T>
            struct TransientTemplate
            {
            private:
                struct Base : Standard_Transient
                {
                };

            public:
                struct Static : Base
                {
                };
            };

            struct NestedTransientDerived : TransientTemplate<int>::Static
            {
            };
            """, "__occt__/test.cpp");

        var heapOnlyRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "HeapOnlyDerived");
        var transientRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "TransientDerived");
        var nestedTransientRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "NestedTransientDerived");

        var manager = CreateManager();

        var heapOnlyModel = manager.Add(heapOnlyRecord);
        var transientModel = manager.Add(transientRecord);
        var nestedTransientModel = manager.Add(nestedTransientRecord);

        await Assert.That(heapOnlyModel.IsStandardTransient).IsFalse();
        await Assert.That(transientModel.IsStandardTransient).IsTrue();
        await Assert.That(nestedTransientModel.IsStandardTransient).IsTrue();
    }

    /// <summary>
    /// Verifies a fixed field is not mistaken for a coincident specialization argument.
    /// </summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Should_preserve_fixed_field_types_in_shared_template_families_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template<class TValue> struct Box { TValue Value; float Fixed; };
            struct Owner { Box<float> First; Box<int> Second; };
            """);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner"));
        var records = manager.RecordModels.ToArray();
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            DeclOptions = [], CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var slots = NativeExportInventory.GetExports(records).Select(static (name, index) => (name, index))
            .ToDictionary(static entry => entry.name, static entry => entry.index, StringComparer.Ordinal);
        var boxes = records.Where(static record => record.Type.CppTypeName.StartsWith("Box<", StringComparison.Ordinal)).ToArray();
        await Assert.That(boxes.Length).IsEqualTo(2);
        foreach (var record in boxes)
        {
            await Assert.That(record.TemplateProjection).IsNotNull();
            var source = await new CSharpGenerator(record, options, nativeFunctionIndices: slots)
                .GenerateAsync(CancellationToken.None).ConfigureAwait(false);
            await Assert.That(source).Contains("public TValue Value;");
            await Assert.That(source).Contains("public float Fixed;");
            await Assert.That(source).DoesNotContain("public TValue Fixed;");
        }
    }

    /// <summary>
    /// Verifies family sharing waits for native ownership classifications and preserves dependent references.
    /// </summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Should_finalize_template_families_after_native_lifetime_classification_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct zOwned { int Value; ~zOwned() {} };
            template<class TValue> struct Box { TValue Value; Box() = default; };
            struct Owner { Box<int> First; Box<zOwned> Second; };
            """);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner"));
        var records = manager.NativePreparationRecords.OrderBy(static record => record.Type.CppTypeName, StringComparer.Ordinal).ToArray();
        var nativeFacts = records.Select(static (record, index) =>
        {
            var trivial = record.Type.CppTypeName is "Box<int>" ? 1 : 0;
            return $"{index}\t{record.Size}\t4\t{trivial}\t{trivial}\t1\n";
        });
        OcctCompilerProbeModule.CompleteRecords(records, string.Concat(nativeFacts));
        var completed = manager.RecordModels.ToArray();
        var boxes = completed.Where(static record => record.Type.CppTypeName.StartsWith("Box<", StringComparison.Ordinal)).ToArray();
        await Assert.That(boxes.Length).IsEqualTo(2);
        await Assert.That(boxes.All(static record => record.TemplateProjection is null)).IsTrue();
        await Assert.That(boxes.Select(static record => record.Type.CSharpTypeName))
            .IsEquivalentTo(["Box_int", "Box_zOwned",]);
        await Assert.That(boxes.Select(static record => record.ObjectKind))
            .IsEquivalentTo([NativeObjectKind.Value, NativeObjectKind.Owned,]);
        var owner = completed.Single(static record => record.Type.CppTypeName == "Owner");
        await Assert.That(owner.FieldModels.Select(static field => field.Type.CSharpTypeName))
            .IsEquivalentTo(["Box_int", "Box_zOwned",]);
    }

    /// <summary>
    /// Verifies an explicit specialization uses its own native field declarations.
    /// </summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Should_keep_explicit_template_specializations_closed_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template<class T> struct Box { T Value; };
            template<> struct Box<int> { float Value; };
            struct Owner { Box<int> First; Box<float> Second; };
            """);
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner"));
        var record = manager.RecordModels.Single(static record => record.Type.CppTypeName == "Box<int>");
        await Assert.That(record.TemplateProjection).IsNull();
        await Assert.That(record.Type.CSharpTypeName).IsEqualTo("Box_int");
        await Assert.That(record.FieldModels.Single().Type.CSharpTypeName).IsEqualTo("float");
    }

    /// <summary>
    /// Verifies shared templates retain fixed bases and differing native bases remain closed.
    /// </summary>
    /// <param name="dependent">Whether each specialization has a distinct native base.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_preserve_exact_template_base_relationships_Async(bool dependent)
    {
        var baseArgument = dependent ? "T" : "float";
        using var translationUnit = ParseTranslationUnit($$"""
            template<class T> struct Base { int Read() const { return 7; } };
            template<class T> struct Box : Base<{{baseArgument}}> { T Value; };
            struct Owner { Box<float> First; Box<int> Second; };
            """, "__occt__/test.cpp");
        var manager = CreateManager();
        manager.Add(translationUnit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner"));
        var records = manager.RecordModels.ToArray();
        var boxes = records.Where(static record => record.Type.CppTypeName.StartsWith("Box<", StringComparison.Ordinal)).ToArray();
        await Assert.That(boxes.Length).IsEqualTo(2);
        var options = Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            DeclOptions = [], CSharpFolder = new(Path.GetTempPath()), CppFolder = new(Path.GetTempPath()),
        });
        var slots = NativeExportInventory.GetExports(records).Select(static (name, index) => (name, index))
            .ToDictionary(static entry => entry.name, static entry => entry.index, StringComparer.Ordinal);
        foreach (var record in boxes)
        {
            await Assert.That(record.TemplateProjection is null).IsEqualTo(dependent);
            var relation = record.Bases.Single();
            await Assert.That(relation.Base.MethodModels.Any(static method => method.MethodName == "Read")).IsTrue();
            var source = await new CSharpGenerator(record, options, nativeFunctionIndices: slots)
                .GenerateAsync(CancellationToken.None).ConfigureAwait(false);
            await Assert.That(source).Contains(relation.Base.Type.CSharpInterfaceName);
            await Assert.That(source).DoesNotContain("IBase<T>");
        }

        var owner = records.Single(static record => record.Type.CppTypeName == "Owner");
        string[] expectedFields = dependent ? ["Box_float", "Box_int",] : ["Box<float>", "Box<int>",];
        await Assert.That(owner.FieldModels.Select(static field => field.Type.CSharpTypeName))
            .IsEquivalentTo(expectedFields);
    }

    private static RecordModelManager CreateManager(params ITypeRule[] rules)
    {
        return new(
            Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
            {
                DeclOptions = [],
                CSharpFolder = new(Path.GetTempPath()),
                CppFolder = new(Path.GetTempPath()),
            }),
            new Resolver(rules),
            new FakeVcpkgDefaultTripletResolver(),
            new FakeVcpkgEnvironment());
    }

    private static TranslationUnit ParseTranslationUnit(string source, string filePath = "test.cpp")
    {
        using var file = CXUnsavedFile.Create(filePath, source);
        var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            filePath,
            ["-std=c++20", "-x", "c++",],
            [file,],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        return TranslationUnit.GetOrCreate(translationUnit);
    }

    private static string Render(IToDescription description)
    {
        var builder = new SourceBuilder();
        description.ToDescription(ref builder);
        return SourceBuilderField.GetValue(builder)?.ToString()
               ?? throw new InvalidOperationException("Unable to render RoslynHelper description.");
    }

    private sealed class FakeVcpkgEnvironment : IVcpkgEnvironment
    {
        public Task<string> GetIncludingHeaderContentAsync(
            string triplet,
            IReadOnlyList<OcctDeclarationOptions> declarations,
            CancellationToken cancellationToken)
        {
            return Task.FromResult("");
        }

        public string GetRoot()
        {
            return "";
        }

        public string GetIncludeFolder(string triplet)
        {
            return "";
        }

        public string GetOcctIncludeFolder(string triplet)
        {
            return "__occt__";
        }
    }

    private sealed class FakeVcpkgDefaultTripletResolver : IVcpkgDefaultTripletResolver
    {
        public string GetTriplet()
        {
            return "x64-windows";
        }
    }
}