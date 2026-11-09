# SMLib Windows bindings

`TedToolkit.CppBindings.Smlib.Windows` provides the finite SMLib 1.0.1 profile for .NET 8 and
Windows x64. The NuGet package contains the generated managed API, the matching native module and
its runtime dependencies, and the upstream license and notices.

```csharp
using TedToolkit.CppBindings.Smlib;

using var box = Solids.CreateBox(0, 0, 0, 2, 3, 4);
double volume = box.ComputeVolume(1e-7); // 24
```

The returned owner releases the native solid on disposal. Calls into the shared SMLib context are
serialized by the binding. Build instructions are in the [provider guide](../README.md).
