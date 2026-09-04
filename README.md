# TedToolkit.Occt

TedToolkit.Occt 是一个面向 .NET 的 OCCT 绑定代码生成项目：它从 vcpkg 安装的 Open CASCADE Technology（OCCT）头文件中解析用户选择的 C++ 类型，并生成配套的 C++ ABI 包装代码与 C# 类型代码。

`TedToolkit.Occt` 是仓库和产品家族名称。计划中的首个即用绑定 package 与 managed
assembly 名为 `TedToolkit.Occt.Windows`，当前只面向经过证明的 `win-x64` 布局矩阵；生成
类型的默认 C# namespace 仍为 `TedToolkit.Occt`。

> ⚠️ 项目目前处于开发阶段。核心解析与 C++ 生成链路已经建立，但尚未形成可直接消费的完整 C# 绑定包；请先阅读[当前实现边界](#当前实现边界)。

## 项目解决什么问题

现有 OCCT .NET 封装通常需要在易用性、原生语义、包体积和平台覆盖之间取舍。本项目尝试采用代码生成方式解决这些问题：

- 只从指定的 OCCT 类型开始生成，并递归加入实际依赖，避免无条件包装整个 OCCT。
- 保留 OCCT 的值类型、继承和 `Standard_Transient` 生命周期语义。
- The target boundary keeps exported declarations C11-compatible while passing pointers to
  compiler-matched native object storage; that generated export layer is still under migration.
- Every supported C++ object has one exact-layout unmanaged C# struct generated from native size,
  alignment, fields, hidden storage, and padding. Managed and native layout cannot evolve
  independently inside one supported artifact set.
- C++ inheritance is projected through C# interfaces, while instance behavior and native lifetime
  remain in extension methods and separate reference-type owners.
- `Handle<T>` stores only the address of a C++-owned transient object and releases one intrusive
  reference. `Owned<T>` directly contains a non-transient RAII object in address-stable managed
  storage; disposal calls its C++ destructor but never intrusive `Release` or native storage free,
  and the GC reclaims the backing memory.
- 在原生边界捕获 OCCT/C++ 异常，再转换成 .NET 异常。

## 工作原理

接受的生成架构固定为 Model-first：先完成解析和规范化，再由同一个 Model 分别生成 C# 与
C++ 源码；源码生成完成后才可以选择编译 native DLL。即用包会启用并验证这个编译阶段，普通
Generator 调用可以停在源码输出。Model 的内容可以随受支持语义演进，但 emitter 不得绕过它。

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
      完成 normalized Model
              │
              ├───────────────┐
              ▼               ▼
generated C++ project      generated C# binding set
              │               │
              └───────┬───────┘
                      ▼
           complete generated source set
                      │
                      ▼ optional
             compile native DLL
                      │
                      ▼ package only
       assemble and verify ready-to-use package
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

当前记录模型保存对象大小、字段偏移、抽象性、是否含有虚函数，以及是否继承
`Standard_Transient`。接受的目标还要求编译器对齐、packing、完整 base/hidden physical
segments、模板特化和 construction/destruction 语义；这些信息共同决定精确布局和独立生命周期策略。

### 3. 生成两组代码

| 输出 | 责任 |
| --- | --- |
| C++ source | Traverses `RecordModel` directly and emits one internal `.cpp` per parsed record with required headers and real OCCT invocation expressions. |
| C# 代码 | 当前根据原生大小和字段偏移生成类型形状；目标是精确的 Sequential struct、padding/opaque storage、继承接口、extension API、泛型特化和所有权 glue。 |

生成的 C# 根命名空间由经过验证的 `CSharpNamespace` 选项控制，包默认使用
`TedToolkit.Occt`。自定义命名空间只改变托管 API 身份，不改变 C++ canonical identity、
native export 命名或原生布局身份。

Package/assembly 名称与 namespace 是两个契约：Windows 即用产物使用
`TedToolkit.Occt.Windows`，但普通调用代码仍使用 `TedToolkit.Occt` namespace。新的
platform、architecture 或 compiler ABI 不能只替换 native asset；必须重新证明 managed
exact layout，并交付对应的平台绑定产物。

The production generation path no longer uses a handwritten operation catalog or
`AbiOperationModel`. Per-type C++ files are still internal invocation helpers; the complete C11
transport boundary, CMake/DLL, managed imports, and exact-match validation remain unimplemented.
The historical ABI-v1 migration fixture is retired; verification targets the current generated
boundary and Runtime/native lifetime contracts.

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

The development generation host processes every public OCCT header and writes to `output/generated`:

```powershell
dotnet run --project tests/TedToolkit.Occt.Console/TedToolkit.Occt.Console.csproj -c Release
```

预期输出结构：

```text
output/generated/
├── csharp/                         # 生成的 C# 文件
└── cpp/
    ├── Geom2d_BSplineCurve.cpp     # one invocation source per RecordModel
    └── <DependencyType>.cpp
```

Exact members that cannot be instantiated or are absent from the delivered OCCT DLLs are excluded
only after compiler or linker proof; representable related declarations remain generated.

## 组件

| 组件 | 责任 | 文档 |
| --- | --- | --- |
| `TedToolkit.Occt.Windows` | Windows 平台生成绑定 package/assembly；当前支持矩阵仅为 `win-x64` | [README](src/core/TedToolkit.Occt.Windows/README.md) |
| `TedToolkit.Occt.Generator` | 读取 vcpkg/OCCT、解析 AST、建立模型并生成两组代码 | [README](src/core/TedToolkit.Occt.Generator/README.md) |
| `TedToolkit.Occt.Runtime` | 生成库依赖的最小、声明无关托管机制；提供异常投影、`handle<T>`、`Handle<T>` 和 `Owned<T>` | [README](src/core/TedToolkit.Occt.Runtime/README.md) |
| `TedToolkit.Occt.Analyzer` | 从已安装 OCCT 头文件生成可选择的头文件类型枚举 | `src/tools/TedToolkit.Occt.Analyzer` |
| `TedToolkit.Occt.Console` | Development host that generates the all-public-header Windows surface | `tests/TedToolkit.Occt.Console` |
| `Build` | 仓库构建管线；不会为平台 wrapper 生成 Runtime 友元权限 | `Build` |

## 当前实现边界

- `TedToolkit.Occt.Windows` generates every representable public-header declaration supported by the delivered OCCT DLLs.
- 当前仅支持经验证的 `win-x64`、OCCT 8.0.1 和 `net8.0` 组合。
- 本机生成和 NuGet 打包已经可用，但尚未发布到远程 feed。
- 新增平台、RID 或头文件范围时仍必须重新执行编译器探测和真实原生行为验证。

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

The repository build entry point runs the Generator, Runtime, and Runtime Analyzer TUnit projects.
It also configures and builds the two native Handle fixtures, then runs their managed integration
executable with the resolved library paths:

```powershell
dotnet run --project Build/Build.csproj
```

The Windows build and native fixture gate require PowerShell 7, CMake, and Visual Studio with
the MSVC C++ compiler, CMake tools, and LLVM (`clang-cl`) components. After the solution build,
managed tests run with `--no-build --no-restore`; the build phase itself performs its normal restore
behavior unless the caller configures the .NET environment to prevent it.

Use a short Windows checkout path. Long generated template names can exceed MSVC's object-path
limit in deeply nested worktrees; shorten the checkout if CMake warns about object path lengths.

Focused build-gate checks are available independently of the full native build:

```powershell
pwsh -NoProfile -File Build/VerifyGenerationCache.ps1
dotnet build Build/Build.csproj -c Release
pwsh -NoProfile -File Build/VerifyManagedTestGate.ps1
```

The managed-gate reflection check requires PowerShell running on .NET 10 or later. These checks
cover invalid caches, incomplete TRX results, failed child processes, and cancellation; they do not
replace the full solution and native integration gates.

Set `VCPKG_ROOT` to a usable vcpkg installation to run the real OCCT boundary test. When it is unset or the selected OCCT header is unavailable, only that environment-dependent test is reported as skipped.

The accepted object model is governed by the
[repository principles](docs/principles/README.md) and the
[generated binding architecture](docs/architecture/generated-binding-system.md). Current
implementation facts in this README do not override those records.

## 许可证

本项目使用 LGPL-3.0 许可，详见 [COPYING](COPYING) 和 [COPYING.LESSER](COPYING.LESSER)。OCCT 及其他依赖分别遵循其自身许可。
