---
name: use-smlib-windows-bindings
description: >-
  Use TedToolkit.CppBindings.Smlib.Windows when a .NET consumer needs the packaged SMLib binding on Windows x64.
---

# Use SMLib Windows Bindings

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Use `Solids.CreateBox` in a `using` scope for an owned solid. Check a representative `ComputeVolume` call before extending the consumer.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.
