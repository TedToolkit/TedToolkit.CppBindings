# SMLib Generator

The generator takes an official USD BRep 1.0.1 source checkout through
`SmlibGenerationProvider(sourceRoot)`. It uses `NativeSources.FromSourceDirectory` and
the shared binding pipeline to generate C#, the paired C++ boundary, and a CMake project.
Set `GenerationOptions.NativeLibraryBaseName` to `SmlibGenerationProvider.NativeLibraryBaseName`
and `CSharpNamespace` to `TedToolkit.CppBindings.Smlib` when calling `AddCppGenerators`.

The finite profile covers creating a box, computing its volume, and disposing its native owner.
The generated CMake project builds the SMLib kernel and SM_API directly from the supplied source
tree with C++17. See the [provider guide](../README.md) for the packaged build.
