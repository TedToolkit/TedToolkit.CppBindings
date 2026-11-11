# TedToolkit.CppBindings.Manifold.Runtime

Contains the Manifold generated-code facade over Shared native-error projection. Shared owns all
currently emitted exception kinds and exact-once carrier cleanup; Manifold currently defines no
local exception kind. Consumers normally reference the Windows package rather than this package
directly.

## Agent skill

The `preserve-cpp-binding-lifetimes` skill ships in `TedToolkit.CppBindings.Runtime`, which this package restores as a dependency. If your project references only this package, get the resolved `TedToolkit.CppBindings.Runtime` version from `dotnet package list --include-transitive` and import the skill with `dotnet-package-skills install --package TedToolkit.CppBindings.Runtime@<resolved-version>`.
