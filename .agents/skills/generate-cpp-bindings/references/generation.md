# Generation setup

Use the package’s public generation configuration and keep the source and output folders distinct.

```csharp
using TedToolkit.CppBindings.Generator;
using TedToolkit.FileSystem;

var options = new GenerationOptions
{
    CSharpFolder = new DirectoryPath("generated/csharp"),
    CppFolder = new DirectoryPath("generated/cpp"),
};
```

Confirm `GenerationOptions.NativeLibraryBaseName` and the emitted native module name agree. If a required header or export is missing, diagnose discovery and profile selection before changing generated files.
