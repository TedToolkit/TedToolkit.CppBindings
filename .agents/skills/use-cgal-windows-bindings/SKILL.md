---
name: use-cgal-windows-bindings
description: >-
  Use TedToolkit.CppBindings.Cgal.Windows when a .NET consumer needs the packaged CGAL binding on Windows x64.
---

# Use CGAL Windows Bindings

- Confirm the consumer targets the package’s supported .NET and Windows x64 runtime. Use the package’s generated managed APIs and matching app-local native module.
- Confirm the operation is in the package’s finite EPICK surface and check the documented ownership of the result.
- Validate a successful call from a clean consumer build or publish directory and verify that the expected native dependencies resolve there.
