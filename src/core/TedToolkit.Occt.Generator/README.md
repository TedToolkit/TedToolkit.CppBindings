# TedToolkit.Occt.Generator

`TedToolkit.Occt.Generator` 将 vcpkg 安装的 OCCT C++ 声明解析成内部模型，并为用户选择的类型生成 C++ ABI 包装代码与 C# 类型代码。

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
| `CppFolder` | C++ wrappers, CMake project, and native library directory. |
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
| `GenerateCppModule` | Generates C++ wrappers, copies the exception bridge header, creates the CMake project, and compiles the native library. |

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

`opencascade::handle<T>` 和 `occ::handle<T>` 模板特化会展开到其目标 `T`，以便递归生成真实 OCCT 类型。

## 3. 投影跨语言类型

同一个 C++ 类型在不同边界具有不同表达：

```text
Clang C++ 类型
    ├── CppTypeName          → C++ wrapper 签名
    ├── CSharpPInvokeType    → ABI、字段布局和原生调用签名
    └── CSharpPublicType     → 面向调用方的 C# API
```

默认 Resolver 从 canonical Clang 类型推导三个投影。特殊规则可以覆盖结果；当前内置规则把 `const char*` 一类 UTF-8 输入投影为：

- C++：原始 `const char*`；
- P/Invoke：`byte*`；
- Public：`ReadOnlySpan<byte>`。

字段优先使用 `CSharpPInvokeType` 保持布局；方法参数和返回值面向调用方时使用 `CSharpPublicType`。

## 4. 生成 C++ ABI 包装

每个记录生成一个 `.cpp`。生成代码：

- 包含记录本身及签名依赖的头文件；
- 把构造函数、成员函数、转换和部分运算符变成 `extern "C"` 导出函数；
- 把重载签名编码进导出名称；
- 使用显式 `self` 参数表示 C++ 实例；
- 使用输出指针承载非 void 返回值；
- 对右值引用调用参数使用 `std::move`；
- 根据类型语义生成构造和释放操作。

可能抛异常的方法使用 `CSHARP_WRAPPER_TRY`。嵌入的 `csharp_interop.h` 捕获 `Standard_Failure`、`std::exception` 和未知异常，并返回只包含堆分配 UTF-8 字符串的 `interop_error`。`noexcept` 方法使用不返回错误结构的 `CSHARP_WRAPPER`。

所有生成源文件被写入临时 CMake 工程：

```cmake
find_package(OpenCASCADE CONFIG REQUIRED)
target_include_directories(... ${OpenCASCADE_INCLUDE_DIR})
target_link_libraries(... ${OpenCASCADE_LIBRARIES})
```

CMake 使用 vcpkg toolchain 和选定 triplet，最终目标库名为 `ted_toolkit_occt`。

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
└── cpp/ted_toolkit_occt/
    ├── src/
    │   ├── csharp_interop.h
    │   ├── CMakeLists.txt
    │   └── <Type>.cpp
    ├── build/
    └── bin/
```

## Known limitations

- Recursive type discovery may still enter STL and compiler implementation types that are not yet modeled as boundary adapters.
- The C# record-generation condition still needs correction; non-abstract records currently do not generate structs.
- The P/Invoke invocation layer is incomplete.
- The repository has no vcpkg manifest, so builds depend on a machine-level OCCT installation.
- `GetFieldOffsetByRunning` does not yet replace the current libclang-based layout lookup.

## 开发与验证

从仓库根目录运行开发样例：

```powershell
$env:VCPKG_ROOT = 'C:\vcpkg'
dotnet run --project tests/TedToolkit.Occt.Console/TedToolkit.Occt.Console.csproj -c Release
```

该命令目前用于暴露完整链路问题，不应被视为绿色验收命令。仓库级构建与测试约定见[根 README](../../../README.md#开发)。

## 相关文档

- [仓库概览](../../../README.md)
- [Runtime 契约](../TedToolkit.Occt.Runtime/README.md)
