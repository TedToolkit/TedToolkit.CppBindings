# Provider generation inputs

## Cgal

- Confirm the installed CGAL headers and vcpkg profile before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.

```csharp
using TedToolkit.FileSystem;

var options = new CgalGenerationOptions
{
    VcpkgRoot = new DirectoryPath(@"C:\vcpkg"),
    CSharpFolder = new DirectoryPath("generated/csharp"),
    CppFolder = new DirectoryPath("generated/cpp"),
    CSharpNamespace = "TedToolkit.CppBindings.Cgal",
    NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
    CppVersion = 20,
    NativeLibraryVersion = new Version(6, 2),
};

builder.AddCgalGenerators(options);
```

## Fcl

- Confirm the complete installed FCL header inventory and pinned vcpkg inputs before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.

## Manifold

- Confirm the pinned Manifold headers and vcpkg inputs before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.

## Occt

- Confirm the OCCT version and compiler-proved native input facts before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.

## Smlib

- Confirm the official SMLib source root passed to `SmlibGenerationProvider` before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.
