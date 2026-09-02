# TedToolkit.Occt.Windows

Generated OCCT bindings for the pinned `win-x64`, OCCT 8.0.1 artifact set. Public APIs use the
`TedToolkit.Occt` namespace and reference `TedToolkit.Occt.Runtime` for ownership and error
projection.

The package contains its matched generated native library and the complete `x64-windows` runtime
DLL set required by the generated surface. Consumers do not run the Generator and do not need OCCT,
vcpkg, Clang, or CMake.

The Windows generation host selects every public OCCT header. Types and members are retained when
they can be represented and linked against the delivered OCCT binaries; exact members proven absent
or uninstantiable are omitted. No other Windows architecture is currently supported.
