# FCL Generator

This package generates the finite `fcl-0.7.0-obbrss-double-windows-v1` profile. The profile locks
FCL and dependency versions, all BVH build codes, managed/native layouts, ownership, exports, and
toolchain identity. It does not claim coverage of the full FCL API.

The profile data is owned by the Generator code. The package includes the vcpkg manifest and registry
configuration needed to reproduce its native inputs. Generation reads the complete FCL public header
inventory from the selected vcpkg installation and verifies it against the installed package list.
Repository generation passes the vcpkg root explicitly. The parameterless compatibility entry points
resolve `VCPKG_ROOT` at invocation and fail before writing output when it is not configured.
