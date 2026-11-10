---
name: generate-cpp-bindings
description: >-
  Use TedToolkit.CppBindings.Generator when implementing a generation provider or emitting a matched managed/native boundary from native inputs.
---

# Generate C++ Bindings

- Choose the provider and declared native inputs first. Use `IGenerationProvider` or `SemanticGenerationProvider` according to the provider’s supported extension boundary.
- Register generation through `AddCppGenerators` and keep the generated C# and C++ outputs tied to the same plan and native library name.
- Verify the generated consumer and native export table together. Keep provider-specific lifetime and dependency facts in the provider.
