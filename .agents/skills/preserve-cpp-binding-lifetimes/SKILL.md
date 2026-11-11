---
name: preserve-cpp-binding-lifetimes
description: >-
  Use TedToolkit.CppBindings.Runtime and packaged native providers to choose bindings, manage native owners and borrowed references, and handle projected errors.
---

# Preserve C++ Binding Lifetimes

Identify the provider and package already referenced by the consumer. Use [Windows provider guidance](references/windows-providers.md) for ready-to-use bindings and [provider runtime semantics](references/provider-runtime.md) when the failure or lifetime boundary is provider-specific.

## Preserve ownership

1. Acquire owners through generated factories and use `using` at the call site that owns disposal. Trace each borrowed `ref` or `ref readonly` value to the original native owner.
2. Keep borrowed values within the owner’s lifetime and invalidation rules. Copy or retain only through a native operation whose contract explicitly grants independent lifetime.
3. Verify result and exception paths separately. A successful native call and a projected error can require different cleanup and interpretation.
4. Run a clean consumer build or publish for the intended target runtime. If the library fails to load, check the package identity, native asset, and app-local dependency closure before changing the binding code.
