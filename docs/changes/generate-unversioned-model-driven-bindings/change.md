# Generate exact-layout unversioned bindings from one semantic model

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: approved -->
<!-- delivery-shape: multi-item -->

- Priority: P1
<!-- approval-source: Explicit maintainer approval in the Codex task on 2026-08-26. -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

For every supported OCCT declaration, one `RecordModel`-centered semantic and physical model emits
an unversioned, exactly matched C/C++/C# binding chain whose object ownership and every operation's
lifetime flow are complete before emission. The current production path still contains a
handwritten ABI-v1 boundary, while the current C# generator emits explicit field offsets and does
not classify every object for the approved value, `Handle<T>`, or `Owned<T>` architecture. The
replacement must not consume ABI-v1 as an input, oracle, fallback, or compatibility target.

<!-- section: scope -->
## Scope and non-goals

- In scope: one declaration authority; generated C11-compatible exports and C++ adapters; exact
  sequential C# object layouts; physical padding and opaque storage; interface inheritance without
  a managed `BaseType`; supported closed generic projection; extension-method instance behavior;
  transient `Handle<T>` and non-transient `Owned<T>` integration through Runtime's public API;
  one generated process-lifetime native-module loader and static managed function table;
  deterministic manifests, fingerprints, names,
  dispositions, native build descriptions, a validated configurable C# root namespace,
  source-only completion, optional native-library compilation, and removal of active ABI-v1
  scaffolding.
- In scope: one Model-owned, compiler-backed ownership classification for every object; complete
  receiver, parameter, result, borrowing, transfer, construction, copy, destruction, and cleanup
  semantics for every emitted operation; and fail-closed rejection when those facts are incomplete
  or contradictory.
- In scope: one generated adapter source per canonical owning type, plus type-independent support
  sources; complete cross-layer validation; native layout and lifecycle facts attached to the
  originating declaration rather than copied into a second operation catalog.
- Non-goals: increasing the selected OCCT header universe, packaging or publishing NuGet, Linux or
  another ABI, consumer-time generation, changing the approved public owner categories, or changing
  the completed managed exception hierarchy. Runtime's declaration-agnostic `Handle<T>`,
  `Owned<T>`, exceptions, and analyzers remain handwritten and are not generated from the Model.
- Compatibility: no supported generated binding package has been released. Generated managed and
  native artifacts remain one inseparable exact-match set for the pinned `win-x64`, OCCT, compiler,
  architecture, and layout inputs; the dependent public package/assembly identity is
  `TedToolkit.Occt.Windows`, while its default C# namespace remains `TedToolkit.Occt`.
- Preserved behavior: public exports remain C11-compatible and contain no C++ class, reference,
  template, STL, or exception type; native exceptions are projected through the accepted managed
  error boundary; ownership and cleanup remain explicit and same-library; incomplete operations
  and layouts fail closed without partial output.
- Migration recovery: ABI-v1 may remain physically present only as an inactive recovery artifact
  until the generated replacement passes. No replacement generation, build, or acceptance proof
  may consume it; completion removes it from active source, build, fixtures, output, and current
  documentation.

### Model and projection boundary

- `RecordModel`, `MethodModel`, `ParameterModel`, and `TypeModel` remain the only declaration graph.
  Layout, transport, ownership, conversion, error, and cleanup facts reference that graph and do
  not copy declaration identity into another catalog.
- Parsing and normalization complete the Model before emission. C# and C++ emitters consume only
  that Model and never parse headers, generated text, or each other's output. Native compilation
  consumes the completed C++ source set and generated build description as an optional later stage.
- The physical type projection records native size, alignment, packing, complete-object segments,
  source and hidden fields, base-subobject evidence, template arguments, ownership category, and
  construction/destruction operations required by every emitter and the exact-match manifest.
- Model normalization assigns exactly one proved object category before emission: a
  `Standard_Transient` descendant with complete intrusive-reference semantics uses `Handle<T>`; a
  non-transient object with proved safe value copying and no required native cleanup is a plain
  value; and another non-transient object uses `Owned<T>` only when construction, copying,
  destruction, alignment, and same-library cleanup are complete and representable. Otherwise the
  declaration is unsupported.
- The Model also records each operation's receiver, parameter, result, borrowing, transfer,
  construction, and cleanup semantics. Emitters consume those facts and never infer or override
  ownership. An incomplete lifetime flow rejects the complete operation without partial output.
- Each declaration and operation receives exactly one generated, unsupported, or excluded
  disposition. No emitter rebuilds or independently filters a supported-operation list.
- Every required operation and cleanup export receives one deterministic generated function-table
  slot derived from the same completed Model identity as its native symbol and managed call site.
- ABI-v1, legacy fixtures, emitted text, and handwritten bindings are never semantic inputs,
  comparison authorities, fallbacks, or compatibility targets for any generated layer.
- The C header uses pointer-compatible storage transports for exact-layout objects. C++ adapters
  perform placement construction, destruction, intrusive reference operations, pointer adjustment,
  and exception containment without exposing C++ declarations in the export surface.
- The validated `CSharpNamespace` generation option applies one root namespace to every generated
  C# artifact and defaults to `TedToolkit.Occt`; it changes managed API identity without changing
  canonical C++ or native export identity.
- `CSharpNamespace` is a non-empty dot-separated sequence of valid C# identifiers. Keyword segments
  are emitted with deterministic identifier escaping; empty segments, surrounding separators, and
  invalid identifier characters are rejected with `ArgumentException` before any output directory
  is cleaned or materialized. The configured spelling is otherwise preserved deterministically.
- One machine-readable native build identity pins the OCCT version, vcpkg baseline, triplet,
  architecture, compiler and toolset, CRT linkage, packing, and layout-affecting OCCT/compiler
  options used by layout acquisition, native compilation, manifests, and package evidence.

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Generation authority | Handwritten ABI-v1 and parsed operation authorities coexist | Every declaration-specific binding and build artifact traverses one normalized Model; ABI-v1 is never an input, oracle, fallback, or target | Unsupported siblings remain isolated |
| OB-02 | Managed object layout | Records use explicit offsets and old architecture treats many objects as semantic or opaque projections | Every supported C++ object is an exact unmanaged sequential struct with generated physical segments | Native compiler data remains authoritative |
| OB-03 | Inheritance and behavior | Descriptor classes and class inheritance are assumed by dependent records | Interfaces express inheritance; complete derived storage has no managed `BaseType`; instance operations are extensions | OCCT names and source behavior remain recognizable |
| OB-04 | Templates | Closed C++ template instances have no approved generic managed rule | One generic physical graph represents every proved supported specialization, otherwise closed projection or rejection applies | Unknown closed types fail before native access |
| OB-05 | Ownership classification and generated API | The old plan assumes a covariant descriptor `Handle<out T>` and opaque non-transient objects | The Model uniquely classifies every supported object as value, transient `Handle<T>`, or non-transient `Owned<T>` and supplies every operation lifetime flow; ambiguity is unsupported | Handle release and Owned destruction each occur exactly once |
| OB-06 | Contract identity and dispatch | ABI-v1/version state or incomplete manifests gate loading | One unversioned fingerprint includes every layout, closed generic, operation, ownership classification, lifetime flow, error, cleanup, export, and function-table slot fact; after equality, one complete generated table is published | Mismatch or a missing required export fails before table publication or operation invocation |
| OB-07 | Generator staging and configuration | Source emitters exist, but namespace selection and native compilation are not a complete governed path | Model-only C#/C++ emitters honor one configured C# root namespace; source generation may stop before an optional native build | Package delivery still requires the compiled and verified native artifact |

<!-- acceptance-case: AC-01 -->
### AC-01 — One declaration model emits every compatible layer

```gherkin
Scenario: Generate a fully supported parsed operation
  Given one parsed declaration has complete physical, transport, ownership, conversion, error, and cleanup semantics
  When generation completes
  Then its C declaration, C++ adapter, managed import, public operation, manifest row, and native build entry share one canonical identity
  And its generated function-table slot and exact unmanaged Cdecl call signature share that identity
  And the configured root namespace is applied coherently to every generated C# artifact
  And C# and C++ emission consume only the completed Model and can finish without native compilation
  And no ABI-v1 artifact, handwritten catalog, final symbol, adapter, import list, emitted text, or second declaration graph supplies facts or expected results
```

<!-- acceptance-case: AC-02 -->
### AC-02 — Exact sequential object layouts are proved

```gherkin
Scenario: Generate a supported C++ object type
  Given the pinned compiler supplies its complete native size, alignment, packing, fields, base storage, and hidden segments
  When its managed projection is generated
  Then every emitted object projection is an unmanaged LayoutKind.Sequential struct with typed, padding, and aligned opaque segments
  And it contains no FieldOffsetAttribute, managed BaseType field, or managed class inheritance
  And managed size, alignment, and every physical segment position equal the native results
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Supported templates remain generic only under closed proof

```gherkin
Scenario: Generate NCollection_Array1 for several supported element types
  Given one generic physical graph represents each selected native specialization
  When the managed bindings are generated
  Then one NCollection_Array1<T> struct is emitted
  And every supported closed T has matching identity, size, alignment, layout, construction, copy, and destruction evidence
  And an unregistered or mismatched T is rejected before native access
```

<!-- acceptance-case: AC-04 -->
### AC-04 — Model classification drives representation, behavior, and lifetime

```gherkin
Scenario: Generate trivial, transient, and non-transient RAII types
  Given each object and operation has compiler-backed representation, ownership, and lifetime-flow evidence
  When its public managed surface is emitted
  Then the Model assigns exactly one value, Handle, or Owned category before any emitter runs
  And a proved trivial non-transient value uses ordinary struct construction without disposal
  And a proved Standard_Transient object with complete intrusive lifecycle is an exact struct owned only by Handle<T>
  And every eligible non-transient RAII struct with complete construction, copy, destruction, alignment, and cleanup semantics, including eligible TCollection_* types, implements IOcctRaii and is owned only by Owned<T>
  And trivial and Standard_Transient structs do not implement IOcctRaii
  And non-transient RAII values are placement-constructed directly in managed storage held by Owned<T>, with its address stabilized by fixed during native use
  And Handle<T> and Owned<T> expose no public ownership inheritance or conversion
  And const value operations use in receivers while mutating value operations use ref receivers
  And owner operations use Handle<T> or Owned<T> extensions without public Value extraction
  And generated wrappers reference the Runtime owner definitions without emitting them or receiving friend access
  And receiver, parameter, result, factory, borrowing, transfer, copy, release, destruction, and cleanup code is derived only from the Model classification and operation lifetime flow
  And an ambiguous or incomplete classification or lifetime flow produces an unsupported disposition and no callable partial operation
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Exact matching includes memory and lifetime interpretation

```gherkin
Scenario: Load generated artifacts from different physical contracts
  Given managed and native artifacts differ in any layout, compiler identity, closed template, ownership category, operation lifetime flow, or cleanup fact
  When managed initialization runs
  Then it throws BadImageFormatException before publishing the operation table or invoking an OCCT operation
  And a matching artifact resolves every required export into one complete private static managed table
  And a missing required export fails before that table is published
  And matching artifacts invoke representative operations successfully through exact typed table slots
```

<!-- acceptance-case: AC-06 -->
### AC-06 — The replacement boundary builds and runs without active ABI-v1 artifacts

```gherkin
Scenario: Exercise the generated replacement
  Given the replacement is generated from pinned real OCCT headers
  When native compilation is selected and its strict C11 and managed consumers run
  Then representative layout, value, generic, handle, RAII, error, mutation, and cleanup categories match OCCT behavior
  And every owned resource is released once through its allocating library
  And generation, build, and proof have not consumed ABI-v1 as an input, oracle, fallback, or compatibility target
  And active source, build, fixtures, output, and current documentation contain no ABI-v1 path
```

<!-- acceptance-case: AC-07 -->
### AC-07 — Invalid managed namespaces fail before output mutation

```gherkin
Scenario: Reject an invalid configured C# namespace
  Given an existing generation output and a CSharpNamespace with an empty, malformed, or invalid identifier segment
  When generation begins
  Then it throws ArgumentException before cleaning or materializing any output
  And the existing output remains unchanged
```

<!-- acceptance-case: AC-08 -->
### AC-08 — Incomplete native build identity fails before layout or output work

```gherkin
Scenario: Reject an incomplete pinned native build identity
  Given the OCCT, toolset, CRT, packing, architecture, or another layout-affecting identity field is missing or ambiguous
  When generation begins
  Then it reports a deterministic unsupported configuration before acquiring native layout or cleaning or materializing output
  And no declaration is emitted with an assumed identity
```

## Constraints and risks

- [GEN-01](../../principles/README.md#gen-01-generate-every-binding-layer-from-one-semantic-source),
  [GEN-02](../../principles/README.md#gen-02-reproduce-every-supported-native-object-layout-exactly),
  [GEN-03](../../principles/README.md#gen-03-separate-native-representation-from-ownership-and-behavior),
  [GEN-04](../../principles/README.md#gen-04-keep-the-shared-runtime-minimal-and-declaration-agnostic),
  and the [generated binding architecture](../../architecture/generated-binding-system.md) govern
  this migration.
- [ADR-001](../../adr/ADR-001-native-release-binding/README.md), accepted by the maintainer on
  2026-08-26 and pinned here to uncommitted blob
  `ed54eb7a1a8d4b26166e395f6da6d907b03263d5`, governs Windows loading, table storage, dispatch, and
  finalizable-owner cleanup.
- The exact `net8.0` public Handle and Owned construction and non-owning `Value` contracts remain a
  start condition for finalizing generated ownership signatures.
- Runtime contains only declaration-agnostic shared mechanisms. Generated declarations, layouts,
  imports, closed-generic registrations, expected fingerprints, and type-specific adapters remain
  in each generated wrapper assembly. Generation emits no Runtime source and requires no
  `InternalsVisibleTo` or assembly-name privilege.
- The generated artifact set records and derives from one machine-readable
  platform/architecture/compiler/layout identity. A missing or partially pinned identity fails
  before layout acquisition or output materialization.
  It must not be reused as the managed input for another unproved RID or platform package.
- A compiler or CLR layout that cannot reproduce native alignment or physical segments is an
  unsupported disposition, not authority to emit `FieldOffset`, `Pack = 1`, or a translated value.
- Ownership classification is exhaustive only for supported objects, mutually exclusive, and
  compiler-backed. Unknown inheritance, unsafe value copying, incomplete construction or
  destruction, unproved direct-field alignment, or incomplete operation lifetime flow produces an
  unsupported disposition before any callable output.
- ABI-v1 and handwritten legacy fixtures may provide migration recovery only while inactive. They
  do not supply generation input, expected output, comparison truth, fallback behavior, or a
  compatibility obligation.
- Active code and current guides describe only the accepted replacement architecture after
  migration.
- Generated initialization uses the Win32 loader boundary, resolves only the fixed fingerprint
  bootstrap before exact-match equality, then privately validates and atomically publishes one
  process-lifetime managed `IntPtr[]` containing every required operation and cleanup address.
- Ordinary generated calls use exact typed unmanaged Cdecl pointers read from deterministic slots.
  Generated factories copy matching release and destructor pointers into Runtime owners; owners do
  not retain a table or index and never consult mutable global state during finalization.
- Escalate if implementation needs another declaration graph, a handwritten final artifact, an
  independently versioned native ABI, a public raw pointer, ownership in a struct, a fourth
  ownership category, emitter-side reclassification, ABI-v1 dependence, or a layout or lifetime
  flow that cannot be proved on the pinned matrix.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../implement-managed-owned-lifetime/change.md contract=AC-01 -->
<!-- change-prerequisite: PRE-02 source=../implement-managed-owned-lifetime/change.md contract=AC-02 -->
<!-- change-prerequisite: PRE-03 source=../implement-managed-owned-lifetime/change.md contract=AC-03 -->
<!-- change-prerequisite: PRE-04 source=../implement-managed-owned-lifetime/change.md contract=AC-04 -->
<!-- change-prerequisite: PRE-05 source=../implement-managed-owned-lifetime/change.md contract=AC-05 -->

| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | Public declaration-agnostic `Owned<T>` and `IOcctRaii` contract | `../implement-managed-owned-lifetime/change.md`, AC-01 | Source contract is completed on the selected Git baseline |
| PRE-02 | Aligned placement construction and pre-success failure contract | `../implement-managed-owned-lifetime/change.md`, AC-02 | Source contract is completed on the selected Git baseline |
| PRE-03 | Exactly-once disposal and finalization contract | `../implement-managed-owned-lifetime/change.md`, AC-03 | Source contract is completed on the selected Git baseline |
| PRE-04 | Explicit native copy/clone contract | `../implement-managed-owned-lifetime/change.md`, AC-04 | Source contract is completed on the selected Git baseline |
| PRE-05 | Fail-closed unsupported ownership admission | `../implement-managed-owned-lifetime/change.md`, AC-05 | Source contract is completed on the selected Git baseline |

<!-- section: delivery-brief -->
## Delivery disposition

The revised five-item controlled map separates independently verifiable outcomes: MIG-001 owns the
canonical semantic/physical model, exact managed storage projection, object ownership
classification, and operation lifetime flow; MIG-002 owns the classification-driven managed public
API, deterministic function-table slots and typed dispatch, and namespace surface; MIG-003 owns the
Model-only generated C/C++ boundary, manifest, fingerprint, and native build description; MIG-004
owns exact-match loading, complete table validation, and one-time publication; and MIG-005 owns
optional native compilation, real integration, proof of no ABI-v1 dependency, and removal of the
legacy boundary. The authoritative `work-items.md` owns every item lifecycle and approval state.

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=component -->
<!-- primary-proof: AC-02 purpose=boundary shape=integration -->
<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-04 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-05 purpose=boundary shape=integration -->
<!-- primary-proof: AC-06 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-07 purpose=boundary shape=component -->
<!-- primary-proof: AC-08 purpose=boundary shape=component -->

| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Controlled and real declarations produce one complete deterministic C/C++/C# chain only from the completed Model; structural instrumentation observes no ABI-v1, emitted-text, or handwritten authority | Run the Generator Release source-only Component suite and inspect the clean generated inventory and emitter inputs |
| AC-02 | Primary | Native and managed probes agree for size, alignment, packing, and every physical segment of every emitted object | Generate and run the exhaustive layout-contract matrix against the pinned compiler |
| AC-03 | Primary | Proved `NCollection_Array1<T>` specializations share one generic definition and unregistered or mismatched `T` fails before native access | Run the Generator generic Contract suite and representative real OCCT specializations |
| AC-04 | Primary | Classification fixtures cover value, Handle, Owned, ambiguous, and incomplete cases; generated functions derive receiver, parameter, result, construction, copy, release, destruction, and cleanup shapes only from Model facts and lifecycle calls complete exactly once | Run the Generator and Runtime Release TUnit ownership suites plus generated native lifecycle fixtures |
| AC-05 | Primary | Matching artifacts publish one complete deterministic function table; mutation of any layout, classification, operation lifetime, compiler, error, cleanup, export, or slot fact, a missing bootstrap, or a missing required export fails before table publication or operation invocation | Run canonical-manifest mutation and instrumented native-loader/table fixtures |
| AC-06 | Primary | The generated native project, strict C11 consumer, and managed consumer run without consuming ABI-v1, and structural inspection finds no active legacy path | Build the generated native project in Release, run its CTest consumer and managed integration suite, then inspect source, build, fixtures, output, and current documentation |
| AC-07 | Primary | Valid namespaces apply coherently, while every malformed namespace throws `ArgumentException` before output mutation and preserves existing output | Run the Generator namespace-validation Component matrix against a pre-populated output directory |
| AC-08 | Primary | Removing or changing each required native build-identity field fails deterministically before layout acquisition or output mutation | Run the Generator native-build-identity Component mutation matrix with layout/output instrumentation |

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-08 pass on the pinned `win-x64` matrix; the approved public owner and receiver
contracts are recorded; the generated replacement is deterministic and exact-match; every supported object and
closed generic has native/managed layout, classification, operation-flow, and lifecycle proof;
generation and proof have not consumed ABI-v1; active ABI-v1 and old descriptor or explicit-offset
paths are removed; the dependent `TedToolkit.Occt.Windows` package records consume the exact
candidate; and no package publication or additional platform claim is included.
