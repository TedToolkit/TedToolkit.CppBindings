# Generate exact-layout unversioned bindings from one semantic model

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: draft -->

- Priority: P1
- Approval: The revised parent and five-item delivery map require explicit approval before
  implementation.

<!-- section: goal-rationale -->
## Goal and rationale

For every supported OCCT declaration, one `RecordModel`-centered semantic and physical model emits
an unversioned, exactly matched C/C++/C# binding chain. The current production path still contains a
handwritten ABI-v1 conformance boundary, while the current C# generator emits explicit field
offsets and does not implement the exact-layout ownership architecture required by GEN-02, GEN-03,
and the current generated binding architecture.

<!-- section: scope -->
## Scope and non-goals

- In scope: one declaration authority; generated C11-compatible exports and C++ adapters; exact
  sequential C# object layouts; physical padding and opaque storage; interface inheritance without
  a managed `BaseType`; supported closed generic projection; extension-method instance behavior;
  transient `Handle<T>` and non-transient `Owned<T>` integration through Runtime's public API;
  deterministic manifests, fingerprints, names,
  dispositions, native build descriptions, a validated configurable C# root namespace,
  source-only completion, optional native-library compilation, and removal of active ABI-v1
  scaffolding.
- In scope: one generated adapter source per canonical owning type, plus type-independent support
  sources; complete cross-layer validation; native layout and lifecycle facts attached to the
  originating declaration rather than copied into a second operation catalog.
- Non-goals: increasing the selected OCCT header universe, packaging or publishing NuGet, Linux or
  another ABI, consumer-time generation, changing the approved public owner categories, or changing
  the completed managed exception hierarchy.
- Compatibility: no supported generated binding package has been released. Generated managed and
  native artifacts remain one inseparable exact-match set for the pinned `win-x64`, OCCT, compiler,
  architecture, and layout inputs; the dependent public package/assembly identity is
  `TedToolkit.Occt.Windows`, while its default C# namespace remains `TedToolkit.Occt`.
- Preserved behavior: public exports remain C11-compatible and contain no C++ class, reference,
  template, STL, or exception type; native exceptions are projected through the accepted managed
  error boundary; ownership and cleanup remain explicit and same-library; incomplete operations
  and layouts fail closed without partial output.

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
- Each declaration and operation receives exactly one generated, unsupported, or excluded
  disposition. No emitter rebuilds or independently filters a supported-operation list.
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
| OB-01 | Generation authority | Handwritten and parsed operation authorities coexist | Every layer traverses one normalized declaration and projection graph | Unsupported siblings remain isolated |
| OB-02 | Managed object layout | Records use explicit offsets and old architecture treats many objects as semantic or opaque projections | Every supported C++ object is an exact unmanaged sequential struct with generated physical segments | Native compiler data remains authoritative |
| OB-03 | Inheritance and behavior | Descriptor classes and class inheritance are assumed by dependent records | Interfaces express inheritance; complete derived storage has no managed `BaseType`; instance operations are extensions | OCCT names and source behavior remain recognizable |
| OB-04 | Templates | Closed C++ template instances have no approved generic managed rule | One generic physical graph represents every proved supported specialization, otherwise closed projection or rejection applies | Unknown closed types fail before native access |
| OB-05 | Ownership | The old plan assumes a covariant descriptor `Handle<out T>` and opaque non-transient objects | `Handle<T>` owns only transient exact layouts; trivial values own no native lifetime; `Owned<T>` owns approved RAII values with no public owner hierarchy | Exactly-once and same-library cleanup remain |
| OB-06 | Contract identity | ABI-v1/version state or incomplete manifests gate loading | One unversioned fingerprint includes every layout, closed generic, operation, ownership, error, and cleanup fact | Mismatch fails before operation resolution |
| OB-07 | Generator staging and configuration | Source emitters exist, but namespace selection and native compilation are not a complete governed path | Model-only C#/C++ emitters honor one configured C# root namespace; source generation may stop before an optional native build | Package delivery still requires the compiled and verified native artifact |

<!-- acceptance-case: AC-01 -->
### AC-01 — One declaration model emits every compatible layer

```gherkin
Scenario: Generate a fully supported parsed operation
  Given one parsed declaration has complete physical, transport, ownership, conversion, error, and cleanup semantics
  When generation completes
  Then its C declaration, C++ adapter, managed import, public operation, manifest row, and native build entry share one canonical identity
  And the configured root namespace is applied coherently to every generated C# artifact
  And C# and C++ emission consume only the completed Model and can finish without native compilation
  And no handwritten catalog, final symbol, adapter, import list, or second declaration graph supplies missing facts
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
### AC-04 — Representation, behavior, and lifetime stay separate

```gherkin
Scenario: Generate trivial, transient, and non-transient RAII types
  Given each type has a proved ownership category
  When its public managed surface is emitted
  Then trivial values use ordinary struct construction without disposal
  And Standard_Transient values are exact structs owned only by Handle<T>
  And non-transient RAII values use stable placement-constructed storage in Owned<T>
  And Handle<T> and Owned<T> expose no public ownership inheritance or conversion
  And const value operations use in receivers while mutating value operations use ref receivers
  And owner operations use Handle<T> or Owned<T> extensions without public Value extraction
  And generated wrappers reference the Runtime owner definitions without emitting them or receiving friend access
```

<!-- acceptance-case: AC-05 -->
### AC-05 — Exact matching includes memory and lifetime interpretation

```gherkin
Scenario: Load generated artifacts from different physical contracts
  Given managed and native artifacts differ in any layout, compiler identity, closed template, ownership, or cleanup fact
  When managed initialization runs
  Then it throws BadImageFormatException before resolving an OCCT operation
  And matching artifacts resolve and invoke representative operations successfully
```

<!-- acceptance-case: AC-06 -->
### AC-06 — The replacement boundary builds and runs without active ABI-v1 artifacts

```gherkin
Scenario: Exercise the generated replacement
  Given the replacement is generated from pinned real OCCT headers
  When native compilation is selected and its strict C11 and managed consumers run
  Then representative layout, value, generic, handle, RAII, error, mutation, and cleanup categories match OCCT behavior
  And every owned resource is released once through its allocating library
  And active source, build, fixtures, output, and current documentation contain no ABI-v1 path
```

## Constraints and risks

- [GEN-01](../../principles/README.md#gen-01-generate-every-binding-layer-from-one-semantic-source),
  [GEN-02](../../principles/README.md#gen-02-reproduce-every-supported-native-object-layout-exactly),
  [GEN-03](../../principles/README.md#gen-03-separate-native-representation-from-ownership-and-behavior),
  [GEN-04](../../principles/README.md#gen-04-keep-the-shared-runtime-minimal-and-declaration-agnostic),
  and the [generated binding architecture](../../architecture/generated-binding-system.md) govern
  this migration.
- The exact `net8.0` public Handle and Owned construction and scoped-invocation contracts remain a
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
- Active code and current guides describe only the accepted replacement architecture after
  migration.
- Escalate if implementation needs another declaration graph, a handwritten final artifact, an
  independently versioned native ABI, a public raw pointer, ownership in a struct, or a layout that
  cannot be proved on the pinned matrix.

<!-- section: delivery-brief -->
## Delivery disposition

The revised five-item controlled map separates independently verifiable outcomes: MIG-001 owns the
canonical semantic/physical model and exact managed storage projection; MIG-002 owns the managed
public API and namespace surface; MIG-003 owns the generated C/C++ boundary, manifest, fingerprint,
and native build description; MIG-004 owns exact-match initialization; and MIG-005 owns optional
native compilation, real integration, and removal of the legacy boundary. All items remain Draft
until the revised map is approved.

<!-- section: proof-plan -->
## Proof

| Contract | Evidence purpose | Execution shape | Primary proof | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 / AC-06 | Acceptance and structural regression | Component plus Integration | Source-only C#/C++ output is complete, namespace-coherent, and deterministic; selected native compilation builds the replacement; strict C11 and managed consumers pass; no active ABI-v1 path remains | Run source-only generation, build the generated native project, run its CTest consumer, then run Generator and Runtime TUnit projects in Release |
| AC-02 | Acceptance and boundary | Contract plus native/managed Integration | For every emitted object type, native and managed probes agree for size, alignment, packing, and every physical segment; generated source contains no prohibited layout form | Generate and run the exhaustive layout-contract matrix against the pinned compiler and inspect independent generated output |
| AC-03 | Acceptance and regression | Contract plus Integration | Several `NCollection_Array1<T>` specializations share one generic definition and pass closed layout/lifecycle proof; unknown T fails before native access | Run generic projection fixtures and representative real OCCT specializations |
| AC-04 | Acceptance and compatibility | Public API Contract plus Integration | Public baselines and two independently named wrapper fixtures prove value, Handle, Owned, interface inheritance, `in`/`ref` extension receivers, copy, disposal, same-library cleanup, one Runtime owner definition, and no friend access | Inspect generated and Runtime public/assembly metadata and run representative construction/call/copy/disposal cases |
| AC-05 | Acceptance and boundary regression | Contract plus Integration | Matching fingerprint initializes; missing or differing layout/lifetime identities fail before operation resolution | Run matching, differing, and missing-bootstrap fixtures with resolution instrumentation |

<!-- section: completion-criteria -->
## Completion

AC-01 through AC-06 pass on the pinned `win-x64` matrix; the approved public owner and receiver
contracts are recorded; the generated replacement is deterministic and exact-match; every supported object and
closed generic has native/managed layout and lifecycle proof; active ABI-v1 and old descriptor or
explicit-offset paths are removed; the dependent `TedToolkit.Occt.Windows` package records consume
the exact candidate; and no package publication or additional platform claim is included.
