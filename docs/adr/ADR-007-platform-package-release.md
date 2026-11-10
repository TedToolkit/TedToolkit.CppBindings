# ADR-007 — Publish verified provider packages by platform

- Status: Accepted
- Date: 2026-11-10
- Decision owner: TedToolkit maintainers
- Approval source: The maintainer approved the Linux x64 and Apple Silicon package projects
  and the three-platform release gate in the 2026-11-10 conversation.
- Related: [Native function table](ADR-003-native-function-table-bootstrap.md),
  [Windows package isolation](ADR-005-provider-native-package-isolation.md),
  [repository principles](../principles/README.md)

Platform packages pair the managed assembly and native module from one generation run for one
compiler ABI. The initial release matrix covers Windows x64, Linux x64 on Ubuntu 24.04, and
Apple Silicon on macOS 15. Each provider keeps its Windows package ID and gets separate
`.Linux` and `.MacOS` package IDs. Consumers select one platform package per provider.

A provider package carries the exact recursive non-system native dependency closure under
`runtimes/<rid>/native/`. On Unix, imports resolve relative to the packaged module or from the
supported system runtime. This extends the independence and collision rules of ADR-005 to ELF
and Mach-O. Packages may share an effective native load name when the bytes match. The managed
namespace and ownership contract are shared across platforms.

One package with assets for every RID would simplify a multi-target consumer's package reference,
but would require reconciling per-ABI generated managed surfaces and changing existing package
identities. Separate platform packages keep each generated pair intact and allow the Windows
identity to continue. Independent publication per platform would allow a release version with
only some providers ready. A single gate on `main` instead publishes one expected set of package
IDs after all three platforms pass verification. The gate binds artifacts to the candidate commit
and release version and pushes the verified bytes. A partial feed failure is retried with those
same artifacts and version.

Revisit this decision if a shared managed facade is required, a provider needs a different
public profile on a new platform, or an additional target changes the system dependency boundary.
