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

## 输入与配置

生成器需要：

1. `VCPKG_ROOT` 指向一个有效的 vcpkg 根目录。
2. 对应 triplet 下已经安装 `opencascade`。
3. `DeclOptions` 给出希望生成的 OCCT 声明名。
4. C# 与 C++ 输出目录。

`GenerationOptions` 的主要选项如下：

| 选项 | 作用 |
| --- | --- |
| `DeclOptions` | 生成入口声明列表，例如 `Geom2d_BSplineCurve`。 |
| `CSharpFolder` | 生成的 C# 源文件目录。 |
| `CppFolder` | C++ wrapper、CMake 工程和原生库目录。 |
| `Triplet` | 显式指定 vcpkg triplet；留空时自动选择。 |
| `CppVersion` | 传给 Clang 和 CMake 的 C++ 标准，默认 17。 |
| `CommandLineArgs` | 追加到 Clang 解析的参数。 |
| `FieldTypeToGenerate` | 根据 Clang `FieldDecl` 过滤字段。 |
| `IsInternal` | 控制生成的 C# 类型是否使用 internal 可见性。 |
| `GetFieldOffsetByRunning` | 已公开的布局选项；当前记录模型仍直接读取 libclang 的 size/offset。 |

## 生成管线

逻辑上，生成过程分为四步：

```text
CleanGenerationOutputModule
            │
            ▼
       ParseModule
        ┌───┴───┐
        ▼       ▼
GenerateC#   GenerateC++
```

| 模块 | 当前责任 |
| --- | --- |
| `CleanGenerationOutputModule` | 清理上一次生成的 C#、C++、CMake 构建和二进制输出。 |
| `ParseModule` | 聚合 OCCT 头文件，调用 libclang，记录诊断，并把目标声明加入模型管理器。 |
| `GenerateCSharpModule` | 为记录和枚举写入 `.g.cs` 文件。 |
| `GenerateCppModule` | 生成 C++ wrapper、复制异常桥头文件、生成 CMake 工程并编译原生库。 |

当前依赖声明与上面的逻辑顺序尚不完全一致：`GenerateCppModule` 依赖 Parse 和 Clean，但 `GenerateCSharpModule` 目前只依赖 Clean。因此 C# 模块可能先于解析执行，产生空目录。

## 1. 从 vcpkg 发现 OCCT

`VcpkgDefaultTripletResolver` 扫描 `VCPKG_ROOT/installed`，查找存在 `include/opencascade` 的 triplet，并优先选择当前平台和架构对应的动态 triplet。

`VcpkgEnvironment` 随后提供：

- vcpkg 根目录；
- triplet 公共 include 目录；
- OCCT include 目录；
- 用于 Clang 解析的聚合头文件文本。

当前聚合文本会包含 OCCT 目录下所有未被标记弃用的 `.hxx` 文件。这是现有实现，不是理想边界：更稳妥的方向是只包含 `DeclOptions` 对应的公共头文件，让 C++ 自身的 include 图解析依赖。

## 2. 从 Clang AST 建立模型

`ParseModule` 使用 C++17、vcpkg include 目录和调用方追加参数建立 Translation Unit，然后从顶层记录中匹配 `DeclOptions.FileName`。

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

## 诊断与已知限制

- Clang Error/Fatal 当前只记录日志，`ParseModule` 仍返回成功。
- 聚合全部 `.hxx` 可能触发头文件顺序问题或引用 vcpkg 未安装的私有 `.pxx`。
- 类型递归仍可能进入 STL 和编译器内部实现类型；这些类型尚未全部建模为边界适配器。
- C# 模块缺少 Parse 依赖。
- C# 记录类型的结构生成条件仍需修正，非抽象记录当前不会生成 struct。
- P/Invoke 调用层尚未完成。
- 仓库没有 vcpkg manifest，构建依赖机器级 OCCT 安装。
- `GetFieldOffsetByRunning` 尚未改变当前从 libclang 读取布局的实现。

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
