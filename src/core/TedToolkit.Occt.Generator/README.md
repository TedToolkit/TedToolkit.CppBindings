# TedToolkit.Occt.Generator

`TedToolkit.Occt.Generator` parses selected OCCT C++ declarations from vcpkg into one shared model
graph and currently generates C# type shapes plus one internal C++ invocation source per record.

> ⚠️ 当前项目面向生成器开发和验证，尚不是已经验证发布的 NuGet 消费入口。

The accepted target architecture is defined by
[GEN-02 and GEN-03](../../../docs/principles/README.md) and the
[generated binding architecture](../../../docs/architecture/generated-binding-system.md): every
supported C++ object becomes an exact unmanaged `LayoutKind.Sequential` struct with generated
typed, padding, and aligned opaque physical segments; interfaces express inheritance without a
managed `BaseType`; instance behavior is emitted as extensions; native lifetime belongs to
separate reference-type owners. The explicit-offset implementation described below is current
migration state, not the target contract.

[GEN-01](../../../docs/principles/README.md#gen-01-generate-every-binding-layer-from-one-semantic-source)
fixes the stage direction: parse and normalize a complete Model, emit C# and C++ from that Model,
and only then optionally compile the emitted native project. The Model schema may evolve, but
emitters must not reparse native input or consume each other's output.

## 使用入口

生成器通过 ModularPipelines 注册：

```csharp
var outputFolder = new DirectoryInfo("output/generated");

var pipeline = await Pipeline.CreateBuilder()
    .AddOcctGenerators(new GenerationOptions
    {
        DeclOptions =
        [
            new DeclOptions("Geom2d_BSplineCurve"),
        ],
        CSharpFolder = outputFolder.CreateSubdirectory("csharp"),
        CppFolder = outputFolder.CreateSubdirectory("cpp"),
    })
    .BuildAsync();

await pipeline.RunAsync();
```

完整开发样例位于 [`tests/TedToolkit.Occt.Console`](../../../tests/TedToolkit.Occt.Console)。

## Inputs and configuration

The generator requires:

1. `VCPKG_ROOT` pointing to a valid vcpkg root.
2. `opencascade` installed for the selected triplet.
3. At least one `DeclOptions` entry selecting an OCCT record.
4. C# and C++ output directories.

`DeclOptions.FileName` is both the top-level record name and its public header stem. It must match `[A-Za-z_][A-Za-z0-9_]*`, and the exact header `<FileName>.hxx` must exist below `installed/<triplet>/include/opencascade`. Duplicate names are coalesced in ordinal order. Direct enum targets, namespaced records, and records whose declaration and header stems differ are not supported.

The primary `GenerationOptions` values are:

| Option | Purpose |
| --- | --- |
| `DeclOptions` | Entry records such as `Geom2d_BSplineCurve`. |
| `CSharpFolder` | Generated C# source directory. |
| `CppFolder` | Generated per-record C++ invocation sources. |
| `NativeLibraryBaseName` | Native artifact basename used by the generated CMake project and managed loader. |
| `Triplet` | Explicit vcpkg triplet; automatically selected when omitted. |
| `CppVersion` | C++ standard passed to Clang and CMake; defaults to 17. |
| `CommandLineArgs` | Additional Clang parse arguments. |
| `FieldTypeToGenerate` | Filters fields from their Clang `FieldDecl`. |
| `IsInternal` | Selects internal visibility for generated C# types. |
| `GetFieldOffsetByRunning` | Exposed layout option; the current model still reads size and offsets from libclang. |

`CSharpNamespace` defaults to `TedToolkit.Occt` and applies to every generated C# artifact without
changing package, assembly, canonical C++, or native export identity.

## Generation pipeline

The accepted pipeline boundary is:

```text
configured native inputs and options
  -> parse and normalize complete Model
    -> GenerateCSharpModule
    -> GenerateCppModule and native build description
      -> optional native compilation
```

C# and C++ emission must be independently runnable from the completed Model. Native compilation is
optional for source-generation use and mandatory for the ready-to-use
`TedToolkit.Occt.Windows` package.

The current ModularPipelines dependency graph is:

Clean and Parse are independent prerequisites of both generators:

```text
CleanGenerationOutputModule ───┬───────────────┐
                               │               │
ParseModule ───────────────────┤               │
                               ▼               ▼
                    GenerateCSharpModule  GenerateCppModule
```

| Module | Responsibility |
| --- | --- |
| `CleanGenerationOutputModule` | Removes previous C#, C++, CMake build, and binary output. |
| `ParseModule` | Parses only selected public headers, validates diagnostics and every requested definition, then commits the complete target set to the shared model. |
| `GenerateCSharpModule` | Writes `.g.cs` files for records and enums from the shared model. |
| `GenerateCppModule` | Writes exactly one `<CSharpTypeName>.cpp` invocation source for every parsed `RecordModel` from the shared model, rejecting case-insensitive filename collisions before writing. |

If Clean or Parse fails, neither generator starts. Clang Error and Fatal diagnostics fail Parse; Warning diagnostics remain non-fatal and are logged literally. Parse resolves every requested record definition before adding any target to the shared model, so an unresolved mixed target set cannot expose a partial model.

## 1. Discover OCCT through vcpkg

`VcpkgDefaultTripletResolver` scans `VCPKG_ROOT/installed` for triplets containing `include/opencascade` and prefers the dynamic triplet matching the current platform and process architecture.

`VcpkgEnvironment` supplies the vcpkg root, triplet include directory, OCCT include directory, and relay translation-unit content. The relay contains only the distinct selected `<FileName>.hxx` headers; each selected header supplies its own transitive include graph.

## 2. Build the model from the Clang AST

`ParseModule` creates a translation unit with the configured C++ standard, vcpkg include directories, and caller-provided arguments. It then matches each `DeclOptions.FileName` to a top-level record definition using ordinal comparison.

`RecordModelManager` 将 Clang 声明转换为以下模型：

| 模型 | 保存的信息 |
| --- | --- |
| `RecordModel` | 类型、基类、大小、字段、方法、源头文件、抽象性和生命周期分类。 |
| `MethodModel` | 方法种类、名称、const/noexcept/static、参数、返回类型和文档。 |
| `FieldModel` | 字段名、原生偏移、投影类型和文档。 |
| `TypeModel` | C++ ABI 名称、C# P/Invoke 类型、C# Public 类型和所需头文件。 |
| `EnumModel` | 枚举名称、底层类型、成员值和文档。 |

模型管理器会递归加入基类、字段类型、参数类型、返回类型和枚举。相同 Clang canonical declaration 只建立一次，以避免依赖环和重复输出。

方法进入模型前会过滤：

- 无法获得完整定义的参数或返回类型；
- 非 public、deleted、unavailable 或 invalid 的方法；
- 不受支持的 `operator()`、赋值运算符和 `new/delete` 运算符；
- 抽象类型的构造/析构函数；
- 需要按值复制、但复制构造不可用的参数。

`opencascade::handle<T>` and `occ::handle<T>` specializations are unwrapped to `T` so the real OCCT
type can be generated recursively. A field or method whose handle target is only forward declared
does not enter the current model.

## 3. 投影跨语言类型

The model currently carries several type spellings, but a supported C++ object has only one managed
physical identity in the accepted architecture:

```text
Clang C++ 类型
    ├── CppTypeName          → source C++ semantics and diagnostics
    ├── NativePhysicalLayout → exact size, alignment, packing, and physical segments
    ├── C pointer transport  → C11-compatible pointer to the same native storage
    └── CSharpPublicType     → exact-layout struct plus separate owner/extension API
```

The current Resolver still derives the legacy spelling properties. Special transport rules may
adapt non-object call data; for example, the built-in UTF-8 rule projects `const char*` input as:

- C++：原始 `const char*`；
- P/Invoke：`byte*`；
- Public：`ReadOnlySpan<byte>`。

Object storage cannot use a semantic transport in place of its C++ representation. The migration
will derive its C# fields and padding from the native physical model, while operation parameters
may still use approved public conveniences that do not change object storage identity.

## 4. Generate per-record C++ invocation sources

`CppGenerator` consumes one `RecordModel` directly. It derives sorted required includes and emits
constructor, destructor, normal/static method, operator, and conversion invocation helpers from the
record's ordered methods. `GenerateCppModule` deterministically writes one source per record; it does
not use a handwritten operation catalog or a parallel ABI declaration graph.

These helpers deliberately have C++ linkage. They are not yet the public C11 transport boundary and
cannot by themselves be called safely from generated C#. The active migration must still add the
validated transport/conversion projection, matching C declarations and exports, manifest,
fingerprint, CMake description, and managed imports. The independent ABI-v1 fixture remains only as
replacement evidence until that complete generated boundary is proved.

## 5. Current C# type generation

记录进入当前结构生成分支时，C# 生成器负责：

- 根据原生 size 生成显式布局类型；
- 根据字段 offset 生成 `[FieldOffset]`；
- 使用 `NativeTypeNameAttribute` 保留原生类型名称；
- 投影方法、参数、返回类型和 XML 文档；
- 根据 OCCT 基类生成接口继承；
- 生成枚举及其底层值。

当前实现尚未生成完整的 `DllImport`/`LibraryImport` 声明，也没有把公共方法体连接到 C++ 导出函数。因此这部分目前表达的是托管 API 形状，而不是可以独立调用原生 DLL 的完整绑定。

当前代码只在 `recordDecl.IsAbstract` 为 true 时进入结构生成分支；非抽象记录目前只尝试生成继承接口。这是当前实现事实，不是最终类型策略。

The accepted replacement must instead generate every supported object as an unmanaged sequential
struct, calculate explicit private padding and aligned opaque storage from compiler layout data,
emit no `FieldOffsetAttribute` or managed `BaseType`, prove managed/native size and alignment, and
fail closed when the pinned CLR cannot reproduce a native layout. C++ templates use one C# generic
struct only when one physical graph proves every registered closed specialization. A template that
uses `void` as a dependent implementation placeholder is not emitted as an open generic type; only
its usable closed specializations are emitted, using underscore-expanded fixed type names.

## 输出目录

以示例配置为例：

```text
output/generated/
├── csharp/
│   ├── <Type>.g.cs
│   └── <Enum>.g.cs
└── cpp/
    ├── <TypeA>.cpp
    └── <TypeB>.cpp
```

Each filename is derived from `RecordModel.Type.CSharpTypeName`; distinct records that would map to
the same case-insensitive path fail before materialization.

## Known limitations

The generator fails closed when it cannot preserve native layout, ownership, or lifetime semantics.
Unsupported input is handled at the narrowest safe boundary: a broken header excludes that header
and its transitive dependants, while an unsupported method signature excludes only that method.
For the Windows package, the delivered OCCT DLLs are authoritative: a function declared by a
header but absent from those DLLs is unsupported by that package. The generator removes only the
corresponding binding operation and preserves its declaring type and every other supported member.

### Excluded headers

All-public-header generation excludes:

- A header whose quoted OCCT include dependency is absent from the installed package, plus every
  public header that transitively depends on it. Known missing installed files currently include
  `BOPDS_ListOfPaveBlock.hxx` and `GeomBndLib_InfiniteHelpers.pxx`.
- `MathLin_Jacobi.hxx`, because the installed declaration refers to the nonexistent
  `EigenResult.NbIterations` member and cannot form a valid translation unit.
- `OpenGl_GLESExtensions.hxx`, because its GLES declarations conflict with the desktop OpenGL
  declarations in the all-header translation unit.

The complete, deterministic header exclusion set is written to
`output/generated/unsupported-headers.txt` on every all-public-header run. The generator does not
create replacement OCCT headers or patch the installed package.

### Excluded declarations and methods

- Private, protected, unnamed, incomplete, deleted, unavailable, and invalid declarations are not
  emitted as public binding types or methods. Public nested types remain supported.
- C++ templates, standard-library types, smart pointers, and stream-related APIs are support
  candidates. The generator must first attempt generic modeling and closed-specialization proof;
  their category alone is not a valid exclusion reason. A concrete operation is excluded only when
  its layout, transport, invocation, or lifetime semantics cannot be preserved.
- Shared stream types and `NCollection_Handle<T>` follow that same admission path. They are not
  denylisted. Generate every closed specialization whose native layout, invocation, borrowing, and
  ownership behavior can be preserved. In particular, an `NCollection_Handle<T>` adapter must
  retain and release its actual private `Standard_Transient` owner rather than substituting the
  pointer returned by `get()`. If one concrete specialization or operation cannot satisfy that
  proof, report and exclude only that boundary.
- `IntPolyh_Array<IntPolyh_Edge>::Dump()` and `IntPolyh_Array<IntPolyh_Triangle>::Dump()` are
  excluded because their template bodies call the element `Dump()` without its required integer
  argument. Both concrete array types and every other callable member remain generated.
- `NCollection_CellFilter<BRepExtrema_VertexInspector>::Remove(...)` is excluded because both
  overloads require an `IsEqual` operation that `BRepExtrema_VertexInspector` does not provide.
  The concrete cell-filter type and its other callable members remain generated.
- Copy assignment, `SetValue(...)`, and sequence-to-sequence append, prepend, and insertion are
  excluded from `NCollection_Sequence<CSLib_Class2d>` because `CSLib_Class2d` deletes copying.
  Move construction, rvalue element insertion, and members that do not copy elements remain
  generated.
- `NCollection_Vec3<unsigned long long>::cwiseAbs()` is excluded because the installed OCCT body
  calls an ambiguous MSVC `abs` overload. The concrete vector type and its other operations remain
  generated.
- Functions listed in `Resources/UnsupportedNativeExports.txt` are excluded because a complete
  Windows x64 native link proved that the delivered OCCT binaries do not provide their required
  symbols. This list uses final native export names so the exclusion remains member-specific and
  auditable; it must be regenerated from linker evidence when the packaged OCCT binaries change.

The following callable categories also remain unsupported:

- `operator()`, assignment operators, and custom `operator new/delete`;
- abstract-type constructors and destructors;
- constructors of nontrivial types that have no callable native destructor; such values cannot be
  returned as `Owned<T>` safely;
- parameters requiring an unavailable native copy constructor;
- handle targets that remain forward-declared and have no complete definition;
- builtin Clang types for which no stable C# ABI projection exists.

Only the proved Windows x64 OCCT installation and matching MSVC ABI are currently targeted. The
repository has no vcpkg manifest, so builds depend on a machine-level OCCT installation.

## 开发与验证

The verified native boundary baseline is Windows x64 with CMake 3.28 or newer, Ninja, `clang-cl` targeting the MSVC ABI, and vcpkg `x64-windows` with OCCT 8.0.1. Supply the following environment and ensure Ninja and `clang-cl` are available on `PATH`:

This matrix is the initial target of the `TedToolkit.Occt.Windows` package. The Windows
family name does not imply `win-arm64` or another compiler ABI; each additional matrix requires
independent exact-layout proof and package architecture review.

```powershell
$env:VCPKG_ROOT = 'C:\vcpkg'
$env:CMAKE_GENERATOR = 'Ninja'
$env:CXX = 'clang-cl'
```

This is the legacy boundary verification baseline, not an exclusive consumer toolchain requirement.
It remains available until the generated unversioned C11/native/managed boundary replaces it.

从仓库根目录运行开发样例：

```powershell
$env:VCPKG_ROOT = 'C:\vcpkg'
dotnet run --project tests/TedToolkit.Occt.Console/TedToolkit.Occt.Console.csproj -c Release
```

This command verifies the development path from real-header parsing through C# and per-type C++
source materialization. It does not verify the unimplemented C11 exports or final DLL. See the
[root README](../../../README.md#开发) for repository build and test conventions.

## 相关文档

- [仓库概览](../../../README.md)
- [Binding model internals](Models/README.md)
- [Repository design principles](../../../docs/principles/README.md)
- [Generated binding architecture](../../../docs/architecture/generated-binding-system.md)
- [Unversioned binding migration](../../../docs/changes/generate-unversioned-model-driven-bindings/change.md)
- [Runtime 契约](../TedToolkit.Occt.Runtime/README.md)
