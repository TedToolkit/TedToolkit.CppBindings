---
name: diagnose-cpp-binding-analyzers
description: >-
  Use TedToolkit.CppBindings.Analyzers when building generated bindings or diagnosing compiler checks for borrowed native owner references.
---

# Diagnose C++ Binding Analyzers

- Reference the analyzer package directly where checks are needed. Its analyzer assembly is under `analyzers/dotnet/cs` and consumers do not import it as a runtime API.
- Trace a borrowed value to the native owner and keep that owner alive and valid for its entire use. Inspect the generated hook signatures when the diagnostic involves extension hooks.
- Build the consumer with the analyzer to verify that the intended diagnostic clears without changing native lifetime semantics.
