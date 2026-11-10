---
name: use-cgal-binding-runtime
description: >-
  Use TedToolkit.CppBindings.Cgal.Runtime when a generated CGAL binding needs provider-specific native ownership or error semantics.
---

# Use the CGAL Binding Runtime

- Keep CGAL-specific diagnostic kinds separate from Shared native errors and keep precondition diagnostics available without changing the carrier cleanup path.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.
