# TedToolkit.CppBindings.CAbi.Generator

Generate C# static methods for external C linkage functions declared directly in selected C++ headers.
The native adapter links to the source library and publishes the generated calls through the existing
`NativeApi` function table. The managed project compiling the generated sources references
`TedToolkit.CppBindings.Runtime` and enables unsafe code.

```csharp
using ModularPipelines;
using TedToolkit.CppBindings.CAbi.Generator;
using TedToolkit.CppBindings.Generator;
using TedToolkit.FileSystem;

var options = new CAbiGenerationOptions
{
    CSharpFolder = new DirectoryPath("generated/csharp"),
    CppFolder = new DirectoryPath("generated/cpp"),
    CSharpNamespace = "Example.Bindings",
    NativeLibraryBaseName = "example_bindings",
    Headers =
    [
        new(new FilePath("include/Geometry.h")),
        new(new FilePath("include/Utilities.h"), WrapperClassName: "Control"),
    ],
    LinkLibraries = ["C:/example/lib/example.lib"],
};

var pipeline = await Pipeline.CreateBuilder()
    .AddCppGenerators(options, new CAbiGenerationProvider(options))
    .BuildAsync();
await pipeline.RunAsync();
```

This generates `Example.Bindings.GeometryWrapper` and `Example.Bindings.Control` when the respective
headers declare eligible functions. The default class name uses the header stem followed by
`Wrapper`. A character that cannot appear in a C# identifier is replaced with an underscore.
`IncludeDirectories` and `CompileDefinitions` are used for both Clang discovery and native compilation.
Build the generated CMake project with MSVC for Windows x64. Supply a static or import library
through `LinkLibraries` for source functions implemented outside the selected headers.
If the vendor ships only a DLL, obtain its import library or build an x64 import library with
MSVC `lib /DEF` using the DLL basename and verified named function exports. Deploy the DLL and
its native dependencies alongside the generated adapter so the Windows loader can resolve them.

Both forms of `extern "C"` are supported. A declaration such as
`namespace Geometry { extern "C" int Add(int, int); }` uses `::Geometry::Add` for C++ source
lookup and the C symbol `Add` on Windows x64. Namespaces therefore cannot give two independent
C-linkage functions the same name. Select the owning header once if multiple headers redeclare
the same C symbol.

The generator supports fixed signatures with primitive values, enum underlying values, pointers,
and supported lvalue references. Record pointers use opaque `void*` storage in C#. Borrowed
reference returns preserve `ref` or `ref readonly` and follow native lifetime and invalidation
rules. Unsupported signatures, including record values, callbacks, and variadic functions,
fail generation with a function-specific diagnostic.
