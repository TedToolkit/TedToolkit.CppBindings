# TedToolkit.Occt.Runtime

`TedToolkit.Occt.Runtime` 提供生成后的 OCCT C# 类型所依赖的基础运行时契约，包括原生句柄访问、所有权管理、原生类型标记和 C++ 异常转换。

> ⚠️ Runtime 不是 OCCT 的独立托管实现。它必须与生成的 C# 代码以及名称匹配的原生 `ted_toolkit_occt` 库一起使用；当前仓库尚未形成经过端到端验证的发布包。

## 能力

| 能力 | 入口 | 作用 |
| --- | --- | --- |
| 拥有原生对象 | `Handle<TElement>` | 持有 `Standard_Transient` 派生对象的指针，并在 Dispose/终结时释放。 |
| 借用原生对象 | `handle<TElement>` | 使用 `ref struct` 提供不拥有生命周期的轻量指针视图。 |
| 统一句柄访问 | `IHandle<TElement>` | 暴露 `Value` 引用和 `NativeHandle` 指针。 |
| 标记原生类型 | `NativeTypeNameAttribute` | 在生成的类型、字段、参数和返回值上保留原始 OCCT 类型名。 |
| 转换原生异常 | `interop_error`、`OcctNativeException` | 把 C++ 异常负载转换成适当的 .NET 异常。 |
| 验证值类型方向 | `gp_Pnt2d` | 当前仓库中的手写原型，用于验证值类型原生调用形状。 |

## 对象表示与生命周期

OCCT 类型需要区分值语义和 `Standard_Transient` 引用语义。

### 值类型

生成器计划根据 libclang 获得的对象大小和字段偏移，生成与原生布局相同的 C# struct：

```csharp
[StructLayout(LayoutKind.Explicit, Size = /* native sizeof */)]
public unsafe struct gp_Pnt
{
    [FieldOffset(/* native offset */)]
    private double x;
}
```

字段和 ABI 层优先使用 P/Invoke 投影类型，避免公共 API 的友好表达改变原生内存布局。

### 拥有的 `Standard_Transient` 对象

`Handle<TElement>` 是托管所有者：

- `TElement` 必须是 unmanaged，并实现 `IStandard_Transient`。
- `NativeHandle` 提供原生指针，释放后访问会抛出 `ObjectDisposedException`。
- `Value` 提供指向同一对象的托管 ref。
- `Dispose` 使用原子交换保证释放只执行一次，并抑制终结器。
- 未显式 Dispose 时，终结器仍尝试释放原生对象。

以下代码表示预期使用形态；生成工厂尚未完成，当前不能直接运行：

```csharp
using Handle<MyTransientType> owned = /* generated factory */;
ref MyTransientType value = ref owned.Value;
```

当前构造函数是 internal，实例应由生成代码在原生构造成功后创建，而不是由普通调用方直接 new。

### 借用视图

小写 `handle<TElement>` 是 `readonly ref struct`，只保存指针，不负责释放：

```csharp
handle<MyTransientType> borrowed = owned.Tohandle();
```

它不能逃逸到堆上，适合表达一次调用范围内的借用。`Handle<T>` 还提供到 `handle<T>` 的隐式转换。

## 异常边界

C++ wrapper 使用 `csharp_interop.h` 捕获：

- `Standard_Failure`；
- `std::exception`；
- 其他未知异常。

原生侧返回以下 ABI 结构，而不是让 C++ 异常跨越 P/Invoke：

```cpp
struct interop_error
{
    char* type_name;
    char* message;
    char* stack_trace;
};
```

托管侧 `interop_error.ThrowIfError()`：

1. 把 UTF-8 指针转换成托管字符串；
2. 根据原生类型名称映射 `ArgumentException`、`ArgumentOutOfRangeException`、`OverflowException`、`InvalidOperationException` 等常见异常；
3. 无法归类时创建 `OcctNativeException`，保留原生类型名和堆栈；
4. 调用原生 `free_error` 释放错误字符串。

这条边界的目标是保证所有异常在 C++ 内被捕获，并以普通 C 数据返回给 .NET。

## 与生成代码的关系

```text
生成的 C# Public API
        │
        ▼
生成的 P/Invoke 声明
        │
        ▼
ted_toolkit_occt 原生导出函数
        │
        ├── 调用 OCCT
        └── 返回 interop_error
                    │
                    ▼
             Runtime 转换为 .NET 异常
```

Runtime 只负责共享机制。具体 OCCT 类型、导出函数名称、P/Invoke 方法和构造工厂应由 Generator 产生。

## 兼容性

项目当前目标框架来自 `AlmostAllFrameworks.props`：

- .NET 6、7、8、9、10
- .NET Framework 4.7.2、4.8
- .NET Standard 2.0、2.1

代码包含针对旧目标框架的 UTF-8 转换和 API 兼容分支。实际原生库仍必须按运行平台和架构分别生成、部署。

## 当前实现边界

- Generator 尚未生成完整 P/Invoke 调用层，因此 Runtime 与生成原生库还没有端到端接通。
- 手写 `gp_Pnt2d` 是验证原型，其 DllImport 库名仍为占位值 `Name`，不应视为可用绑定。
- `Handle<T>` 依赖生成类型实现 `IStandard_Transient.Delete()`；没有对应生成代码时不能独立使用。
- Runtime 不负责定位或下载 OCCT，也不负责生成原生库。
- 当前没有证据表明该包已经发布到可消费的 NuGet 源。

## 开发与验证

从仓库根目录构建 Runtime：

```powershell
dotnet build src/core/TedToolkit.Occt.Runtime/TedToolkit.Occt.Runtime.csproj -c Release
```

运行 Runtime 测试：

```powershell
dotnet run --project tests/TedToolkit.Occt.Runtime.Tests/TedToolkit.Occt.Runtime.Tests.csproj -c Release --no-build -- --report-trx
```

Runtime 测试依赖 `Build/Modules/01_Perpare/GenerateCodeModule.cs` 预先生成 triplet 对应的 `InternalsVisibleTo` 声明；绕过准备阶段直接构建时，该测试会失败。

## 相关文档

- [仓库概览](../../../README.md)
- [Generator 原理](../TedToolkit.Occt.Generator/README.md)
