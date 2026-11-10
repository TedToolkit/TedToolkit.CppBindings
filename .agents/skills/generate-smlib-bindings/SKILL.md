---
name: generate-smlib-bindings
description: >-
  Use TedToolkit.CppBindings.Smlib.Generator to generate matching managed and native sources for the finite SMLib solid profile.
---

# Generate SMLib Bindings

- Confirm the official SMLib source root passed to `SmlibGenerationProvider` before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.
