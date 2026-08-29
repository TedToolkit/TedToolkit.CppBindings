# TedToolkit.Occt.Windows

Generated OCCT bindings for the pinned `win-x64`, OCCT 8.0.1 artifact set. Public APIs use the
`TedToolkit.Occt` namespace and reference `TedToolkit.Occt.Runtime` for ownership and error
projection.

The package contains its matched generated native library and OCCT runtime dependencies. Consumers
do not run the Generator and do not need OCCT, vcpkg, Clang, or CMake.

This initial package contains the declaration closure selected by the Windows generation host. It
does not claim every OCCT header or another Windows architecture.
