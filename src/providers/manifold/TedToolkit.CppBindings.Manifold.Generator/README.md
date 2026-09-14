# TedToolkit.CppBindings.Manifold.Generator

Generates the locked `manifold-3.5.2-windows-v2` finite binding profile. The profile covers owned
triangle meshes, Boolean operations, translation, status, triangle count, and mesh exchange. It
does not claim coverage of the complete Manifold API.

The Generator owns only the finite Manifold profile, native algorithm bodies, dependency policy, and
header discovery. It supplies those facts through Shared's `SemanticGenerationProvider`; Shared
constructs the immutable `GenerationPlan` and emits the common managed/native transport, ownership,
bootstrap, and publication machinery through `AddCppGenerators`.

The package includes the vcpkg manifest and registry configuration needed to reproduce its native
inputs. Generation reads the complete Manifold public header inventory from the selected vcpkg
installation and verifies it against the installed package list. The locked `3.5.2` identity
represents vcpkg port revision zero, so a nonzero `Port-Version` is rejected. Repository generation
passes the vcpkg root explicitly. The parameterless provider constructor resolves `VCPKG_ROOT` and
fails before plan creation when it is not configured. Generated managed and native sources are a
matched pair and must be built together.