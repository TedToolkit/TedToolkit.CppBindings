---
name: generate-occt-bindings
description: >-
  Use TedToolkit.CppBindings.Occt.Generator to generate matching managed and native sources for the supported OCCT declaration and layout profile.
---

# Generate OCCT Bindings

- Confirm the OCCT version and compiler-proved native input facts before generating. Use the provider’s declared configuration and the shared `AddCppGenerators` pipeline.
- Keep native inputs, provider profile, generated C# declarations, and generated C++ exports tied to one build. Check the reported coverage before assuming a declaration is supported.
- Verify the generated consumer and native module together on the target platform.
