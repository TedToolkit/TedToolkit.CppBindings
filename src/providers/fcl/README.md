# FCL provider

The FCL provider is an independent finite binding family for FCL 0.7.0 on Windows x64. It exposes
double-precision `BVHModel<OBBRSS<double>>` construction and linear continuous collision without
referencing or converting OCCT, CGAL, or Manifold types.

- `TedToolkit.CppBindings.Fcl.Generator` owns the locked profile and semantic facts submitted through
  the Shared Generator contract; Shared owns plan construction and complete paired emission.
- `TedToolkit.CppBindings.Fcl.Runtime` owns native diagnostic projection.
- `TedToolkit.CppBindings.Fcl.Windows` packages the matched managed/native binding and exact DLL closure.

Repository builds materialize this provider through the shared
`src/tools/TedToolkit.CppBindings.Windows.Generation.Tool` host.
