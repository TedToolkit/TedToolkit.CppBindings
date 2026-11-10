---
name: use-occt-binding-runtime
description: >-
  Use TedToolkit.CppBindings.Occt.Runtime when a generated OCCT binding needs provider-specific native ownership or error semantics.
---

# Use the OCCT Binding Runtime

- Preserve the distinction between the owning `Handle<T>` and the non-owning lowercase `handle<T>` storage projection.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.
