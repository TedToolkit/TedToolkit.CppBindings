---
name: generate-cpp-bindings
description: >-
  Use TedToolkit.CppBindings.Generator to choose a native provider, configure source discovery, and generate and verify the matched C# and C++ binding outputs.
---

# Generate C++ Bindings

Decide whether the consumer needs a packaged binding or a new generated surface. Use this skill for generation, including a changed provider profile or native header inventory.

## Build one matched generation plan

1. Identify the native provider, version, toolchain, supported profile, and source inputs. Use [provider generation inputs](references/providers.md) to choose the appropriate provider and prove those inputs before emitting code.
2. Use the provider’s declared extension boundary. Register through `AddCppGenerators` and keep the managed namespace, native library identity, output root, and semantic plan consistent.
3. Preserve native ownership, references, and explicit results during projection. Keep provider-specific facts in the provider and use the shared engine for common generation behavior.
4. Compile the generated C# consumer against the native build and verify the export table and provider coverage. Test failure, ownership, or unsupported surface where the profile changes.

See [the generation example](references/generation.md) for a complete call shape. Use the runtime skill for consumer lifetime and native package selection.
