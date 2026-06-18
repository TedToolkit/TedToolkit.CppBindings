# TedToolkit.Occt

## 做一个自己的
1. Occt相关的Net包不是要收费就是不好用。
2. 官方的Occt Wrapper,非常不Occt, Handle<>、gp_pnt没有非常符合c#的习惯。
3. 封装库，不论用多少，始终会打包完整的Occt包，冗余与臃肿。
（2026.01调研的一个结果）

## 理想状态下的C# Occt的库。

1. Occt当中，是面向对象的。
2. 跨平台，起码支持Windows和Linux。
3. 堆上对象和栈上对象要和cpp的相对应。（gp_pnt struct, geom_XXXX (handle<>) class）
4. 高性能的：无过多频繁的abi操作(点的x值修改，就别找cpp了)，或者gc操作 (class)，或者heap操作（new / delete）。
5. 不能有非c#的exception出现，其会直接导致崩溃。
6. 可被裁剪的，如果一些功能没有用到，对应的dll文件甚至代码不因出现。（Source/Code Generator）要用多少，生成多少。（精确到类/类中方法）
7. 支持多framework。

## 解决方案

### 对象的生命周期
其中内存分布必须和C++一致，能够直接调用cpp的函数。

#### 栈（C#）

##### 无特殊构造和析构函数

只是一个c的typedef之类的。
```c#

[StructLayout(LayoutKind.Explicit)]
public struct Dummy
{
    [FieldOffset(0)]
    public float Field;
}

```

##### 有特殊构造和析构函数

```c#

[StructLayout(LayoutKind.Explicit)]
public struct Dummy : IDisposable
{
    [FieldOffset(0)]
    public float Field;

    [SkipLocalsInit]
     public unsafe Dummy(float a)
    {
        fixed (Dummy* ptr = &this)
        {
            Wrapper.ConstructDummy(ptr, a); // Cpp ::new()
        }
    }


    public void Dispose()
    {
        fixed (Dummy* ptr = &this)
        {
            Wrapper.DestructDummy(ptr, a); // Cpp ~
        }
    }
}

```

#### 堆（C++ new/delete）

```c#

public struct Dummy;

public class Handle<TUnmanaged> : IDisposalbe
{

}

```

#### 堆（C++）

```c#
[StructLayout(LayoutKind.Explicit)]
public struct Dummy
{
    [FieldOffset(0)]
    public float Field;

    public Dummy()
    {
        throw new InvalidOperationException();
    }
}

public unsafe class Handle<TUnmanaged> : IDisposable
{
    private readonly TUnmanaged* _value;
    public Handle()
    {
        _value = Wrapper.Construct();
    }

    public void Dispose()
    {
        Wrapper.Destruct(_value);
    }
}

class MyClass
{
    public MyClass()
    {
        using var point = new Handle<Point>();
    }
}
```

#### 堆(C#)

使用TedToolkit.Refly

### 解析头文件方案

### 动态生成方案

## 类型系统设计

这个项目不是单纯把 `ClangSharp.Type` 翻译成一个 C# 类型名，而是要自动生成一整套 C++ / `extern "C"` / PInvoke / C# Public API 的 wrapper。  
因此，一个类型不能只有一种“名字”，而应该根据使用位置投影成不同的形态。

### 函数中的四种类型视图

对于一个出现在函数签名中的类型，需要同时考虑下面四种状态：

1. `CppOriginal`
   C++ 原始语义中的类型，也就是直接在 C++ 成员函数、构造函数、返回值里真正使用的类型。
   例子：`const gp_Pnt&`、`occ::handle<Geom_Surface>`。

2. `CppInterop`
   为了通过 `extern "C"` 暴露给外部时，C++ 侧导出函数真正使用的类型。
   例子：把 `const gp_Pnt&` 改成 `const gp_Pnt*`，把 `occ::handle<Geom_Surface>` 改成 `Geom_Surface*`。

3. `CSharpPInvoke`
   C# 中 `[DllImport]` / source-generated PInvoke 层看到的类型，也就是和 `CppInterop` 一一对应的 C# 表达。
   这个层级的目标不是“最符合 C# 习惯”，而是“内存布局与 ABI 对齐”。
   例子：`gp_Pnt*`、`Geom_Surface*`、`int`。

4. `CSharpPublic`
   最终对用户公开的 C# API 类型。
   这一层才允许出现 `ref`、`in`、`out`、`Span<T>`、包装类等更符合 C# 使用习惯的表达。
   例子：`in gp_Pnt`、`Handle<Geom_Surface>`、`out double`。

### 字段中的类型视图

字段和函数不同。

字段本质上只需要关心内存布局，因此通常只需要 `CSharpPInvoke` 这一层的类型视图。  
因为字段生成时不需要重建 C++ 语义，只需要保证和原生内存一致，所以：

- `&` 在字段上下文中通常需要转成 `*`
- 关键目标是布局一致，不是 API 友好

也就是说：

- 函数：需要 `CppOriginal` / `CppInterop` / `CSharpPInvoke` / `CSharpPublic`
- 字段：通常只需要 `CSharpPInvoke`

### 模型方向

因此类型模型不应该只是：

- 一个 `NativeType`
- 一个 `ManagedType`

而应该是“一个类型，按上下文产出多种投影”。

建议的核心模型如下：

```csharp
public sealed class TypeModel
{
    public required ClangSharp.Type SourceType { get; init; }

    public required string CppOriginalDisplayName { get; init; }

    public ClangSharp.CXXRecordDecl? ReferencedRecord { get; init; }

    public required CppAst.CppType CppInteropType { get; init; }

    public required DataType CSharpPInvokeType { get; init; }

    public required DataType CSharpPublicType { get; init; }
}
```

其中：

- `CppOriginalDisplayName` 用于保留 C++ 原始语义
- `ReferencedRecord` 用于递归生成依赖类型
- `CppInteropType` 用于生成 `extern "C"` 导出函数
- `CSharpPInvokeType` 用于生成 PInvoke 层和字段
- `CSharpPublicType` 用于生成最终对外公开的方法签名
- `ref` / `in` / `out` 不放进 `TypeModel`，而是在参数生成阶段直接使用 RoslynHelper 原生节点处理

### 为什么 `CppInteropType` 用 `CppAst`，`CSharpPInvokeType` 和 `CSharpPublicType` 用 `DataType`

`CppInteropType` 服务的是 `extern "C"` 导出层，它本质上仍然属于 C++ 语义。  
因此这里不建议复用 RoslynHelper，而应尽量复用现成的 C++ 类型模型。

当前更合适的方向是：

- `SourceType` 继续来自 `ClangSharp`
- `CppInteropType` 使用 `CppAst` 的类型模型
- `CSharpPInvokeType` / `CSharpPublicType` 使用 RoslynHelper `DataType`
- `ref` / `in` / `out` 不混进类型本体，而是在参数生成阶段单独处理

这样可以避免自己维护一套半成品的 C++ AST。

### 为什么 `CSharpPInvokeType` 和 `CSharpPublicType` 用 `DataType`，而不是只用 `IExpression`

这两层不是简单字符串，但它们也不应该只是裸 `IExpression`。

原因是 C# 里“类型”和“参数修饰”不是一个维度：

- `XXX*` 是类型本体
- `Handle<XXX>` 是类型本体
- `ref XXX` / `in XXX` / `out XXX` 则是“参数位置上的修饰”

因此更稳妥的做法是：

- 类型本体使用 RoslynHelper `DataType`
- 参数修饰留在参数生成层处理

这样：

- 字段可以直接复用 `CSharpPInvokeType`
- 方法参数可以在生成时组合 `DataType` 和 RoslynHelper 自带的参数/表达式节点
- 不需要把 `ref` / `in` / `out` 错塞进类型表达式内部
- 仍然可以继续复用 RoslynHelper 的语法节点来表达 `XXX*` 之类的结构化类型

### 关于 `offset` / `size`

即使类型投影层使用 `CppAst`，`offset` / `size` 的计算也仍然应该保留“生成原生探针并编译运行”的路线。  
原因是字段偏移和对象大小不是单纯语法问题，而是 ABI、编译器、平台、宏和编译选项共同决定的结果。

也就是说：

- `CppAst` 适合承载类型模型和类型投影
- `offset` / `size` 仍然应以真实 C++ 编译结果为准

可以优化的是“探针源码的构造方式”，例如减少字符串拼接、集中抽象探针模板；  
但不建议把字段布局判断退化成纯 AST 推导。

### 规则系统

类型转换应当通过规则完成，而不是把逻辑硬编码在一个大 `TypeService` 中。

建议采用：

- `ITypeResolver`
- `ITypeRule`

流程如下：

1. 从 `ClangSharp.Type` 构造基础 `TypeModel`
2. 规则按顺序匹配并改写不同投影
3. 生成器按上下文读取对应投影

第一条规则就是 `occ::handle<XXX>`：

- `CppOriginalDisplayName` 保持 `occ::handle<XXX>`
- `ReferencedRecord` 指向 `XXX`
- `CppInteropType` 投影为 `XXX*`
- `CSharpPInvokeType` 投影为 `XXX*`
- `CSharpPublicType` 当前也可以先投影为 `XXX*`，后续再单独提升为 `Handle<XXX>` 或别的公开包装

### 设计原则

1. 一个 `Clang` 类型只解析一次，得到一个 `TypeModel`。
2. 不同代码生成阶段只读取自己需要的投影，不再各自重新猜类型。
3. 递归生成依赖时只看 `ReferencedRecord`，不从显示名或字符串里反推。
4. 字段优先保证 ABI 和布局一致。
5. 函数签名再在 ABI 正确的前提下追求 C# 友好。
