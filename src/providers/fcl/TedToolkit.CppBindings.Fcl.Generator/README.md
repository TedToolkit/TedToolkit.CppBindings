# FCL Generator

This package generates the finite `fcl-0.7.0-obbrss-double-windows-v2` profile. The profile locks
FCL and dependency versions, all BVH build codes, managed/native layouts, ownership, exports, and
toolchain identity. It does not claim coverage of the full FCL API.

The profile data is owned by the Generator code. The package includes the vcpkg manifest and registry
configuration needed to reproduce its native inputs. Generation reads the complete FCL public header
inventory from the selected vcpkg installation and verifies it against the installed package list.
Repository generation constructs `FclGenerationProvider` with the vcpkg root and publishes its
`GenerationPlan` through Shared's `AddCppGenerators` pipeline. The parameterless constructor resolves
`VCPKG_ROOT` and fails before plan creation when it is not configured.

FCL supplies only its finite profile facts, header inventory, native algorithm bodies, and CMake
dependency policy. Shared owns buffer pointer/length projection, validation, conditional `Owned<T>`
construction, composite-result assembly, native-error catches, function-table slots, bootstrap source,
and output publication. FCL has no private plan, output writer, or complete managed/native renderer.
