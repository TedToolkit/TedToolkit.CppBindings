# TedToolkit.CppBindings.Manifold.Runtime

Contains the Manifold generated-code facade over Shared native-error projection. Shared owns all
currently emitted exception kinds and exact-once carrier cleanup; Manifold currently defines no
local exception kind. Consumers normally reference the Windows package rather than this package
directly.
