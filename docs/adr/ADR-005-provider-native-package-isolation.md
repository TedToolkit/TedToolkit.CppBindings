# ADR-005: Isolate provider-native package assets

- Status: Accepted
- Date: 2026-09-07
- Decision owner: TedToolkit maintainers
- Decision scope: Native runtime asset selection and collision handling for independently consumable
  Windows provider packages.
- Applicable product intent: None
- Applicable principles: [GEN-01, GEN-03, and GEN-04](../principles/README.md)
- Supersedes: None
- Superseded by: None
- Approval source: On 2026-09-07, the maintainer explicitly requested that every provider package
  be self-contained without unrelated native DLLs and authorized repairing the OCCT package rule.

## Decision at a glance

Each Windows provider package carries its uniquely named binding module plus only its recursively
resolved non-system runtime imports; packages may overlap on a native asset name only when the
bytes are identical.

## Context and decision question

The OCCT Windows project currently includes every DLL in the configured vcpkg triplet `bin`
directory. Installing another library into that shared triplet can therefore change the OCCT NuGet
contents even when the OCCT wrapper and its real imports are unchanged. The CGAL build already
stages a recursively inspected dependency closure.

The decision is whether provider packages should copy a build-environment directory, statically
absorb every dependency, centralize native assets in one shared package, or derive an isolated
runtime closure from each provider's compiled binding module.

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | A provider package must not acquire another provider merely because both were built from one vcpkg installation | Maintainer requirement | Must |
| Hard constraint | Allocation, destruction, exception cleanup, and native calls continue to use the matching provider module | [GEN-03](../principles/README.md) | Must |
| Hard constraint | Managed and native generated artifacts remain an inseparable exact-match set | [Generated binding architecture](../architecture/generated-binding-system.md) | Must |
| Decision driver | Independently published provider packages must coexist in one consumer process | Maintainer requirement | High |
| Decision driver | Build and verification must remain bounded on a workstation with limited free disk space | Maintainer requirement | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Copy every DLL from the vcpkg triplet | Documented by the current OCCT project; high confidence | No | Package contents depend on unrelated installed ports and grow without a provider-owned reason | Rejected |
| Require one statically linked physical DLL per provider | Toolchain and dependency models differ by library; high confidence | Partly | Removes app-local dependencies but changes linkage, licensing, size, and supported ABI instead of solving asset selection | Rejected |
| Publish one shared native-dependencies package | Architectural analysis; medium confidence | Partly | Deduplicates files but couples otherwise independent providers to one release train | Rejected |
| Stage the recursive non-system import closure per provider and compare overlapping names by hash | Existing CGAL packaging behavior and Windows import inspection; high confidence | Yes | Identical dependencies may appear in more than one NuGet archive, and every package build must inspect its native closure | Selected |

## Decision

Every Windows provider package derives native runtime assets from its compiled provider binding
module. It includes that uniquely named module and recursively follows non-system DLL imports. It
does not copy an installation directory, use a provider allow-list as a substitute for recursion,
or include build-only libraries.

An independently restored consumer may receive the same native filename from more than one
provider package only when all copies have the same SHA-256 hash. A differing same-name asset is a
package verification failure. The remedy is to align the pinned dependency baseline or change the
provider's supported linkage; silently selecting one copy or renaming a third-party DLL is not
supported.

"Self-contained" means that the NuGet package owns its complete runtime dependency closure. This
decision does not require all native code to be statically linked into one physical DLL.

## Why this decision now

Manifold and FCL are being added beside OCCT and CGAL. The existing OCCT wildcard would allow those
installations to contaminate the OCCT package without any OCCT source or import change. Recursive
closure keeps package identity tied to actual native behavior and preserves independent provider
release boundaries without imposing a common native package.

## Evidence and links

- [OCCT Windows project](../../src/providers/occt/TedToolkit.CppBindings.Occt.Windows/TedToolkit.CppBindings.Occt.Windows.csproj)
- [Shared Windows generation tool](../../src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/TedToolkit.CppBindings.Windows.Generation.Tool.csproj)
- [C++ bindings platform architecture](../architecture/cpp-bindings-platform.md)
- [Native function-table decision](ADR-003-native-function-table-bootstrap.md)

## Consequences and accepted trade-offs

- Installing an unrelated vcpkg port cannot add a DLL to a provider NuGet package.
- Each provider remains independently installable and carries all required app-local native files.
- Identical shared dependencies may consume bytes in multiple NuGet archives, but they cannot
  introduce ambiguous runtime behavior.
- Package verification requires native import inspection on the target toolchain.
- Static linkage remains a provider-specific option only when separately supported and proved.

## Downstream delivery constraints

- Give every provider binding module a unique provider-qualified basename.
- Derive and record the complete recursive non-system import closure from the packaged module.
- Reject missing imports, unrelated native assets, and differing same-name assets across a tested
  package set.
- Preserve current managed APIs, native semantics, and same-module cleanup while changing package
  asset selection.
- Run native package builds serially, use provider-isolated scratch roots, and retain compact proof
  rather than duplicate build trees.

## Exit requirements

A replacement must keep provider packages independently consumable, prove complete runtime imports,
prevent unrelated assets, and define deterministic handling for every same-name native file.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Apply the closure rule to OCCT and reuse it for subsequent providers | TedToolkit maintainers | Before publishing another Windows provider | Open |
| Reassess shared native asset distribution | TedToolkit maintainers | Identical duplicated assets materially dominate package size | Open |
| Reassess linkage policy | TedToolkit maintainers | A required dependency cannot coexist app-locally under one filename | Open |
