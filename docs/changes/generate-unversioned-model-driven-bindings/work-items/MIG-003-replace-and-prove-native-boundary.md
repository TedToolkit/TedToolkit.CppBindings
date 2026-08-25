# MIG-003: Replace and prove the generated native boundary

<!-- work-item-format: 2 -->

- Approval: Revised item approved by the user in the current Codex task on 2026-08-25 together with
  the expanded parent contract and three-item delivery map.

## Outcome

The unversioned generated native project builds against the pinned OCCT toolchain and is exercised
successfully by both a strict C11 consumer and the generated managed API across every enabled
transport, ownership, error, lifetime, and cleanup category. The passing replacement then becomes
the only active source/build/test/documentation path.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: generated-project materialization,
  configured native basename/build integration, strict C11 and managed boundary fixtures, removal
  of the ABI-v1 scaffold, current documentation, and migration reconciliation records.
- In scope: compile/link/runtime integration against pinned OCCT; configured-header/include/adapter
  selection proof; representative real value, handle, UTF-8, error, mutation, and cleanup calls;
  same-library release evidence; root build/preset/console fixture migration; active-source and
  current-documentation cleanup; preparation of reconciled Draft package change and work-item map.
- Non-goals: expanding declaration-category coverage, changing generated contracts owned by
  MIG-001, changing loader semantics owned by MIG-002, publishing or packing NuGet, Linux support,
  or authorizing package delivery.
- Likely touchpoints (non-binding): `GenerateCppModule`; generated CMake; root
  `CMakeLists.txt`/`CMakePresets.json`; Console materializer; strict native consumer; Generator and
  Runtime integration tests; root/component READMEs and current interop guide; dependent package
  change/map.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| MIG-001 complete | Complete deterministic generated header, C++ adapter, managed source, manifest/fingerprint, and configured-basename CMake candidate | MIG-001 completion evidence and passing owned-contract proof |
| MIG-002 complete | Generated managed loader that proves exact fingerprint equality before operation export resolution | MIG-002 completion evidence and passing AC-03 boundary proof |
| Native toolchain | .NET SDK, CMake, Ninja, clang-cl/MSVC-compatible toolchain, vcpkg baseline `f89a4a1da4e3176a8d1a14c1825b9b2f98e48843`, and OCCT 8.0.1 `x64-windows` installation | Configure preflight and existing repository native proof |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-02 | Owns | Prove the completed repository has no declaration-specific handwritten authority and consume MIG-001 proof that fixture changes propagate through every replacement layer. |
| AC-05 | Owns | Prove real generated C11 and managed calls preserve OCCT values/mutations, errors, ownership, and exactly-once same-library cleanup without exposing C++. |
| AC-07 | Owns | Prove configured selections determine exact required includes/adapters, exclude unsupported/unselected operations, and build the configured unversioned basename. |
| AC-10 | Supports | Compile the exact sorted per-type source inventory supplied by MIG-001 and prove its definitions link without ODR or symbol collisions. |

<!-- work-item: delivery-constraints -->
## Constraints

- The ABI-v1 proof may be removed only after the generated replacement reaches passing primary
  boundary proof in the same delivery; the candidate must retain an atomic revert boundary.
- Native public declarations remain strict C11/C++ compatible and transport no C++/STL/compiler
  representation. Owned handles, buffers, and diagnostics are released exactly once by the
  allocating library.
- Active production source, build configuration, executable fixtures, generated output, root and
  component READMEs, and non-ADR/non-change guides contain no ABI major/minor mechanism or versioned
  identity after replacement. Historical ADR/change records remain history.
- Package records are reconciled as Draft delivery contracts and require separate explicit user
  approval; this item neither creates a package nor authorizes package delivery.
- Build-directory layout, fixture organization, and edit order remain private choices. Adapter
  translation units follow the parent's one-source-per-type layout and may use only the explicit
  non-type common support source for shared ABI support.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-02 | Acceptance and structural | Component evidence plus bounded repository inspection | MIG-001 fixture changes propagate through every replacement layer, and the completed production tree contains no per-operation catalog, copied adapter/import list, or literal final operation symbol. | Re-run or consume the pinned MIG-001 propagation proof, then inspect production source and embedded resources with bounded forbidden-pattern searches after removing the legacy proof. |
| AC-05 | Acceptance, boundary, and regression | Contract plus Integration | The generated library compiles; strict C11 and generated managed consumers observe correct values/mutations and managed errors; every owned handle, diagnostic, and buffer is released exactly once through its allocating library; no public native declaration exposes C++. | Build the solution and generated native project, run the unversioned CTest consumer, then run Generator and Runtime TUnit projects in Release with TRX output. |
| AC-07 / AC-10 support | Acceptance and boundary | Component plus Integration | Distinct configured header selections emit only their exact required includes/adapters; transitive dependencies do not enroll operations; per-type definitions match the C declarations, compile and link to real OCCT declarations without collisions, and representative generated managed calls execute successfully. | Run Generator selection/source-inventory fixtures; configure with the migrated unversioned CMake preset using the pinned `VCPKG_ROOT`; run `cmake --build --preset <unversioned-preset>` and `ctest --preset <unversioned-preset>`; then run representative generated managed calls. |
| Migration and repository gates | Structural and broader regression | Release build plus bounded inspection | No active versioned scaffold or current-documentation claim remains; the full solution and both TUnit projects pass; package records accurately describe the replacement and remain Draft pending approval. | Run `dotnet build TedToolkit.Occt.slnx -c Release`; run both test projects with `dotnet run ... -c Release --no-build -- --report-trx`; inspect active source/build/fixtures/output/current docs with bounded version-pattern searches; validate both migration and reconciled package records. |

<!-- work-item: definition-of-done -->
## Done

- AC-02, AC-05, and AC-07 have passing structural and real-boundary proof on the supported Windows
  x64 OCCT matrix.
- The generated unversioned path is the only active source, build, executable fixture, generated
  output, and current-documentation truth; obsolete ABI-v1 scaffold files are removed only after
  replacement proof passes.
- Reconciled package change and delivery-map Drafts are ready for independent review and explicit
  approval. The parent migration remains incomplete until that approval is recorded.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

The implementation handoff records candidate revision, changed and removed artifacts,
AC-02/AC-05/AC-07
evidence purpose and shape, exact build/test commands, assertions and results/counts, compiler,
CMake, vcpkg and OCCT prerequisites, cleanup instrumentation evidence, version-pattern inspection,
documentation/migration state, and reconciled package-record digests. Mutable status remains in
`work-items.md`.

## Risks and implementation notes

- The current root CMake materializes and compiles the handwritten ABI-v1 adapter. Preserve a
  passing fallback until the generated C++ source, imports, and loader have all reached proof.
- Windows loader reuse can mask mismatch tests; fixtures need distinct isolated artifact paths or
  process boundaries.
