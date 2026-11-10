---
name: preserve-cpp-binding-lifetimes
description: >-
  Use TedToolkit.CppBindings.Runtime to reason about native owners, borrowed references, and native error projection in generated C# bindings.
---

# Preserve C++ Binding Lifetimes

- Acquire an owner through the generated factory and dispose it at the lifetime boundary. Read the binding’s native contract before retaining a borrowed reference.
- Keep a borrowed reference within the lifetime and invalidation rules of its native owner. Copy or retain through an explicit API when independent lifetime is required.
- Verify the generated code’s disposal and native error paths separately from ordinary successful calls.
