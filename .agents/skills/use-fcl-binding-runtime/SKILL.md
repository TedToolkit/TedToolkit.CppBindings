---
name: use-fcl-binding-runtime
description: >-
  Use TedToolkit.CppBindings.Fcl.Runtime when a generated FCL binding needs provider-specific native ownership or error semantics.
---

# Use the FCL Binding Runtime

- Keep FCL native errors on Shared’s error projection and preserve the provider’s explicit model build status.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.
