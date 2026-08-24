# TedToolkit.Occt

TedToolkit.Occt 是一个面向 .NET 的 OCCT 绑定代码生成项目：它从 vcpkg 安装的 Open CASCADE Technology（OCCT）头文件中解析用户选择的 C++ 类型，并生成配套的 C++ ABI 包装代码与 C# 类型代码。

> ⚠️ 项目目前处于开发阶段。核心解析与 C++ 生成链路已经建立，但尚未形成可直接消费的完整 C# 绑定包；请先阅读[当前实现边界](#当前实现边界)。

## 项目解决什么问题

现有 OCCT .NET 封装通常需要在易用性、原生语义、包体积和平台覆盖之间取舍。本项目尝试采用代码生成方式解决这些问题：

- 只从指定的 OCCT 类型开始生成，并递归加入实际依赖，避免无条件包装整个 OCCT。
- 保留 OCCT 的值类型、继承和 `Standard_Transient` 生命周期语义。
- 通过 `extern "C"` 建立稳定的 C ABI，避免 C# 直接调用 C++ ABI。
- 分离 ABI 类型与公共 C# 类型，使内存布局正确性和 C# 易用性可以分别演进。
- 在原生边界捕获 OCCT/C++ 异常，再转换成 .NET 异常。

## 工作原理

```text
GenerationOptions / DeclOptions
              │
              ▼
读取 VCPKG_ROOT 下的 OCCT 头文件和目标 triplet
              │
              ▼
       ClangSharp / libclang 解析 C++ AST
              │
              ▼
构造 RecordModel、MethodModel、FieldModel、TypeModel
              │
              ├───────────────┐
              ▼               ▼
生成 ABI-major-1 C11       生成 C# 类型、字段、
canonical header           接口和公共 API 形状
              │               │
              ▼               │
versioned native adapters      │
              └───────┬───────┘
                      ▼
           Runtime 管理指针、生命周期与异常
```

### 1. 从 vcpkg 获取真实 OCCT 环境

生成器读取 `VCPKG_ROOT`，在 `installed/<triplet>/include/opencascade` 中查找 OCCT 头文件。未显式指定 `GenerationOptions.Triplet` 时，它会从已安装 OCCT 的 triplet 中选择与当前操作系统和进程架构最匹配的一项。

当前环境不是由仓库清单锁定的：仓库尚无 `vcpkg.json`，因此生成结果取决于本机 vcpkg 安装。

### 2. 解析 C++ AST 并建立依赖图

ClangSharp/libclang 将头文件解析为 C++ AST。生成器先寻找 `DeclOptions` 指定的声明，再递归分析：

Each `DeclOptions.FileName` selects the exact public header `<FileName>.hxx`; unrelated OCCT headers are not added to the relay translation unit.

- 基类；
- 字段类型；
- 方法参数和返回类型；
- 枚举；
- `opencascade::handle<T>` 指向的实际记录类型；
- 生成 C++ 签名需要包含的头文件。

记录模型还保存对象大小、字段偏移、抽象性、是否含有虚函数，以及是否继承 `Standard_Transient`。这些信息决定生成类型的内存布局和生命周期策略。

### 3. 生成两组代码

| 输出 | 责任 |
| --- | --- |
| C ABI header | Generates the canonical `ted_toolkit_occt_v1.h` from explicit semantic mappings; incomplete operations are omitted before naming or emission. |
| C# 代码 | 根据原生大小和字段偏移生成托管类型形状，区分 P/Invoke 类型与公共 API 类型，并投影继承接口、枚举和 XML 文档。 |

The production pipeline materializes the canonical header and versioned native adapter project.
The root CMake presets build it and run a real C11 consumer; see
[C interoperability ABI major 1](docs/interop-abi-v1.md).

更详细的生成流程见 [TedToolkit.Occt.Generator](src/core/TedToolkit.Occt.Generator/README.md)，生命周期和异常模型见 [TedToolkit.Occt.Runtime](src/core/TedToolkit.Occt.Runtime/README.md)。

## 🚀 运行开发示例

### 前置条件

- .NET SDK 10
- CMake 3.28 或更高版本
- 可用的 C++17 编译工具链；Windows 当前使用 Visual C++
- vcpkg
- 已安装的 `opencascade` triplet
- 指向 vcpkg 根目录的 `VCPKG_ROOT` 环境变量

先确认 OCCT 安装：

```powershell
& "$env:VCPKG_ROOT\vcpkg.exe" list opencascade
```

示例程序当前选择 `Geom2d_BSplineCurve`，输出到 `output/generated`：

```powershell
dotnet run --project tests/TedToolkit.Occt.Console/TedToolkit.Occt.Console.csproj -c Release
```

预期输出结构：

```text
output/generated/
├── csharp/                         # 生成的 C# 文件
└── cpp/
    ├── CMakeLists.txt               # versioned native adapter project
    ├── ted_toolkit_occt_v1.h       # canonical ABI-major-1 C header
    ├── ted_toolkit_occt_v1.cpp     # OCCT adapters
    └── ted_toolkit_occt_v1_test.h  # compiled only by the boundary proof
```

> ⚠️ This command remains a development entry point rather than a verified release example. Target-scoped parsing and generator ordering are enforced, but downstream model projection or native compilation can still reject unsupported OCCT surface.

## 组件

| 组件 | 责任 | 文档 |
| --- | --- | --- |
| `TedToolkit.Occt.Generator` | 读取 vcpkg/OCCT、解析 AST、建立模型并生成两组代码 | [README](src/core/TedToolkit.Occt.Generator/README.md) |
| `TedToolkit.Occt.Runtime` | 提供原生句柄、借用视图、异常桥和生成类型依赖的基础契约 | [README](src/core/TedToolkit.Occt.Runtime/README.md) |
| `TedToolkit.Occt.Analyzer` | 从已安装 OCCT 头文件生成可选择的头文件类型枚举 | `src/tools/TedToolkit.Occt.Analyzer` |
| `TedToolkit.Occt.Console` | 运行 `Geom2d_BSplineCurve` 生成流程的开发样例 | `tests/TedToolkit.Occt.Console` |
| `Build` | 仓库构建管线，并在准备阶段生成 triplet 友元程序集声明 | `Build` |

## 当前实现边界

- `DeclOptions.FileName` must identify both a top-level OCCT record and its exact public header stem; direct enum targets, namespaced targets, and declaration/header name mismatches are unsupported.
- Recursive managed-model discovery can still encounter STL and compiler implementation types,
  but the canonical C ABI rejects them unless every projection layer has an approved mapping.
- A minimal managed fixture proves P/Invoke layouts and version gating; generated public invocation
  bodies and production lifetime abstractions remain incomplete.
- C# 生成器已经生成类型、字段、接口和方法形状，但实际 P/Invoke 声明与公共方法调用体尚未接通。
- 当前记录类型的结构生成分支只在 `recordDecl.IsAbstract` 为 true 时执行；非抽象记录目前只生成继承接口，类型生成条件仍需调整。
- 仓库没有 vcpkg manifest/baseline，OCCT 版本仍由本机全局安装决定。
- 当前没有证据表明 NuGet 包已经发布；不要把项目文件中的打包配置视为可用发布渠道。

## 开发

还原和构建解决方案：

```powershell
dotnet restore TedToolkit.Occt.slnx
dotnet build TedToolkit.Occt.slnx -c Release --no-restore
```

仓库使用 TUnit 和 Microsoft Testing Platform。测试项目已构建时，优先使用 `dotnet run`：

```powershell
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release --no-build -- --report-trx
```

The Generator test project uses the same executable test entry point:

```powershell
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
```

Set `VCPKG_ROOT` to a usable vcpkg installation to run the real OCCT boundary test. When it is unset or the selected OCCT header is unavailable, only that environment-dependent test is reported as skipped.

## 许可证

本项目使用 LGPL-3.0 许可，详见 [COPYING](COPYING) 和 [COPYING.LESSER](COPYING.LESSER)。OCCT 及其他依赖分别遵循其自身许可。
