---
name: diagnose-cpp-binding-analyzers
description: >-
  Use TedToolkit.CppBindings.Analyzers when building generated bindings or diagnosing compiler checks for borrowed native owner references.
---

# Diagnose C++ Binding Analyzers

Build the consumer with the exact analyzer package and locate the first diagnostic. Determine whether it is a generated hook signature or a borrowed native owner lifetime issue before changing code.

## Workflow

- Reference the analyzer package directly where checks are needed. Its analyzer assembly is under `analyzers/dotnet/cs` and consumers do not import it as a runtime API.
- Trace a borrowed value to the native owner and keep that owner alive and valid for its entire use. Inspect the generated hook signatures when the diagnostic involves extension hooks.
- Build the consumer with the analyzer to verify that the intended diagnostic clears without changing native lifetime semantics.

## Example

```xml
<PackageReference Include="TedToolkit.CppBindings.Analyzers"
                  Version="1.0.0" PrivateAssets="all" />
```

## Check the result

Verify a bounded borrow passes and an escaped or disposed owner still reports a diagnostic. For a hook, compare the signature with the generated extension point and rebuild the consuming project.
