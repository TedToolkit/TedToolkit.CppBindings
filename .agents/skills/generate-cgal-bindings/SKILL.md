---
name: generate-cgal-bindings
description: >-
  Use TedToolkit.CppBindings.Cgal.Generator to generate matching managed and native sources for the finite CGAL profile and EPICK semantics.
---

# Generate CGAL Bindings

- Confirm the installed CGAL headers and vcpkg profile before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.
