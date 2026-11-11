# FCL Runtime

This package exposes the FCL generated-code facade over Shared native-error projection. Shared owns
the common exception kinds and exact-once carrier cleanup; FCL currently defines no local exception
kind. The package contains no dependency on another geometry provider.

## Agent skill

The `preserve-cpp-binding-lifetimes` skill ships in `TedToolkit.CppBindings.Runtime`, which this package restores as a dependency. If your project references only this package, get the resolved `TedToolkit.CppBindings.Runtime` version from `dotnet package list --include-transitive` and import the skill with `dotnet-package-skills install --package TedToolkit.CppBindings.Runtime@<resolved-version>`.
