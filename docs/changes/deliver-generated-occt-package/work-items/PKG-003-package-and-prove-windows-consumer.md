# PKG-003: Package and prove the pinned Windows consumer boundary

<!-- work-item-format: 2 -->

- Approval: None while Draft. The prior map approval is suspended because the public package and
  assembly identity changed to `TedToolkit.Occt.Windows`.

## Outcome

One `TedToolkit.Occt.Windows` NuGet package restores and runs from an isolated `net8.0`
`win-x64` consumer using only declared managed dependencies and the packaged exact-match native
closure. Its managed assembly is also `TedToolkit.Occt.Windows`, while generated public types remain
under `TedToolkit.Occt`. It carries complete license evidence, rejects unsupported RIDs before native
probing, and documents the pinned compiler-coupled layout and generated coverage truthfully.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: exact `TedToolkit.Occt.Windows` package ID and assembly name; `TedToolkit.Occt` generated
  namespace; package metadata and assets; Runtime dependency; `runtimes/win-x64/native`; recursive
  native dependency reconciliation; exact managed/native candidate pairing; license/notice
  manifest; clean feed/cache and sanitized native-loading journey; module-origin evidence;
  unsupported-RID behavior; coverage, layout, ownership, and platform documentation.
- Non-goals: changing generated public/native behavior, layout eligibility, owner APIs, Linux or
  another RID, an unsuffixed public `TedToolkit.Occt` binding package/assembly, static-linking
  policy, remote publication, or independently upgradeable native artifacts.
- Likely touchpoints (non-binding): package project and props, native staging, RID resolution,
  package inspection, isolated-consumer fixtures, README/package documentation, and license
  manifests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| Approved parent and item | Package, support, compatibility, and journey boundaries |
| PKG-001 complete | Coverage report and unsupported-surface summary |
| PKG-002 complete | Public assembly, exact-match native candidate, Runtime dependency, manifest/fingerprint, API baseline, and passing boundary proof |
| Managed target baseline | Active architecture `net8.0` target and compatibility claim |

<!-- work-item: contract-coverage -->
## Contract responsibility

- Own parent AC-04 and AC-05.
- Ship the managed and native artifacts from one verified generation; never combine outputs from
  different OCCT/compiler/layout/ownership manifests.
- Include every runtime native dependency and applicable license/notice exactly once; sanitized
  consumption must prove that no external OCCT or developer-tool path is used.
- Documentation states `win-x64`, OCCT 8.0.1, the complete pinned native build identity, minimum .NET version,
  exact-match coupling, `TedToolkit.Occt.Windows` package/assembly identity, `TedToolkit.Occt`
  namespace default, minimal Runtime role, coverage dispositions, generic closed-type support, and
  ownership duties.
- Package inspection proves no unsuffixed `TedToolkit.Occt` binding package or managed assembly is
  shipped and does not infer support for another Windows RID from the Windows family name.
- Unsupported RIDs fail with an actionable managed exception before library probing.
- Remote push, signing, release announcement, and another platform remain separate authorized work.

<!-- work-item: delivery-constraints -->
## Constraints

- The nupkg contains only the exact verified PKG-002 artifact set and truthful PKG-001 coverage.
- Private staging and inspection organization remains open when isolated consumption, dependency,
  license, and module-origin evidence remains reproducible.

<!-- work-item: proof-plan -->
## Proof

| Parent contract | Evidence purpose and shape | Observable proof |
| --- | --- | --- |
| AC-04 | Acceptance/journey End-to-end | Clean isolated `TedToolkit.Occt.Windows` PackageReference restore, build, and representative `TedToolkit.Occt` namespace exact-layout calls succeed on `net8.0` with the minimal Runtime dependency and without vcpkg, OCCT, CMake, Clang, or repository outputs |
| AC-05 package closure | Acceptance/boundary Contract | Package ID, assembly name, absence of an unsuffixed binding artifact, nupkg inventory, dependency scan, module origin, managed dependency closure, license manifest, and support docs match the verified candidate |
| AC-05 unsupported RID | Acceptance/regression Component | An unsupported RID fails before any native library probe and reports the supported `win-x64` scope |

Pack to an isolated local feed, clear or isolate NuGet and native search inputs, restore/build/run the
minimum consumer, inspect the nupkg and native dependency closure, and run unsupported-RID fixtures.

<!-- work-item: definition-of-done -->
## Done

The exact `TedToolkit.Occt.Windows` candidate passes isolated consumption; package and assembly
identity are exact and no unsuffixed binding artifact ships; contents and hashes correspond to one
verified fingerprint; all managed/native dependencies and notices are present; module-origin and
unsupported-RID proof pass; documentation matches the pinned `win-x64` layout and actual coverage;
no remote publication or additional platform claim occurs.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record candidate revision and package hash, nupkg inventory, consumer and inspection commands,
restore/build/run results, dependency and license closure, module origin, unsupported-RID assertion,
support-documentation state, and any skipped environment prerequisite. Publication evidence is not
part of this item.
