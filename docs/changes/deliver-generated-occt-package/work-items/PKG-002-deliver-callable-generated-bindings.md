# PKG-002: Deliver complete exact-layout callable bindings

<!-- work-item-format: 2 -->

- Approval: None while Draft. The prior map approval is suspended because the public assembly
  identity changed to `TedToolkit.Occt.Windows`.

## Outcome

The completed Generator and Runtime outputs are materialized for every declaration marked generated
by PKG-001 into the public `TedToolkit.Occt.Windows` assembly and exact-match native artifact. The
candidate has one compatible layout, interface, operation chain, ownership category, error contract,
manifest identity, `TedToolkit.Occt` root namespace, minimal Runtime dependency, and public API
baseline.

<!-- work-item: scope -->
## Scope and non-goals

- In scope: release generation over the PKG-001 eligible set; full managed/native materialization;
  exhaustive layout and registered-generic contract probes; public API baseline; package-default
  namespace propagation; native compile/link; exact-match validation; complete generated static
  function-table publication and typed dispatch; Runtime admission inspection;
  and representative real operation, ownership, error, and cleanup journeys.
- Non-goals: changing PKG-001 coverage identity; changing Generator models, emitters, manifests,
  generated loaders, Runtime owners/exceptions, or any completed public contract; adding a layout/lifetime
  category; package/RID composition; Linux; an unsuffixed binding assembly; or publication.
- Likely touchpoints (non-binding): release-generation configuration, generated public/native
  candidate materialization, exhaustive layout probes, public API baselines, integration fixtures,
  and candidate evidence.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite | Required input |
| --- | --- |
| Approved parent and item | Public, native, layout, ownership, compatibility, and proof boundaries |
| PKG-001 complete | Canonical generated declaration set and coverage identities |
| Runtime ownership complete | Verified separate Runtime-defined `Handle<T>` and `Owned<T>` public surfaces usable by any wrapper assembly |
| Revised migration complete | Verified exact-layout generated managed/native pipeline and exact-match loader |
| Managed target baseline | Active architecture `net8.0` public assembly and consumer contract |

<!-- work-item: contract-coverage -->
## Contract responsibility

- Own parent AC-02 and AC-03 and supply callable/API evidence for AC-04 and AC-05.
- Every supported object is an unmanaged sequential struct with exact native size, alignment, and
  physical segments and no `FieldOffset`, managed `BaseType`, or descriptor class. Every shipped
  object and registered closed generic passes the exhaustive layout contract.
- Interfaces express inheritance. Base-declared extensions and parameters accept derived values or
  handles through generated constraints and native-proved pointer adjustment.
- `NCollection_Array1<T>` and another selected template prove generic reuse over several registered
  closed `T` values; unsupported closed types fail before native memory access.
- Trivial values use ordinary C# construction and no disposal. `Handle<T>` owns only transient
  intrusive references and stores no object memory. Eligible non-transient RAII structs, including
  eligible `TCollection_*` types, implement Runtime's `IOcctRaii`; `Owned<T>` is constrained by
  `unmanaged, IOcctRaii`, placement-constructs, and explicitly
  clones non-transient RAII values in managed storage stabilized by `fixed` during native use, then
  destructs them through the matching native library without intrusive release or native storage
  free. The two owners expose
  no public hierarchy.
- Instance operations use extension syntax and expose no routine public `.Value`, raw pointer,
  `IntPtr`, public retain, or bitwise-copy ownership path.
- `TedToolkit.Occt.Windows` references the single Handle/Owned definitions in Runtime and uses only
  their ordinary public construction and `Value` contracts. It emits no Runtime source
  and receives no `InternalsVisibleTo`, reflection, private-member, or assembly-name privilege.
- After exact-match equality, the generated wrapper resolves every required operation and cleanup
  export into one private static managed table and publishes it only when complete. Ordinary calls
  use exact typed slots; Handle and Owned factories copy matching cleanup pointers into Runtime
  owners, which retain no generated table or index.
- Every generated C# artifact uses the package-default `TedToolkit.Occt` root namespace. Concrete
  declarations, imports, native symbols, closed-generic registrations, layouts, expected
  fingerprints, and target-specific adapters remain in generated output rather than Runtime.
- The managed assembly name is `TedToolkit.Occt.Windows`; its public API baseline and metadata prove
  that assembly identity independently from the source namespace.
- Native failure projection preserves the existing public exception hierarchy. Exact matching
  includes every layout and lifecycle identity.

<!-- work-item: delivery-constraints -->
## Constraints

- Governed by GEN-01 through GEN-04 and the active generated binding architecture. An unproved
  layout, owner, closed generic, Runtime addition, or native cleanup origin is unsupported rather
  than a partial public binding.
- A discovered defect that requires a Generator, Runtime, public API, layout, ownership, manifest,
  loader, or exception contract change returns to its owning change and renewed approval. This item
  may change only release materialization and package-candidate evidence inside those contracts.

<!-- work-item: proof-plan -->
## Proof

| Parent contract | Evidence purpose and shape | Observable proof |
| --- | --- | --- |
| AC-02 | Acceptance/compatibility Contract plus Integration | Exhaustive probes prove every shipped object and registered generic layout; public and assembly baselines prove the `TedToolkit.Occt.Windows` identity, exact structs, interfaces, `in`/`ref` extensions, static factories, ordinary Runtime owner use, no friend access, `TedToolkit.Occt` namespace, minimal Runtime surface, and absence of prohibited projections |
| AC-03 | Acceptance/boundary Integration | Representative value copying, typed table dispatch, Handle alias/dispose/finalize, and Owned placement/clone/destruction prove that Handle release and Owned destruction each use the producing module address exactly once while owners retain no table/index and Owned performs no intrusive release or native storage free |
| Generic boundary | Acceptance/regression Contract plus Integration | Registered generic specializations share one type and pass layout/lifecycle probes; unknown `T` fails before native access |
| Error boundary | Boundary regression Integration | Representative `Standard_Failure`, ordinary, and unknown errors preserve accepted managed diagnostics and clear native storage once |
| Determinism | Structural regression | Identical pinned inputs reproduce public source, native source, manifest, fingerprint, and API baseline byte-for-byte |

Materialize the completed generator output for the full eligible inventory, build the public and
Runtime assemblies and generated native project in Release, run the exhaustive layout matrix, run
Generator and Runtime TUnit projects with TRX output, and execute representative generated calls
against the real native candidate.

<!-- work-item: definition-of-done -->
## Done

All owned contracts pass; the `TedToolkit.Occt.Windows` assembly contains only approved exact-layout,
interface, generic, extension, Handle, Owned, and exception shapes under the `TedToolkit.Occt`
namespace; every generated operation has a complete cross-language chain; all ownership and
diagnostics clean up through the producing library; PKG-003 receives the verified
managed/native/package inputs; no packaging or publication is performed.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record candidate revision, public/native artifacts and baselines, owned contract IDs, commands,
layout/generic/API/lifecycle/error assertions, test counts, pinned native resources, cleanup and
module origins, documentation state, and exact package inputs supplied to PKG-003.
