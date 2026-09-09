# TedToolkit.CppBindings.Manifold.Generator

Generates the locked `manifold-3.5.2-windows-v1` finite binding profile. The profile covers owned
triangle meshes, Boolean operations, translation, status, triangle count, and mesh exchange. It
does not claim coverage of the complete Manifold API.

The profile data is owned by the Generator code. The package includes the vcpkg manifest and registry
configuration needed to reproduce its native inputs. Generation reads the complete Manifold public
header inventory from the selected vcpkg installation and verifies it against the installed package
list. The locked `3.5.2` identity represents vcpkg port revision zero, so a nonzero `Port-Version`
is rejected. Repository generation passes the vcpkg root explicitly. The parameterless compatibility
entry points resolve `VCPKG_ROOT` at invocation and fail before writing output when it is not
configured. Generated managed and native sources are a matched pair and must be built together.
