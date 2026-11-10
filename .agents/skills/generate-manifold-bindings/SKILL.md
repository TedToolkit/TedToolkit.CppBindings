---
name: generate-manifold-bindings
description: >-
  Use TedToolkit.CppBindings.Manifold.Generator to generate matching managed and native sources for the finite mesh and Boolean profile.
---

# Generate Manifold Bindings

- Confirm the pinned Manifold headers and vcpkg inputs before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.
