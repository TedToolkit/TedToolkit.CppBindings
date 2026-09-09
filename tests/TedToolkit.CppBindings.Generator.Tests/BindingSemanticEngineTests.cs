// -----------------------------------------------------------------------
// <copyright file="BindingSemanticEngineTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Generator.Tests;

/// <summary>
/// Verifies the provider-neutral semantic extension boundary.
/// </summary>
internal sealed class BindingSemanticEngineTests
{
    /// <summary>
    /// Verifies ordered type rule selection and provider-owned projection.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_use_the_first_matching_type_rule_Async()
    {
        var first = new TypeRule(false, "ignored");
        var second = new TypeRule(true, "Projected");
        var third = new TypeRule(true, "Wrong");
        var engine = CreateEngine([first, second, third,]);

        var result = engine.ResolveType(new("fixture::value", IsConst: true, PointerDepth: 1));

        await Assert.That(result.ManagedTypeName).IsEqualTo("Projected");
        await Assert.That(first.Calls).IsEqualTo(1);
        await Assert.That(second.Calls).IsEqualTo(1);
        await Assert.That(third.Calls).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies provider-specific layout and template admission.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_delegate_admission_to_provider_policy_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);

        var admitted = engine.AdmitLayout(new("fixture::value", 16, 8));
        var rejected = engine.AdmitLayout(new("fixture::value", 16, 16));
        var template = engine.AdmitTemplate(new("fixture::box", ["int",], isClosed: true));

        await Assert.That(admitted.IsAdmitted).IsTrue();
        await Assert.That(rejected.IsAdmitted).IsFalse();
        await Assert.That(rejected.Reason).Contains("alignment");
        await Assert.That(template.IsAdmitted).IsTrue();
    }

    /// <summary>
    /// Verifies normalized template descriptors do not retain caller-owned collections.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_snapshot_template_arguments_Async()
    {
        var arguments = new List<string>() { "int", };
        var descriptor = new BindingTemplateDescriptor("fixture::box", arguments, isClosed: true);

        arguments[0] = "mutated";
        arguments.Add("double");

        await Assert.That(descriptor.Arguments).IsEquivalentTo(["int",]);
    }

    /// <summary>
    /// Verifies named emitter primitives remain provider-owned.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_emit_through_a_named_provider_primitive_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);

        var result = engine.Emit("source-stem", "fixture::value");

        await Assert.That(result).IsEqualTo("fixture_value");
    }

    /// <summary>
    /// Verifies Shared closes provider dependencies and constructs both sides of one plan.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_construct_a_paired_plan_from_the_root_dependency_closure_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var dependency = CreateDeclaration("fixture::dependency", "Dependency", isRoot: false, [], "DependencyExport");
        var root = CreateDeclaration("fixture::root", "Root", isRoot: true, ["fixture::dependency",], "RootExport");
        root.Record.Bases =
        [
            new()
            {
                Base = dependency.Record,
                IsPublic = true,
                IsVirtual = false,
                PointerAdjustment = PointerAdjustmentKind.Identity,
            },
        ];
        var unreachable = CreateDeclaration("fixture::unreachable", "Unreachable", isRoot: false, [], "UnreachableExport");
        var providerModel = new BindingProviderModel(
            [unreachable, root, dependency,],
            [],
            CreateEmissionProfile(),
            [],
            [],
            [],
            "source-stem",
            "source-stem",
            nativeProject: null);

        var plan = engine.CreatePlan(providerModel);

        await Assert.That(plan.CSharpSources.Select(static source => source.RelativePath))
            .IsEquivalentTo(["fixture_dependency.g.cs", "fixture_root.g.cs",]);
        await Assert.That(plan.CppSources.Select(static source => source.RelativePath))
            .IsEquivalentTo(["fixture_dependency.cpp", "fixture_root.cpp",]);
        await Assert.That(plan.NativeExports).IsEquivalentTo(["DependencyExport", "RootExport",]);
        var managedWriter = new StringWriter();
        await using (managedWriter.ConfigureAwait(false))
        {
            await plan.CSharpSources.Single(static source => source.RelativePath == "fixture_root.g.cs")
                .RenderAsync(managedWriter, CancellationToken.None).ConfigureAwait(false);
            await Assert.That(managedWriter.ToString()).Contains("public unsafe struct Root");
            await Assert.That(managedWriter.ToString()).Contains("public int Value;");
            await Assert.That(managedWriter.ToString()).Contains("in int input");
            await Assert.That(managedWriter.ToString()).Contains("NativeApi.GetFunction(1)");
        }

        var nativeWriter = new StringWriter();
        await using (nativeWriter.ConfigureAwait(false))
        {
            await plan.CppSources.Single(static source => source.RelativePath == "fixture_root.cpp")
                .RenderAsync(nativeWriter, CancellationToken.None).ConfigureAwait(false);
            await Assert.That(nativeWriter.ToString()).Contains("#include <fixture_root.hxx>");
            await Assert.That(nativeWriter.ToString()).Contains("extern \"C\" int RootExport(");
            await Assert.That(nativeWriter.ToString()).Contains("const int* input");
        }
    }

    /// <summary>
    /// Verifies providers can hide ABI storage behind a read-only managed value property.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_project_hidden_abi_storage_as_a_read_only_property_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var declaration = CreateDeclaration("fixture::value", "Value", true, [], "ValueExport");
        var original = declaration.Record.FieldModels[0];
        declaration.Record.FieldModels =
        [
            new()
            {
                DescriptionItems = original.DescriptionItems,
                Offset = original.Offset,
                Size = original.Size,
                Alignment = original.Alignment,
                Name = "StorageValue",
                IsManagedStoragePrivate = true,
                ManagedReadOnlyPropertyName = "Value",
                Type = original.Type,
            },
        ];
        var model = new BindingProviderModel(
            [declaration,],
            [],
            CreateEmissionProfile(),
            [],
            [],
            [],
            "source-stem",
            "source-stem",
            nativeProject: null);

        var plan = engine.CreatePlan(model);
        var managed = await RenderAsync(plan.CSharpSources.Single(static source =>
            source.RelativePath == "fixture_value.g.cs")).ConfigureAwait(false);

        await Assert.That(managed).Contains("private int StorageValue;");
        await Assert.That(managed).Contains("public readonly int Value");
        await Assert.That(managed).Contains("return StorageValue;");
        await Assert.That(managed).DoesNotContain("public int StorageValue;");
    }

    /// <summary>
    /// Verifies a completed plan does not retain caller-owned nested semantic state.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_snapshot_the_complete_semantic_graph_before_creating_a_plan_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var nestedDescriptionTable = new DescriptionTable(
            new DescriptionText("Term original."),
            new DescriptionText("Definition original."));
        var recordSummaryItems = new List<IDescriptionItem>()
        {
            new DescriptionText("Record original."),
            nestedDescriptionTable,
        };
        var fieldDescription = new MutableDescription("<summary>Field original.</summary>");
        var methodDescription = new MutableDescription("<summary>Method original.</summary>");
        var returnDescription = new MutableDescription("Return original.");
        var parameterDescription = new MutableDescription("Parameter original.");
        var declaration = CreateDeclaration(
            "fixture::root",
            "Root",
            isRoot: true,
            [],
            "RootExport",
            [new DescriptionSummary(recordSummaryItems),],
            [fieldDescription,],
            [methodDescription,],
            [returnDescription,],
            [parameterDescription,]);
        var enumDescription = new MutableDescription("<summary>Enum original.</summary>");
        var enumMemberDescription = new MutableDescription("<summary>Member original.</summary>");
        var enumMembers = new List<EnumMemberModel>()
        {
            new()
            {
                DescriptionItems = [enumMemberDescription,],
                Name = "Original",
                Value = 1.ToLiteral(),
            },
        };
        var enumUnderlyingType = new DataType("int");
        var providerModel = new BindingProviderModel(
            [declaration,],
            [
                new()
                {
                    DescriptionItems = [enumDescription,],
                    Name = "FixtureState",
                    SourceType = "fixture::state",
                    UnderlyingType = enumUnderlyingType,
                    Members = enumMembers,
                },
            ],
            CreateEmissionProfile(),
            [],
            [],
            [],
            "source-stem",
            "source-stem",
            nativeProject: null);

        var plan = engine.CreatePlan(providerModel);

        var managedBeforeMutation = await RenderAsync(
            plan.CSharpSources.Single(static source => source.RelativePath == "fixture_root.g.cs"))
            .ConfigureAwait(false);
        var enumBeforeMutation = await RenderAsync(
            plan.CSharpSources.Single(static source => source.RelativePath == "FixtureState.g.cs"))
            .ConfigureAwait(false);

        declaration.Record.MethodModels[0].NativeExportName = "MutatedExport";
        declaration.Record.Alignment = 16;
        declaration.Record.Type.CSharpPublicType.PointCounter = 2;
        declaration.Record.FieldModels[0].Type.CSharpPInvokeType.PointCounter = 1;
        recordSummaryItems.Add(new DescriptionText("Record mutated."));
        nestedDescriptionTable.Items.Add((
            new DescriptionText("Term mutated."),
            new DescriptionText("Definition mutated.")));
        fieldDescription.Text = "<summary>Field mutated.</summary>";
        methodDescription.Text = "<summary>Method mutated.</summary>";
        returnDescription.Text = "Return mutated.";
        parameterDescription.Text = "Parameter mutated.";
        enumDescription.Text = "<summary>Enum mutated.</summary>";
        enumMemberDescription.Text = "<summary>Member mutated.</summary>";
        declaration.Record.FieldModels = [];
        enumUnderlyingType.PointCounter = 1;
        enumMembers[0] = new()
        {
            DescriptionItems = [],
            Name = "Mutated",
            Value = 2.ToLiteral(),
        };

        var managed = await RenderAsync(
            plan.CSharpSources.Single(static source => source.RelativePath == "fixture_root.g.cs"))
            .ConfigureAwait(false);
        var native = await RenderAsync(
            plan.CppSources.Single(static source => source.RelativePath == "fixture_root.cpp"))
            .ConfigureAwait(false);
        var enumSource = await RenderAsync(
            plan.CSharpSources.Single(static source => source.RelativePath == "FixtureState.g.cs"))
            .ConfigureAwait(false);

        await Assert.That(plan.NativeExports).IsEquivalentTo(["RootExport",]);
        await Assert.That(managed).IsEqualTo(managedBeforeMutation);
        await Assert.That(enumSource).IsEqualTo(enumBeforeMutation);
        await Assert.That(managed).Contains("public int Value;");
        await Assert.That(managed).Contains("NativeApi.GetFunction(0)");
        await Assert.That(native).Contains("extern \"C\" int RootExport(");
        await Assert.That(native).DoesNotContain("MutatedExport");
        await Assert.That(enumSource).Contains("Original = 1");
        await Assert.That(enumSource).DoesNotContain("Mutated");
        await Assert.That(managed).DoesNotContain("Record mutated.");
        await Assert.That(managed).DoesNotContain("Definition mutated.");
        await Assert.That(managed).DoesNotContain("Field mutated.");
        await Assert.That(fieldDescription.Calls).IsEqualTo(1);
        await Assert.That(methodDescription.Calls).IsEqualTo(1);
        await Assert.That(returnDescription.Calls).IsEqualTo(1);
        await Assert.That(parameterDescription.Calls).IsEqualTo(1);
        await Assert.That(enumDescription.Calls).IsEqualTo(1);
        await Assert.That(enumMemberDescription.Calls).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies the shared model rejects a declaration without proved lifetime classification.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_report_a_narrow_lifetime_rejection_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var declaration = CreateDeclaration("fixture::value", "Value", isRoot: true, [], "ValueExport");
        declaration.Record.ObjectKind = NativeObjectKind.Unknown;
        var providerModel = new BindingProviderModel(
            [declaration,],
            [],
            CreateEmissionProfile(),
            [],
            [],
            [],
            "source-stem",
            "source-stem",
            nativeProject: null);

        InvalidOperationException? failure = null;
        try
        {
            _ = engine.CreateModel(providerModel);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Message).Contains("lifetime");
    }

    /// <summary>
    /// Verifies a provider can opt into five-field diagnostics without changing four-field providers.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_emit_optional_provider_native_stack_expressions_Async()
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var declaration = CreateDeclaration(
            "fixture::root",
            "Root",
            isRoot: true,
            [],
            "RootExport",
            noExceptions: false);
        var providerModel = new BindingProviderModel(
            [declaration,],
            [],
            CreateEmissionProfile(withNativeStack: true),
            [],
            [],
            [],
            "source-stem",
            "source-stem",
            nativeProject: null);

        var plan = engine.CreatePlan(providerModel);
        var native = await RenderAsync(plan.CppSources.Single()).ConfigureAwait(false);

        await Assert.That(native)
            .Contains("NativeError_Set(__error, 16, \"failure\", exception.what(), Stack(exception));");
        await Assert.That(native).Contains("NativeError_Set(__error, 255, nullptr, nullptr, nullptr);");
    }

    /// <summary>
    /// Verifies the complete Shared standard C++ catch order and fixed error kinds.
    /// </summary>
    /// <returns>A task that completes when the catch contract is checked.</returns>
    [Test]
    public async Task Should_emit_the_fixed_shared_native_error_catch_sequence_Async()
    {
        var source = BindingNativeErrorCatchEmitter.Render("SetError", "error");
        var expected = new (string Type, int Code)[]
        {
            ("std::bad_alloc", 6),
            ("std::out_of_range", 2),
            ("std::overflow_error", 7),
            ("std::underflow_error", 3),
            ("std::invalid_argument", 1),
            ("std::domain_error", 1),
            ("std::logic_error", 4),
            ("std::exception", 8),
        };
        var previousPosition = -1;

        foreach (var (type, kind) in expected)
        {
            var catchText = $"catch (const {type}& exception)";
            var position = source.IndexOf(catchText, StringComparison.Ordinal);

            await Assert.That(position).IsGreaterThan(previousPosition);
            await Assert.That(source).Contains($"SetError(error, {kind}, \"{type}\", exception.what());");
            previousPosition = position;
        }

        await Assert.That(source.IndexOf("catch (...)", StringComparison.Ordinal)).IsGreaterThan(previousPosition);
        await Assert.That(source).Contains("SetError(error, 255, nullptr, nullptr);");
    }

    /// <summary>
    /// Verifies Providers cannot shadow a C++ exception type owned by the Shared catch sequence.
    /// </summary>
    /// <param name="cppType">The canonical or equivalent Shared-owned C++ type spelling.</param>
    /// <returns>A task that completes when validation finishes.</returns>
    [Test]
    [Arguments("std::bad_alloc")]
    [Arguments("::std::bad_alloc")]
    [Arguments("std::out_of_range")]
    [Arguments("::std::out_of_range")]
    [Arguments("std::overflow_error")]
    [Arguments("::std::overflow_error")]
    [Arguments("std::underflow_error")]
    [Arguments("::std::underflow_error")]
    [Arguments("std::invalid_argument")]
    [Arguments("::std::invalid_argument")]
    [Arguments("std::domain_error")]
    [Arguments("::std::domain_error")]
    [Arguments("std::logic_error")]
    [Arguments("::std::logic_error")]
    [Arguments("std::exception")]
    [Arguments("::std::exception")]
    [Arguments(" ::std :: exception ")]
    public async Task Should_reject_a_provider_override_of_a_shared_exception_type_Async(string cppType)
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var provider = CreateProviderModel(CreateEmissionProfile(
            projections: [new(cppType, 9, "\"provider\"", "exception.what()"),]));

        var action = () => engine.CreatePlan(provider);

        await Assert.That(action).Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies Providers cannot claim the Shared success or unknown discriminator.
    /// </summary>
    /// <param name="kind">The invalid Provider-local discriminator.</param>
    /// <returns>A task that completes when validation finishes.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(8)]
    [Arguments(255)]
    [Arguments(256)]
    public async Task Should_reject_provider_projections_outside_the_extension_range_Async(int kind)
    {
        var engine = CreateEngine([new TypeRule(true, "Projected"),]);
        var provider = CreateProviderModel(CreateEmissionProfile(
            projections: [new("fixture::failure", kind, "\"failure\"", "exception.what()"),]));

        var action = () => engine.CreatePlan(provider);

        await Assert.That(action).Throws<ArgumentException>();
    }

    private static BindingDeclaration CreateDeclaration(
        string nativeName,
        string managedName,
        bool isRoot,
        IReadOnlyList<string> dependencies,
        string export,
        IReadOnlyList<IRootDescriptionItem>? recordDescriptions = null,
        IReadOnlyList<IRootDescriptionItem>? fieldDescriptions = null,
        IReadOnlyList<IRootDescriptionItem>? methodDescriptions = null,
        IReadOnlyList<IDescriptionItem>? returnDescriptions = null,
        IReadOnlyList<IDescriptionItem>? parameterDescriptions = null,
        bool noExceptions = true)
    {
        var intType = new TypeModel()
        {
            CppTypeName = "int",
            CppValueTypeName = "int",
            CSharpPInvokeType = new("int"),
            CSharpPublicType = new("int"),
        };
        var constIntReference = new TypeModel()
        {
            CppTypeName = "const int&",
            CppValueTypeName = "int",
            CSharpPInvokeType = DataType.Int.Pointer,
            CSharpPublicType = DataType.Int.RefReadonly,
            Transport = new(true, [new(TypeIndirectionKind.LValueReference, false),]),
        };
        var record = new RecordModel()
        {
            DescriptionItems = recordDescriptions ?? [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            SourceHeader = nativeName.Replace("::", "_", StringComparison.Ordinal) + ".hxx",
            Type = new()
            {
                CppTypeName = nativeName,
                CppValueTypeName = nativeName,
                CSharpPInvokeType = new(managedName),
                CSharpPublicType = new(managedName),
                IsRecord = true,
            },
            Size = 8,
            Alignment = 4,
            ObjectKind = NativeObjectKind.Value,
            FieldModels =
            [
                new()
                {
                    DescriptionItems = fieldDescriptions ?? [],
                    Offset = 0,
                    Size = 4,
                    Alignment = 4,
                    Name = "Value",
                    Type = intType,
                },
            ],
            MethodModels =
            [
                new()
                {
                    DescriptionItems = methodDescriptions ?? [],
                    ReturnTypeDescriptionItems = returnDescriptions ?? [],
                    NativeExportName = export,
                    NoExceptions = noExceptions,
                    IsConst = false,
                    IsStatic = true,
                    ReturnType = intType,
                    MethodName = "Measure",
                    Type = MethodModelType.NORMAL,
                    Parameters =
                    [
                        new()
                        {
                            DescriptionItems = parameterDescriptions ?? [],
                            Type = constIntReference,
                            Name = "input",
                        },
                    ],
                },
            ],
        };
        return new(
            record,
            nativeName,
            isRoot,
            dependencies);
    }

    private static BindingEmissionProfile CreateEmissionProfile(
        bool withNativeStack = false,
        IReadOnlyList<BindingNativeExceptionProjection>? projections = null)
    {
        return new()
        {
            CSharpNamespace = "Fixture.Bindings",
            ManagedNativeErrorProjection = "global::Fixture.Bindings.NativeErrorProjection",
            NativeErrorClearExport = "NativeError_Clear",
            NativePreamble = "// <auto-generated />\n#include <new>\n#include <utility>\n",
            NativeErrorPreamble = "#include \"NativeError.h\"\n#include <exception>\n",
            NativeExceptionProjections = projections ?? (withNativeStack
                ? [new("fixture::failure", 16, "\"failure\"", "exception.what()", "Stack(exception)"),]
                : []),
            UnknownNativeStackExpression = withNativeStack ? "nullptr" : null,
        };
    }

    private static BindingProviderModel CreateProviderModel(BindingEmissionProfile emissionProfile)
    {
        return new([], [], emissionProfile, [], [], [], "source-stem", "source-stem", nativeProject: null);
    }

    private static async Task<string> RenderAsync(GeneratedSource source)
    {
        var writer = new StringWriter();
        await using (writer.ConfigureAwait(false))
        {
            await source.RenderAsync(writer, CancellationToken.None).ConfigureAwait(false);
            return writer.ToString();
        }
    }

    private static BindingSemanticEngine CreateEngine(IEnumerable<IBindingTypeRule> rules)
    {
        return new(rules, new LayoutPolicy(), new TemplatePolicy(), [new SourceStemEmitter(),]);
    }

    private sealed class TypeRule(bool matches, string managedName) : IBindingTypeRule
    {
        public int Calls { get; private set; }

        public bool TryResolve(CppTypeDescriptor type, out BindingTypeProjection? projection)
        {
            Calls++;
            projection = matches ? new(type.NativeName, managedName) : null;
            return matches;
        }
    }

    private sealed class LayoutPolicy : IBindingLayoutPolicy
    {
        public BindingAdmission Admit(BindingLayoutDescriptor layout)
        {
            return layout.Alignment <= 8
                ? BindingAdmission.Admitted
                : BindingAdmission.Reject("Unsupported alignment.");
        }
    }

    private sealed class TemplatePolicy : IBindingTemplatePolicy
    {
        public BindingAdmission Admit(BindingTemplateDescriptor template)
        {
            return template.IsClosed
                ? BindingAdmission.Admitted
                : BindingAdmission.Reject("Only closed templates are supported by this fixture.");
        }
    }

    private sealed class SourceStemEmitter : IBindingEmitterPrimitive
    {
        public string Name
        {
            get
            {
                return "source-stem";
            }
        }

        public string Emit(string value)
        {
            return value.Replace("::", "_", StringComparison.Ordinal);
        }
    }

    private sealed class MutableDescription(string text) : IDescriptionItem, IRootDescriptionItem
    {
        public int Calls { get; private set; }

        public string Text { get; set; } = text;

        public void ToDescription(ref SourceBuilder builder)
        {
            Calls++;
            builder.Append(Text);
        }
    }
}