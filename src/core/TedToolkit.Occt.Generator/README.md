# TedToolkit.Occt.Generator

`TedToolkit.Occt.Generator` parses selected OCCT C++ declarations from vcpkg into one shared model
graph and currently generates C# type shapes plus one internal C++ invocation source per record.

> ⚠️ 当前项目面向生成器开发和验证，尚不是已经验证发布的 NuGet 消费入口。

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
| `NativeLibraryBaseName` | Reserved portable native artifact basename for the incomplete generated native-project stage. |
| `Triplet` | Explicit vcpkg triplet; automatically selected when omitted. |
| `CppVersion` | C++ standard passed to Clang and CMake; defaults to 17. |
| `CommandLineArgs` | Additional Clang parse arguments. |
| `FieldTypeToGenerate` | Filters fields from their Clang `FieldDecl`. |
| `IsInternal` | Selects internal visibility for generated C# types. |
| `GetFieldOffsetByRunning` | Exposed layout option; the current model still reads size and offsets from libclang. |

## Generation pipeline

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
| `GenerateCSharpModule` | Writes `.g.cs` files for records and enums. |
| `GenerateCppModule` | Writes exactly one `<CSharpTypeName>.cpp` invocation source for every parsed `RecordModel`, rejecting case-insensitive filename collisions before writing. |

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

同一个 C++ 类型在不同边界具有不同表达：

```text
Clang C++ 类型
    ├── CppTypeName          → 源 C++ 语义与诊断
    ├── CSharpPInvokeType    → ABI、字段布局和原生调用签名
    └── CSharpPublicType     → 面向调用方的 C# API
```

默认 Resolver 从 canonical Clang 类型推导三个投影。特殊规则可以覆盖结果；当前内置规则把 `const char*` 一类 UTF-8 输入投影为：

- C++：原始 `const char*`；
- P/Invoke：`byte*`；
- Public：`ReadOnlySpan<byte>`。

字段优先使用 `CSharpPInvokeType` 保持布局；方法参数和返回值面向调用方时使用 `CSharpPublicType`。

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

## 5. 生成 C# 类型

记录进入当前结构生成分支时，C# 生成器负责：

- 根据原生 size 生成显式布局类型；
- 根据字段 offset 生成 `[FieldOffset]`；
- 使用 `NativeTypeNameAttribute` 保留原生类型名称；
- 投影方法、参数、返回类型和 XML 文档；
- 根据 OCCT 基类生成接口继承；
- 生成枚举及其底层值。

当前实现尚未生成完整的 `DllImport`/`LibraryImport` 声明，也没有把公共方法体连接到 C++ 导出函数。因此这部分目前表达的是托管 API 形状，而不是可以独立调用原生 DLL 的完整绑定。

当前代码只在 `recordDecl.IsAbstract` 为 true 时进入结构生成分支；非抽象记录目前只尝试生成继承接口。这是当前实现事实，不是最终类型策略。

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

- Recursive managed-model discovery may still encounter STL and compiler implementation types;
  incomplete declarations are excluded, while supported interop projections still need explicit
  transport and lifetime rules.
- Generated C11 declarations/exports, CMake/native-library materialization, managed imports, and
  public invocation bodies remain incomplete.
- The minimal ABI-major-1 native/PInvoke fixture is migration proof only and is not a production
  generation input.
- The C# record-generation condition still needs correction; non-abstract records currently do not generate structs.
- The P/Invoke invocation layer is incomplete.
- The repository has no vcpkg manifest, so builds depend on a machine-level OCCT installation.
- `GetFieldOffsetByRunning` does not yet replace the current libclang-based layout lookup.

## 开发与验证

The verified native boundary baseline is Windows x64 with CMake 3.28 or newer, Ninja, `clang-cl` targeting the MSVC ABI, and vcpkg `x64-windows` with OCCT 8.0.1. Supply the following environment and ensure Ninja and `clang-cl` are available on `PATH`:

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
- [Unversioned binding migration](../../../docs/changes/generate-unversioned-model-driven-bindings/change.md)
- [Runtime 契约](../TedToolkit.Occt.Runtime/README.md)
