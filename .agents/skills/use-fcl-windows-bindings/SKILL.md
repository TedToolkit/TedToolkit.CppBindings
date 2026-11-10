---
name: use-fcl-windows-bindings
description: >-
  Use TedToolkit.CppBindings.Fcl.Windows when a .NET consumer needs the packaged FCL binding on Windows x64.
---

# Use FCL Windows Bindings

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Check `FclModelBuildResult.Code` and dispose every successful model owner. Use the provider’s contact semantics for initial overlap, endpoint contact, and a miss.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.
