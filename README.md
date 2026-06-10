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