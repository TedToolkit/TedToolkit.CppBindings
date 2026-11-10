---
name: use-manifold-windows-bindings
description: >-
  Use TedToolkit.CppBindings.Manifold.Windows when a .NET consumer needs the packaged Manifold binding on Windows x64.
---

# Use Manifold Windows Bindings

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Dispose the owner returned by `Manifold.Create`. Treat mesh data returned by `GetMesh` as a fresh managed result and check status before subsequent geometry operations.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.
