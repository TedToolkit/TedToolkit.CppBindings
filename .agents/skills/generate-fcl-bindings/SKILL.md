---
name: generate-fcl-bindings
description: >-
  Use TedToolkit.CppBindings.Fcl.Generator to generate matching managed and native sources for the finite OBBRSS double profile.
---

# Generate FCL Bindings

- Confirm the complete installed FCL header inventory and pinned vcpkg inputs before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.
