# Repository design principles

## Scope and precedence

- Governed scope: architecture, implementation, and review decisions for TedToolkit.CppBindings source
  generation and generated native and managed binding artifacts.
- External hard constraints that take precedence: supported toolchain and platform requirements,
  C11 interoperability, explicit ownership and lifetime contracts, and same-library cleanup.
- Product intent: None currently recorded.
- Precedence: approved product intent guides principles; principles guide architecture design;
  approved architecture constrains change design; approved work items constrain implementation.

## Principle index

| ID | Title | Strength | Status | Owner | Review trigger | Document |
| --- | --- | --- | --- | --- | --- | --- |
| GEN-01 | Generate every binding layer from one semantic source | Required | Active | TedToolkit maintainers | A binding artifact cannot be derived without declaration-specific code or a generated output requires a manual patch | This file |
| GEN-02 | Reproduce every supported native object layout exactly | Required | Active | TedToolkit maintainers | A generated type cannot prove native size, alignment, or physical segment placement for the supported toolchain | This file |
| GEN-03 | Separate native representation from ownership and behavior | Required | Active | TedToolkit maintainers | A generated API would place lifetime behavior in a native-layout struct, use a transient handle for a non-transient type, require routine handle operations through `.Value`, or expose instance behavior through a copied native value | This file |
| GEN-04 | Keep the shared Runtime minimal and declaration-agnostic | Required | Active | TedToolkit maintainers | Runtime would gain a declaration-specific type, symbol, import, layout, specialization, or generated-set constant | This file |
| GEN-05 | Support every representable native capability | Required | Active | TedToolkit maintainers | A declaration is excluded because of its library, template, smart-pointer, stream, or other broad type category | This file |

## Principles

### GEN-01: Generate every binding layer from one semantic source

- Status: Active
- Strength: Required
- Scope: The Generator and every generated C declaration, C++ adapter, managed transport/import,
  and public C# binding.
- Owner: TedToolkit maintainers
- Review trigger: A new output language or ABI backend is introduced; an operation appears to
  require declaration-specific executable code; or any generated artifact requires a manual patch.

#### Default

The parsed and normalized semantic model is the single source for every supported binding
operation. Generic validation, mapping, naming, and emission rules transform that model into all
native and managed artifacts in one coherent pipeline.

Every declaration-specific artifact is derived from that Model: managed representation and public
API, managed imports and invocation glue, C declarations, C++ adapters, layout and ownership
metadata, construction and cleanup paths, manifests, fingerprints, and native build descriptions.
Legacy ABI source, generated text, handwritten conformance fixtures, and emitted artifacts are not
semantic inputs, comparison authorities, fallbacks, or compatibility targets for generation.

The architectural stage direction is fixed: configured native inputs are parsed and normalized
into the Model; C# and C++ emitters consume only that Model; and an optional post-emission build
stage may compile the emitted C++ project into the native library. Emitters do not read each
other's output, recover semantics from emitted text, or bypass the Model. The Model schema may
evolve as new semantics are required, but it remains the only emitter input boundary.

The Generator must not embed knowledge of a specific OCCT header, declaration, type, source
location, operation, final export symbol, adapter body, managed import, or public wrapper. It must
not maintain a manually curated per-operation catalog or copy handwritten binding source into the
generated output. Export names and every paired declaration and implementation are derived
deterministically from the same canonical operation identity.

#### Rationale

Declaration-specific source creates parallel authorities that can disagree while still compiling.
One semantic source prevents drift between the C contract, C++ implementation, managed import, and
public API; allows coverage to scale beyond a conformance sample; and makes regeneration and review
reproducible.

#### Practical implications

- Generator code may contain language syntax emitters, transport vocabulary, category-level type
  and ownership policies, compatibility rules, and deterministic naming algorithms.
- Declaration-specific exceptions, when unavoidable, are validated semantic metadata or mapping
  data consumed by the common pipeline; they are not handwritten C, C++, C#, or final symbols.
- One supported operation produces its complete native and managed chain from one model. If any
  required projection or implementation cannot be derived, the complete operation is reported as
  unsupported and no partial artifact is emitted.
- C# and C++ source emission completes before native compilation begins. Source-only generation
  may omit compilation; a ready-to-use package must compile and verify the emitted native project.
- The generated C# root namespace is a validated generation input. It has a deterministic default,
  applies coherently to every generated C# artifact, and does not change canonical C++ identity or
  native export naming. Changing the default after a public package baseline is a breaking managed
  API decision.
- Generated artifacts are never edited by hand. A required correction changes the source model,
  mapping policy, validator, or emitter and is then regenerated.
- Tests prove cross-layer identity and completeness from generated outputs rather than comparing
  them with separately handwritten operation lists.
- Migration may keep a legacy boundary physically present only as an inactive recovery artifact
  until the generated replacement passes. No generator, replacement build, or acceptance proof may
  consume that boundary; the completed migration removes it from active source, build, fixtures,
  output, and current documentation.

#### Exception route

Any declaration-specific executable binding, manually assigned final symbol, handwritten generated
artifact, or second operation authority requires an explicitly approved update to the current
architecture before implementation. That update must identify why the common semantic pipeline
cannot represent the case, how cross-layer drift is prevented, and the objective condition for
removing the exception.

### GEN-02: Reproduce every supported native object layout exactly

- Status: Active
- Strength: Required
- Scope: Every generated C# type that corresponds to a C++ object type, including values,
  `Standard_Transient` descendants, non-transient RAII objects, template specializations, base
  subobjects, and compiler-generated object state.
- Owner: TedToolkit maintainers
- Review trigger: The supported OCCT version, compiler ABI, architecture, runtime layout behavior,
  native packing, or generated package platform matrix changes; a layout requires an
  unrepresentable alignment or `T`-dependent physical segment; or managed and native layout proof
  disagrees.

#### Default

Every supported C++ object type is projected as an unmanaged C# `struct` whose complete physical
representation matches the pinned native build exactly. Equality is required for total size,
alignment, ordered physical segments, field and base-subobject positions, padding, tail padding,
and compiler-generated state.

The generated representation uses `LayoutKind.Sequential` with typed fields, explicit private
padding, and alignment-preserving opaque storage. It does not generate `FieldOffsetAttribute`.
Native unions, bit fields, virtual-table pointers, reused base tail padding, and other hidden or
overlapping state are represented by physical storage segments and typed accessors rather than by
overlapping managed fields.

C++ inheritance is a semantic relationship expressed through generated C# interfaces. A generated
struct does not contain or expose a managed `BaseType` field. Its storage is derived from the
compiler-reported layout of the complete C++ object, not by nesting the complete managed projection
of a base type.

An operation uses its concrete declaring struct as the extension receiver unless that declaring
type has generated derived records that must reuse the operation. Only that proven inheritance case
introduces a generic `TReceiver` and pointer adjustment. A standalone value type such as `gp_XYZ`
therefore keeps concrete `in gp_XYZ` or `ref gp_XYZ` receivers. Because C# rejects `in` on a generic
extension receiver, inherited generic value operations use `ref TReceiver`; the generated native
call still preserves the source method's constness and does not copy the receiver.

#### Rationale

An exact in-memory projection permits generated interop to inspect and pass native objects directly
through pointers without translation objects or per-call field copying. Treating padding, hidden
state, and base storage as first-class generated segments also prevents a managed type from looking
source-equivalent while being ABI-incompatible.

#### Practical implications

- The Generator continues to acquire native size, alignment, field offsets, base-subobject offsets,
  and hidden-layout evidence from the pinned native compiler. Removing generated field-offset
  attributes does not remove native offset analysis.
- The Generator computes one ordered physical-segment model, inserts every required padding or
  opaque-storage segment, simulates the resulting CLR sequential layout, and fails closed when the
  complete representation cannot be proved.
- Packing is selected from the supported native ABI. A universal `Pack = 1` projection is forbidden
  because matching offsets and size does not compensate for a mismatched type alignment.
- Native fields that overlap, occupy bits, or participate in reused storage are accessed through
  generated typed operations over one physical storage segment. Duplicate overlapping managed
  fields are not emitted.
- Compiler-generated state such as a virtual-table pointer is represented as native storage even
  when no corresponding field appears in the C++ source declaration.
- A C++ class template is projected as one C# generic struct only when one generic physical-segment
  graph exactly represents every supported closed specialization. Each allowed closed
  specialization still requires native size, alignment, layout, identity, and lifecycle proof.
  Otherwise the Generator emits separately proved closed projections or reports the type as
  unsupported.
- Layout identity participates in the generated exact-match contract. A managed assembly must not
  operate with a native artifact produced for a different OCCT, compiler, ABI, architecture, or
  layout configuration.
- Each generated binding assembly and package is bound to one proved native layout matrix. A
  platform-neutral binding package must not carry one exact-layout managed assembly across OS,
  architecture, compiler ABI, or packing variants unless complete layout equality is independently
  proved for every advertised variant.

#### Exception route

Any supported C++ object projection that is not an exact unmanaged struct, uses explicit managed
field offsets, embeds a managed base-type projection, accepts an unproved layout, or shares one
managed binding assembly across unproved native layout matrices requires an explicitly approved
update to the current architecture before implementation. The update must preserve pointer safety,
define the constrained toolchain and type scope, and provide an objective path back to exact
generated layout proof.

### GEN-03: Separate native representation from ownership and behavior

- Status: Active
- Strength: Required
- Scope: Construction, ownership, copying, disposal, low-level storage access,
  inheritance-facing behavior, and generated method syntax for every projected OCCT object type.
- Owner: TedToolkit maintainers
- Review trigger: A supported type does not fit the trivial-value, `Standard_Transient`, or owned
  non-transient categories; an operation requires ownership transfer not expressible by the
  selected owner; routine use requires a caller to reach through an owner to its native-layout
  value; a non-owning native reference would gain a managed wrapper or owner-retaining lifetime
  abstraction; or a native-layout value would gain managed lifetime behavior.

#### Default

An exact-layout struct describes native object memory; it does not by itself own an external native
lifetime or implement managed lifetime control. Generated OCCT instance operations are extension
methods over the appropriate value, handle, or non-transient owner so callers use instance-like
syntax without extracting native storage or duplicating a native object.

Every `Standard_Transient` descendant remains an exact-layout struct and is owned only through the
reference-type `Handle<T>`. `Handle<T>` carries the stable native address and OCCT
intrusive-reference lifetime; it is never valid for a type that does not derive from
`Standard_Transient`. It owns one reference, not the C++ object storage: C++ reference counting
continues to govern the object's final destruction and memory. Logical inheritance constraints and
conversions are expressed through the generated interfaces from GEN-02, not managed class
inheritance between native object projections.
`Handle<T>.Value` is a public, non-owning `ref T` view of the exact-layout value for explicit
low-level data access. It does not transfer ownership, acquire an independent lifetime, or make the
value a disposable object. Its validity remains bounded by the live, undisposed handle that owns the
native object.

A non-`Standard_Transient` type uses one of two categories. A trivial value such as a `gp_*` value
is created and copied as an ordinary C# struct and exposes no disposal capability. A type with a
native destructor or other RAII state is contained directly in correctly aligned managed storage
through the reference-type `Owned<T>`. Native code receives its address only while that storage is
stabilized by `fixed`. `Owned<T>` invokes deterministic
same-library destruction but does not use intrusive `Release` and does not ask native code to free
its managed backing storage. `Owned<T>.Value` provides the same kind of public, non-owning `ref T`
view as `Handle<T>.Value`; it does not change the object's ownership or lifetime. Both owners
implement `ICppOwner<T>`, whose sole member is `ref T Value`, for declaration-agnostic diagnostics
and explicit low-level access. The interface is not a shared generated receiver and defines no
construction, cleanup, disposal, conversion, or ownership semantics; intrusive release and direct
destruction remain distinct.

Borrowing is an operation-level lifetime fact, not another ownership or public representation
category. A non-owning native reference remains a direct, C++-like `ref T` view or an exact native
pointer at an already-approved low-level boundary. The binding does not introduce `Borrowed<T>`, a
per-declaration borrowed reference class, an owner-retaining facade, or a lease solely to prevent
use-after-free. Such wrappers add managed allocation, identity, and state to a native relationship
that remains non-owning and still cannot be made safe against explicit disposal or native
invalidation. Supported suspicious uses belong to suppressible binding-analyzer guidance; the
caller remains responsible for the referenced owner's lifetime.

The completed Model classifies every supported C++ object into exactly one representation and
ownership category before emission:

- a proved `Standard_Transient` descendant with a complete intrusive-reference lifecycle uses
  `Handle<T>`;
- a proved non-transient object that is safely copyable as a value and requires no native lifetime
  cleanup is an ordinary value struct; and
- any other supported non-transient object uses `Owned<T>` only when construction, copying,
  destruction, alignment, and same-library cleanup are complete and representable.

The classification and its compiler-backed evidence belong to the Model. Emitters do not infer,
override, or repair it. A declaration that cannot be classified uniquely, or whose selected
category lacks complete layout, transport, construction, copy, destruction, or cleanup evidence,
is unsupported and produces no callable partial binding.

Type category alone is not sufficient to generate an operation. The completed Model also records
the receiver, parameter, result, ownership-transfer, borrowing, construction, and cleanup semantics
needed for the complete cross-language call. An operation with an ambiguous or incomplete
lifetime flow is unsupported as a whole.

#### Rationale

C# struct assignment is a bitwise value copy and cannot run an OCCT handle retain operation, a C++
copy constructor, or a native destructor. Keeping native memory views separate from reference-type
owners preserves exact layout while giving aliasing, disposal, and cleanup one managed identity.
Extension methods then retain familiar OCCT call syntax without putting behavior or ownership into
the generated storage struct. Keeping non-owning access as a direct native-memory view preserves
the lightweight C++ lifetime model instead of allocating a managed object for every borrowed
reference; analyzers can identify bounded suspicious patterns without pretending to own or prove
the lifetime.

#### Practical implications

- A generated exact-layout struct never implements `IDisposable` merely because its native type has
  a destructor and never exposes managed ownership or disposal operations. Disposal belongs to the
  reference-type owner of the object lifetime, so `Handle<T>.Dispose()` is valid while
  `handle.Value.Dispose()` is not.
- `Handle<T>` owns only `Standard_Transient` objects and releases them through the matching OCCT
  intrusive-reference operation. It stores only the native address and owns no object storage.
  Non-transient values and owners cannot be converted to or wrapped by `Handle<T>`.
- `Owned<T>` owns only approved non-transient RAII objects. The owner object directly contains one
  private `T` field as the native object's managed storage; it does not allocate a second storage
  object. Generated construction, invocation, and destruction stabilize that field with `fixed`
  whenever native code receives its address. Its generated construction path placement-constructs
  the object there and does not heap-allocate it through C++ `new T`. Disposal
  invokes the matching C++ destructor exactly once. It never calls the OCCT intrusive-reference
  `Release` operation or a native storage-free function; the GC reclaims the managed backing
  storage.
- Runtime defines the empty representation-category marker `ICppRaii`. `Owned<T>` requires
  `where T : unmanaged, ICppRaii`. Every generated exact-layout struct classified as supported
  non-`Standard_Transient` RAII, including eligible `TCollection_*` types, implements this marker.
  Proved trivial values and `Standard_Transient` projections do not implement it.
- `Owned<T>` construction has no public completion phase. A generated factory creates the owner
  through `Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)`, using its matching non-throwing
  `cdecl void(T*)` destructor,
  placement-constructs the private `T` field through `Value`, and returns the owner only when the
  native constructor succeeds. A local success flag and `finally` suppress finalization on every
  managed exit before native success is established. Native construction failure is projected only
  after suppression, so no destructor runs for an object whose C++ construction did not complete.
- Direct-field `Owned<T>` generation is allowed only when the pinned CLR target proves that the
  field representation satisfies the native `alignof(T)` requirement. This is a generator
  admission decision: a type whose direct-field alignment cannot be proved is unsupported rather
  than moved to another storage form. Runtime neither receives an alignment value nor treats one
  observed managed address as proof across later GC relocation.
- The `Owned<T>` constructor is public only so independently generated wrapper assemblies can call
  it without friend access. It is marked `GeneratedCodeOnly`, and the binding analyzer reports
  handwritten `new Owned<T>(...)` as an error. Consumers obtain owners only from generated C++
  factory projections; suppressing the diagnostic crosses the supported construction boundary.
- A generated binding loads its exact-match native module for the process lifetime and does not
  support unloading it. Its generated static function table centralizes resolved export addresses,
  while each `Handle<T>` or `Owned<T>` receives and retains the exact matching release or destructor
  pointer needed by its own finalization. Owners never retain a generated table or table index.
  They therefore hold no per-owner module lease and do not participate in module unloading. Runtime
  validates cleanup pointers and owner state but does not authenticate a function pointer's module
  origin; that guarantee belongs to generated exact-match initialization and factory emission.
  Runtime may share declaration-agnostic disposed-state and cleanup machinery internally.
- `ICppOwner<T>` exposes only non-owning `ref T Value` for shared diagnostics and low-level access;
  it does not inherit `IDisposable`, select cleanup, or act as a common generated receiver.
- Assigning a reference-type owner aliases one owner and one disposed state. A distinct native
  object is produced only by an explicit generated clone or copy operation that invokes the mapped
  C++ copy semantics.
- Factory-style creation is used when an operation produces owned native identity, including
  `Standard_Transient` handles. Ordinary C# value construction is reserved for proved trivial
  value types.
- Generated instance operations are extension methods on the semantic receiver. A C++ `const`
  value operation receives `this in T`; a mutating value operation receives `this ref T` so the
  exact-layout struct is neither boxed nor silently copied. Transient operations expose separate
  direct overloads for owning `Handle<T>` and borrowed `in handle<T>` receivers; both use the same
  native slot and only the owning overload applies owner liveness. `Owned<T>`, static operations,
  and factories remain distinct. Consumers invoke routine operations without reaching through an
  owner to its native-layout value.
- `Handle<T>.Value` and `Owned<T>.Value` are simple escape hatches for direct field or property data
  access and explicit low-level interop. They are not normal receivers for generated OCCT
  operations, lifetime tokens, or second owners. A returned reference cannot be revoked, does not
  keep its owner alive, and must not be used after or concurrently with disposal. The caller owns
  these low-level lifetime obligations and any resulting use-after-free risk.
- Generated bindings do not allocate a managed `Borrowed<T>` object, declaration-specific borrowed
  class, owner-retaining facade, lease, or equivalent lifetime token for a native pointer,
  reference, iterator, or subobject that remains non-owning. Borrowing stays in the operation model
  and direct access surface; it does not become a fourth owner category.
- A generated operation obtains each owner address only inside a lexical `fixed` scope over that
  owner's `Value`; the resulting pointer does not escape the scope. This one call shape pins
  `Owned<T>` managed storage when required and also works for the already-stable native address
  referenced by `Handle<T>`. After each finalizable owner's last unmanaged use, generated code calls
  `GC.KeepAlive(owner)` before error projection. `fixed` does not itself extend the owner lifetime or
  protect against explicit concurrent disposal.
- Generated API shape and receiver types keep ownership behavior off exact-layout structs and keep
  routine owner operations off `.Value`. The binding analyzer rejects handwritten use of explicitly
  marked generated-only Runtime hooks and reports supported suspicious `Value` lifetime patterns,
  including known escape, suspension, temporary-owner, post-disposal, alias-disposal, and missing
  owner-keepalive forms. This analysis is suppressible and incomplete; it does not prove aliasing,
  concurrency safety, or absence of use-after-free.
- Generated APIs reject unsupported owner implementations, closed generic types, disposed owners,
  and invalid inheritance projections before obtaining native memory. A `Value` getter rejects an
  owner already known to be disposed, but Runtime cannot revoke a reference already returned or
  prevent a later concurrent `Dispose()`.

#### Exception route

Putting ownership or disposal directly on an exact-layout struct, using `Handle<T>` for a
non-`Standard_Transient` type, treating `.Value` as an ownership token or the routine receiver for
generated handle operations, introducing a managed borrowed-reference wrapper or owner-retaining
lifetime abstraction, or copying an owning native object through C# struct assignment requires an
explicitly approved update to the current architecture before implementation. The update must
define copy, aliasing, construction, destruction, exception, same-library cleanup, and the measured
cost and safety boundary of any added wrapper.

### GEN-04: Keep the shared Runtime minimal and declaration-agnostic

- Status: Active
- Strength: Required
- Scope: The handwritten `TedToolkit.CppBindings.Runtime` package and every dependency introduced into it
  for generated managed libraries.
- Owner: TedToolkit maintainers
- Review trigger: Runtime would gain a type-specific layout, operation import, native symbol,
  template specialization, manifest value, generated-set registry, or dependency that is not
  required by all relevant generated consumers.

#### Default

Generated managed libraries may depend on `TedToolkit.CppBindings.Runtime`, but Runtime contains only the
small declaration-agnostic mechanisms required to implement their shared managed contracts. It has
no dependency on the Generator, a generated binding assembly, or an OCCT declaration set.

Declaration-specific types, layouts, inheritance interfaces, extension operations, imports,
native symbols, function-table slots and storage, closed-generic registrations, and per-type
construction or cleanup adapters belong to generated output. Runtime may own shared metadata,
exception, loading, invocation-lifetime, and ownership mechanisms only when their contracts are
independent of any particular OCCT declaration or generated artifact set.

#### Rationale

A broad Runtime becomes a second handwritten authority for facts already known by the Model,
couples unrelated generated packages to one declaration set, and makes regeneration incomplete.
A narrow dependency keeps generated assemblies self-describing while centralizing only lifecycle
and safety mechanisms that must behave consistently across them.

#### Practical implications

- A Runtime addition must be necessary for a shared correctness or public contract, reusable across
  generated declaration sets, independent of concrete OCCT symbols and layouts, and materially more
  appropriate to centralize than to emit. Failing any condition keeps it in generated output.
- Runtime may define stable abstractions such as native-name metadata, managed exception contracts,
  owner-state machinery, and declaration-agnostic native-module loading capabilities.
- Generated code supplies the concrete type identities, imports, exports, layout evidence,
  specialization registry, and target-specific construction, release, or
  pointer-adjustment functions consumed through those mechanisms.
- Runtime dependencies are reviewed as part of its public and transitive surface. Convenience alone
  is not sufficient reason to add a package or a declaration-specific helper.

#### Exception route

Adding declaration-specific or generated-set-specific authority to Runtime, or making Runtime
depend on the Generator or a generated binding assembly, requires an explicitly approved update to
the current architecture before implementation. The update must explain why the Model cannot emit
the information, how regeneration and version isolation remain complete, and when the exception
can be removed.

### GEN-05: Support every representable native capability

- Status: Active
- Strength: Required
- Scope: Admission and projection of native declarations, C++ templates, standard-library types,
  smart pointers, streams, and every operation that exposes them.
- Owner: TedToolkit maintainers
- Review trigger: A declaration or operation is excluded because it belongs to a broad type
  category rather than because its concrete layout, transport, invocation, or lifetime semantics
  cannot be proved.

#### Default

The Generator supports every native capability that its generic Model and generated adapter
mechanisms can express without changing native semantics. A type is not unsupported merely because
it is a C++ template, standard-library type, smart pointer, stream-related type, or an unfamiliar
closed specialization.

User-selected public headers are generation roots, not a whitelist of top-level output types. The
Model recursively closes over every record, enum, base, field, parameter, result, and template
specialization required by those roots. Every representable member of that dependency closure is
generated. Output filtering by library, namespace, template family, nested-declaration category, or
whether a type was selected directly is forbidden. The Windows product uses nearly all installed
OCCT public headers as roots and therefore delivers their complete representable dependency closure.

When one generic physical and behavioral model is valid, generate one C# generic struct and its
generic operation surface. Every used closed specialization still receives compiler-backed layout,
ABI, construction, destruction, borrowing, and ownership validation. When one generic managed
shape cannot represent all specializations, generate separately proved closed projections instead
of rejecting the entire template category.

A native template declaration that uses `void` as a placeholder for a dependent implementation
type is not itself a usable generic API and must not be projected as one. Generate only its usable
closed specializations, using the established underscore-expanded fixed type names that encode the
concrete template arguments.

Smart pointers and stream-related APIs follow the same rule. They are supported when generated
native adapters can preserve the exact pointee, control-block or reference-count behavior,
construction, copying, movement, destruction, and borrowing contract. An exposed inner pointer is
never substituted for the actual native owner, and managed convenience does not invent ownership
that the C++ API does not provide.

Shared stream specializations and `NCollection_Handle<T>` are explicitly included in this rule.
Neither may be denylisted by name or family. The Generator attempts their closed models and native
adapters first; failure of one concrete semantic or ABI proof excludes only the affected
specialization or operation and must be reported with that evidence.

#### Rationale

Broad exclusions trade generator simplicity for unnecessary loss of the native API. The Generator
already owns generic declaration analysis, compiler probing, native adapters, and managed emission;
those mechanisms should scale to representable template families instead of maintaining a growing
denylist. Evidence-driven admission preserves API coverage without weakening layout or lifetime
correctness.

#### Practical implications

- Attempt generic modeling before adding any type-name or library-category exclusion.
- Treat selected headers as roots and recursively generate their complete representable type
  dependency closure. Never leave an emitted declaration referring to a type that was filtered out.
- The Windows package selects nearly all installed OCCT public headers. Header exclusion requires a
  concrete parse or dependency failure, and declaration exclusion requires a concrete failed
  semantic or ABI proof at the narrowest affected boundary.
- `std::pair<TFirst, TSecond>`, shared streams, smart pointers, and `NCollection_Handle<T>` are
  ordinary support candidates, not predefined unsupported categories.
- Generate methods and extension methods from the template Model just as for non-template records;
  do not handwrite per-specialization wrappers.
- Do not emit open generic projections for `void`-placeholder template declarations. Emit only the
  concrete closed specializations, with underscore-expanded fixed names.
- Validate every emitted specialization on the target compiler ABI. Generic source shape does not
  replace closed-specialization proof.
- If only one concrete operation cannot preserve layout, transport, invocation, or lifetime
  semantics, exclude and report that operation at the narrowest boundary; do not discard unrelated
  methods or the entire template family.
- Unsupported reports state the failed semantic proof. A broad label such as "STL type",
  "smart pointer", or "iostream" is not sufficient justification.

#### Exception route

Adding a broad category exclusion requires an accepted ADR demonstrating that the category cannot
be represented by the common Model or generated adapter architecture. The ADR must identify the
failed semantic proof, affected API surface, considered generic and closed-specialization designs,
and an objective condition for revisiting the exclusion.

## Exception route

A proposed deviation from a Required principle must first update the affected principle and current
architecture and receive explicit maintainer approval. Emergency changes that cannot satisfy this
gate are not shipped as supported generated bindings.

## Maintenance

- Principle-set owner: TedToolkit maintainers
- Review cadence or objective review triggers: Review whenever the Generator adds an output layer,
  a declaration-specific mapping, a manual generated-source step, a native layout category, or an
  ownership category; a generated binding package changes its platform matrix; or Runtime gains a
  new public mechanism or dependency.
- Last reviewed: 2026-08-28
