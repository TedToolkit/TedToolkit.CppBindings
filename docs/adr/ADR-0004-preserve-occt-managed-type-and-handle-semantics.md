# ADR-0004: Preserve OCCT type names and handle ownership in the managed API

- Status: Accepted
- Date: 2026-08-25
- Decision owner: TedToolkit.Occt maintainers
- Decision scope: Public managed type naming and `Standard_Transient` ownership semantics for
  generated `TedToolkit.Occt` bindings.
- Applicable product intent: None
- Applicable principles: None
- Supersedes: None
- Superseded by: None
- Related decisions: [ADR-0001](ADR-0001-stable-c-interop-abi.md) and
  [ADR-0003](ADR-0003-distribute-generated-occt-bindings.md)

## Decision at a glance

The generated managed API preserves legal OCCT type identifiers and represents every owned
`Standard_Transient` reference through one handwritten covariant `Handle<out T>` contract whose
Runtime implementation uses the versioned native retain/release boundary.

## Context and decision question

ADR-0001 defines owned and borrowed transient tokens at the C ABI, but deliberately leaves the
public managed projection independent from that transport. The current Runtime has removed its
pre-version `Handle<T>` prototype and explicitly provides no production lifetime abstraction. The
current conformance model also projects `opencascade::handle<Geom2d_CartesianPoint>` to public
`Geom2d_CartesianPoint`, erasing the source ownership distinction before a callable package exists.

OCCT itself distinguishes value-semantic types such as `gp_Pnt2d` from heap-resident descendants of
`Standard_Transient` managed through `opencascade::handle<T>`. Flattening both categories into plain
managed type names makes ownership, copying, disposal, and borrowing invisible to a C# caller.

The decision question is: **how should the public managed API preserve recognizable OCCT naming and
`Standard_Transient` handle semantics without exposing raw pointers or reviving the unsafe
pre-version deletion path?**

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Public ownership must consume the ABI-major-1 retain/release contract and must never directly call `delete` or `Standard_Transient::Delete()`. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | Managed signatures must distinguish value, borrowed, and owned transient semantics without exposing a raw pointer, C++ handle layout, or unproved lifetime. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | A public owning value cannot rely on C# struct copying because bitwise copies cannot execute the OCCT handle copy constructor or retain a second native token. | Documented C# value-copy behavior and the OCCT intrusive-reference model summarized by ADR-0001 | Must |
| Decision driver | Legal OCCT identifiers, including casing and underscores such as `gp_Pnt2d`, should remain recognizable to existing OCCT users. | Existing generator source-name metadata and generated type model | High |
| Decision driver | Normal C# use should support deterministic `using`/`Dispose`, method-call syntax, and failure after disposal without requiring consumers to resolve native symbols. | The Runtime is the selected shared ownership and exception boundary in ADR-0003 | High |
| Decision driver | A native call must remain safe when disposal races with an in-progress borrowed invocation, without claiming that the OCCT operation itself is thread-safe. | Cross-thread ownership can otherwise release the target during native use | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Flatten an owned native handle into a disposable concrete managed type | The current conformance model uses this public projection. Confidence: High. | Partly | It can be made memory-safe but hides the OCCT distinction between a target type and its owning handle. | Rejected |
| Restore the pre-version raw-pointer `Handle<T>` and `ref T Value` API | Its implementation remains in Git history. Confidence: High. | No | It bypasses the versioned release boundary, assumes a managed view of an opaque C++ object, and does not establish safe generated deletion. | Rejected |
| Represent an owning handle as a C# struct mirroring `opencascade::handle<T>` | C# copies structs without an ownership hook. Confidence: High. | No | Assignment could duplicate a native token without retain and cause stale copies or double release. | Rejected |
| Preserve legal OCCT target names and expose one covariant owning `Handle<out T>` interface over an internal Runtime owner | ADR-0001 already supplies typed opaque handles, internal retain, release, and borrowed-call rules. C# permits covariance only on interfaces and delegates. Confidence: High for the semantic boundary; delivery proof remains required. | Yes | The public owning contract is an interface rather than a concrete class; C# reference assignment aliases one internal owner, and generated interop duplicates native ownership only when the mapped OCCT operation requires a distinct owner. | Selected |

## Decision

Public generated types preserve the canonical unqualified OCCT identifier exactly when it is a
legal C# identifier, including original casing and underscores. C# keyword escaping and structural
namespace or nesting projection do not rename that identifier, and canonical source spelling
remains metadata. A declaration whose public identifier would otherwise require mangling or collide
with another projected type is unsupported until a separate deterministic naming rule is approved.
The binding does not rename `gp_Pnt2d` to `Pnt2d` merely to follow .NET naming style.

Approved semantic values such as `gp_Pnt2d` are public C# value types. Their API represents OCCT
meaning and invariants rather than copying the C++ object layout across the ABI.

A generated type deriving from `Standard_Transient`, such as `Geom2d_CartesianPoint`, is a
non-owning target descriptor that mirrors the source inheritance identity and carries no native
object state. Target descriptors derive from a public `Standard_Transient` descriptor whose
construction boundary prevents consumer-defined derived targets; consumers cannot directly
instantiate descriptors or supply arbitrary `T` arguments to `Handle<T>`. A constructor or
operation that produces one owned transient token returns `Handle<Geom2d_CartesianPoint>`, not an owning
`Geom2d_CartesianPoint`, raw pointer, or public borrowed view. Generated instance-like operations are
callable on the handle while the generator remains free to implement that syntax through forwarding
or extension members.

The target descriptor hierarchy uses C# class inheritance and does not generate duplicate
`I<OCCTType>` interfaces. A supported transient descriptor has at most one supported direct base;
multiple inheritance is rejected until an explicit projection rule exists. This is stronger than
merely assuming that no diamond occurs.

`Handle<out T>` is a public covariant interface extending `IDisposable`, constrained to generated
`Standard_Transient` target descriptors. Its contract is handwritten once in
`TedToolkit.Occt.Runtime`; the Generator never emits another definition. Runtime supplies the only
supported implementation as an internal sealed reference-type owner. C# does not permit this public
interface to enforce a closed implementer set: consumer implementations can compile, but they are
unsupported and every generated operation rejects them with `ArgumentException` before acquiring a
lease or probing native code.
Generated bindings reference the shared contract and provide only target descriptors, public OCCT
operations, and internal type-specific glue needed to reach the correct ABI retain/release exports.
Because the only supported Runtime implementation is a reference type, conversion of a valid
Runtime-supplied owner from `Handle<TDerived>` to `Handle<TBase>` is a non-boxing covariant reference
conversion and creates no second owner. No allocation or boxing guarantee is made for an
unsupported consumer implementation before provenance rejection.

Each Runtime owner owns exactly one native ownership token.

The first atomic transition from open to disposed is the disposal linearization point. Calls that
have not acquired a lifetime lease before that transition fail with `ObjectDisposedException`
before native invocation. Operations that acquired a lease first may complete.

`Dispose` is idempotent and non-blocking: it returns after the disposed transition and may defer the
native release until the last pre-existing lease ends. The final lease consumes the token at most
once through its exact ABI-major release operation. Leasing guarantees native target lifetime only;
it does not make concurrent OCCT operations thread-safe. A non-throwing finalization path applies the
same exactly-once deferred-release rule when disposal is omitted. The native module remains loaded
until every dependent handle token and invocation lease has completed its cleanup.

Passing a `Handle<T>` to a non-consuming operation borrows its target only for that call even though
the parameter uses the same public owning contract. Managed
reference assignment aliases the same managed owner and disposed state; it does not invoke native
retain and no public `Retain` or ownership-duplication method is added. When the mapped OCCT
operation returns a distinct owning handle, performs a handle conversion, or stores a handle beyond
the call, generated interop acquires the required native reference internally before exposing or
storing that ownership.

Nullable handles are exposed only when the mapped OCCT operation permits null. The implicit
covariant conversion from `Handle<TDerived>` to `Handle<TBase>` aliases the same Runtime owner and
does not retain or create another ownership token. A base-target call acquires a borrowed invocation
lease and performs any ABI-approved native pointer adjustment internally. A successful explicit
checked downcast that returns a handle creates an independent owned token internally; a failed
downcast follows the mapped OCCT null or error contract. Generic pointer reinterpretation is not
public API.

Illustrative public usage is:

```csharp
gp_Pnt2d value = new(1.25, -2.5);

using Handle<Geom2d_CartesianPoint> point =
    Geom2d_CartesianPoint.Create(value);

point.SetPnt2d(new gp_Pnt2d(9, 8));

static void UsePoint(Handle<Geom2d_Point> point)
{
    // The operation borrows the owner for this invocation.
}

UsePoint(point); // Covariant reference conversion; no boxing, allocation, or retain.
```

This decision does not prescribe the private safe-handle implementation, generated source layout,
the private shape of generated per-target lifetime glue, or representation of owned non-transient
opaque objects. No public lowercase `handle<T>` borrowed view is introduced: borrowing is the
documented parameter behavior plus an internal invocation lease over the `Handle<T>` owner, not a
second public pointer-shaped value.

## Why this decision now

The first consumable package will establish a public compatibility baseline. Ownership erased from
that initial API would either become permanent or require an avoidable breaking migration. The
selected model keeps OCCT users oriented by source names, preserves the material value/handle
distinction, and delegates all native release behavior to the already accepted ABI rather than
reviving pre-version raw-pointer behavior.

The reference-type handle accepts one deliberate C# difference: assigning a handle variable aliases
the same managed owner instead of duplicating native ownership. Generated interop performs native
retain only where an OCCT operation requires a separate owner; trying to imitate the C++ copy
constructor with a public C# value type or invented public duplication method would make ordinary
C# use less predictable.

Reconsider the public handle shape if a required language feature cannot be expressed through
instance-like handle calls, a supported OCCT API requires weak ownership, or measured invocation
leasing cost is material for a representative workload.

## Evidence and links

- [Runtime current surface](../../src/core/TedToolkit.Occt.Runtime/README.md)
- [Generator type projection](../../src/core/TedToolkit.Occt.Generator/Models/AbiV1ConformanceModel.cs)
- [Repository architecture overview](../../README.md)
- [C interoperability ABI major 1](../interop-abi-v1.md)
- [OCCT Foundation Classes references collected by ADR-0001](ADR-0001-stable-c-interop-abi.md#evidence-and-links)

## Consequences and accepted trade-offs

- Public signatures expose ownership directly and retain recognizable OCCT names.
- Runtime regains a production lifetime responsibility, but release remains mediated by the native
  ABI rather than a managed call to `Delete()`.
- One handwritten covariant Runtime `Handle<out T>` contract supplies consistent public ownership
  semantics for every generated target; the only supported internal reference-type implementation owns the
  disposal, finalization, leasing, and disposed-state behavior, while generated bindings own
  type-specific ABI symbol selection.
- Generated call sites must acquire and release invocation leases and keep the native library loaded
  until all dependent handles are released.
- `Dispose` does not wait for already leased calls; callers remain responsible for OCCT's own
  thread-safety requirements and synchronization of concurrent operations.
- C# aliases and covariant base views share one handle object's disposed state. Native ownership duplication remains an
  internal generated operation driven by the mapped OCCT signature, not a public helper API.
- Public API baselines must treat changing a value projection to a handle projection, or removing
  the handle wrapper, as a compatibility change after release.
- The selected API is intentionally OCCT-oriented rather than normalized to ordinary .NET naming
  conventions.

## Downstream delivery constraints

- Generated public names preserve every legal canonical OCCT type identifier and record the source
  spelling for namespace, nesting, and keyword projections; unapproved identifier mangling or
  collisions make the affected declaration unsupported.
- Every owned `Standard_Transient` result is exposed as `Handle<T>`; every borrowed call keeps an
  owning handle alive for the complete native invocation.
- `Handle<out T>` accepts only generated, nonconstructible `Standard_Transient` target descriptors.
  Runtime's internal reference-type owner owns exactly one token and releases at
  most once through the allocating/retaining ABI library, and prevents use after disposal and
  release during an active invocation. Consumer implementations are rejected before native access.
- `Handle<out T>` is defined once by Runtime and never regenerated. Generated output references it
  and supplies only target-specific internal lifetime operations; no public lowercase borrowed
  handle, duplicate `I<OCCTType>` hierarchy, or native-pointer view is emitted.
- Generated transient target descriptors use one source-faithful class-inheritance chain. A type
  with more than one supported direct base is rejected until another accepted projection exists.
- Passing a valid Runtime-supplied `Handle<TDerived>` to a `Handle<TBase>` parameter uses covariance
  without boxing, allocation, native retain, or ownership transfer; the callee leases but does not
  dispose the caller's owner.
- Disposal linearizes before returning, rejects new leases, does not wait for existing leases, and
  defers native release until their completion; the same non-throwing exactly-once rule and module
  liveness apply to finalization.
- No public API exposes the opaque transient target through `ref T`, a raw pointer, `IntPtr`, or a
  copyable owning struct.
- Handle assignment aliases one disposal state without retaining, and no public ownership-
  duplication API is emitted. Generated operations retain internally only when their mapped OCCT
  ownership requires a distinct token. Generated casts never reinterpret pointer types and every
  successful handle-producing cast returns a separately owned token.
- Generated constructors, methods, inheritance operations, nullability, errors, and documentation
  preserve the source OCCT behavior while using only ABI-major-1 transports.
- The first package establishes a reviewed public API baseline containing representative value and
  transient-handle signatures before publication.

## Exit requirements

A replacement must preserve released public callers or define an explicit managed compatibility
transition, retain the ABI-major ownership and same-library cleanup guarantees, and provide an
equally explicit distinction between value, borrowed, and owned transient semantics. Replacing the
native ownership protocol remains subject to ADR-0001's ABI-major transition rules.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Accept or reject the managed naming and handle projection. | TedToolkit.Occt maintainers | Before reapproving the generated package delivery change | Closed |
| Reassess handle aliasing ergonomics. | TedToolkit.Occt maintainers | Representative consumer feedback shows persistent ownership misuse | Open |
| Reassess invocation leasing. | TedToolkit.Occt maintainers | Measured cost is material or a required call cannot safely lease the native target | Open |
| Define another ownership category. | TedToolkit.Occt maintainers | A required API needs weak, shared non-transient, or consuming-transfer semantics not governed here | Open |
