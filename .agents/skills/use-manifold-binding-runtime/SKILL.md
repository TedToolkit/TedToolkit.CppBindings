---
name: use-manifold-binding-runtime
description: >-
  Use TedToolkit.CppBindings.Manifold.Runtime when a generated Manifold binding needs provider-specific native ownership or error semantics.
---

# Use the Manifold Binding Runtime

- Keep Manifold native errors on Shared’s error projection and preserve the lifetime of returned native owners.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.
