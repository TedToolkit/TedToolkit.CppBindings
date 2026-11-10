---
name: use-occt-windows-bindings
description: >-
  Use TedToolkit.CppBindings.Occt.Windows when a .NET consumer needs the packaged OCCT binding on Windows x64.
---

# Use OCCT Windows Bindings

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Use generated factories for owning values. Keep borrowed `ref` and `ref readonly` returns inside the native owner’s lifetime and invalidation rules.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.
