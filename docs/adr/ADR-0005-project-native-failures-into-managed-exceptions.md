# ADR-0005: Project native failures into idiomatic managed exceptions

- Status: Accepted
- Date: 2026-08-25
- Decision owner: TedToolkit.Occt maintainers
- Decision scope: Public managed exception semantics for every generated `TedToolkit.Occt`
  operation that consumes the ABI-major-1 native error contract.
- Applicable product intent: None
- Applicable principles: None
- Supersedes: None
- Superseded by: [ADR-0006](ADR-0006-generate-an-unversioned-matched-interop-boundary.md) for version-coupled native-boundary wording. The managed exception and diagnostic decisions remain active.
- Related decisions: [ADR-0001](ADR-0001-stable-c-interop-abi.md),
  [ADR-0003](ADR-0003-distribute-generated-occt-bindings.md), and
  [ADR-0004](ADR-0004-preserve-occt-managed-type-and-handle-semantics.md)

## Decision at a glance

Managed bindings expose the stable ABI error kind as `OcctErrorKind`, project applicable categories
into OCCT-specific subclasses of familiar .NET exceptions, and expose common native diagnostics
through `IOcctException` without allowing a C++ exception or native-owned string to cross the ABI.

## Context and decision question

ADR-0001 defines `ted_occt_v1_error` as the sole native failure carrier. It freezes the category
values, requires every nonzero value to remain a failure, carries optional native type, message, and
stack text, and requires cleanup in the native library. It deliberately leaves consumer error
projection open.

The Runtime currently contains metadata only and has no production imports, invocation bodies, or
managed error projection. Returning or silently discarding the ABI carrier would make ordinary C#
calls unlike the .NET exception model, while mapping every category to one custom exception would
discard useful standard catch semantics. Mapping only to built-in exceptions would make native
origin and diagnostics inconsistent because C# classes have one direct base class.

The decision question is: **how should generated managed calls represent ABI-major-1 failures so
that .NET callers receive familiar catch behavior while retaining stable OCCT identity and native
diagnostics?**

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | No C++ exception, native pointer, or native-owned diagnostic string may escape the ABI carrier lifetime. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | Every nonzero `int32_t` error kind, including a reserved value unknown to the managed build, remains a failure and preserves its numeric identity. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | Native diagnostic storage must be copied before `ted_occt_v1_error_clear` consumes its authoritative owner slot in the allocating native library. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Decision driver | Callers should be able to catch applicable argument, arithmetic, state, memory, and overflow failures through familiar .NET base types. | [.NET exception best practices](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions) | High |
| Decision driver | Every projected native failure should expose one typed, programmatic diagnostic contract independent of its .NET base exception. | C# permits one direct base class and multiple interfaces; see [C# inheritance](https://learn.microsoft.com/dotnet/csharp/fundamentals/object-oriented/inheritance) | High |
| Decision driver | Native and managed stacks must remain distinguishable because the managed stack begins at the managed throw site. | [.NET exception best practices](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions) and [ADR-0001](ADR-0001-stable-c-interop-abi.md) | High |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep the ABI carrier internal and expose no managed exception contract | The Runtime has this incomplete status quo. Confidence: High. | No | Managed calls cannot safely or idiomatically report native failure. | Rejected |
| Map every native failure to one `OcctException` hierarchy | A common base can carry every diagnostic. Confidence: High. | Partly | Callers lose standard `ArgumentException`, `InvalidOperationException`, and arithmetic catch behavior. | Rejected |
| Throw only built-in .NET exceptions | Applicable ABI categories resemble standard .NET failure categories. Confidence: High. | Partly | Native origin, error kind, type, and stack have no consistent strongly typed surface. | Rejected |
| Derive OCCT-specific exceptions from applicable .NET exception types and unify them with `IOcctException` | C# supports one class base plus interfaces, and .NET guidance prefers predefined semantic bases where they apply. Confidence: High. | Yes | The public API contains several exception types, and callers use the interface rather than one class to catch every native-origin failure. | Selected |

Construction policy alternatives are:

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Expose conventional public or protected exception constructors | This is familiar for ordinary application exceptions. Confidence: High. | Partly | Consumers could instantiate or derive the library-provided exception types with fabricated native diagnostics. | Rejected |
| Keep exception types public for catching while Runtime alone constructs library-provided native-origin instances | Runtime is already the single error projection and diagnostic owner. Confidence: High. | Yes | Consumers cannot directly instantiate or derive the library-provided exception types. | Selected |

## Decision

The public managed error category is `OcctErrorKind : int`. It defines the ABI-major-1 values
explicitly: `None = 0`, `Argument = 1`, `ArgumentOutOfRange = 2`, `Arithmetic = 3`,
`InvalidOperation = 4`, `NullObject = 5`, `OutOfMemory = 6`, `Overflow = 7`,
`OcctFailure = 8`, `StandardException = 9`, and `Unknown = 255`. An unrecognized nonzero `int32_t`
is represented by casting its unchanged value to `OcctErrorKind`; projection never rejects or
normalizes a reserved value. No thrown native-origin exception has kind `None`.

Every projected native failure implements this public contract:

```csharp
public interface IOcctException
{
    OcctErrorKind ErrorKind { get; }

    string? NativeTypeName { get; }

    string? NativeStackTrace { get; }
}
```

The managed exception mapping is:

| `OcctErrorKind` | Managed exception |
| --- | --- |
| `Argument` | `OcctArgumentException : ArgumentException, IOcctException` |
| `ArgumentOutOfRange` | `OcctArgumentOutOfRangeException : ArgumentOutOfRangeException, IOcctException` |
| `Arithmetic` | `OcctArithmeticException : ArithmeticException, IOcctException` |
| `InvalidOperation` | `OcctInvalidOperationException : InvalidOperationException, IOcctException` |
| `NullObject` | `OcctNullObjectException : OcctException` |
| `OutOfMemory` | `OcctOutOfMemoryException : OutOfMemoryException, IOcctException` |
| `Overflow` | `OcctOverflowException : OverflowException, IOcctException` |
| `OcctFailure`, `StandardException`, `Unknown`, or any reserved nonzero value | `OcctException : Exception, IOcctException` |

The exception types are public for catching, but expose no public or protected instance
constructors. `OcctException` has internal constructors so Runtime can throw it directly and derive
the internal hierarchy; every mapped leaf exception is sealed and has internal constructors.
External callers therefore cannot construct or derive the library-provided native-origin exception
types. These types add no legacy formatter-serialization constructors, attributes, or overrides;
binary exception serialization is not a supported contract.

`IOcctException` remains an open diagnostic contract because consumers can implement a public C#
interface. It does not authenticate native provenance. Runtime provenance applies only to the
library-provided `Occt*Exception` instances constructed from the ABI error carrier.

`OcctNullObjectException` does not derive from or project to `NullReferenceException`; that .NET
type is reserved for managed null dereference and would misstate the failure origin. Likewise,
`StandardException` never becomes a bare `System.Exception`.

Managed argument and lifetime validation performed before native invocation continues to throw the
ordinary applicable .NET exception and does not implement `IOcctException`. This distinguishes a
managed precondition failure from a failure returned by an executed native operation while still
allowing a broad catch such as `ArgumentException` to handle both when appropriate.

The native message supplies the managed exception message when it is non-null and nonempty and is
otherwise replaced with `Native OCCT operation failed with error kind <value>.`, where `<value>` is
the invariant decimal `int` value. Native-origin `ArgumentException` projections have
`ParamName == null`; `ArgumentOutOfRangeException` projections additionally have
`ActualValue == null`. Native projection does not invent a managed `InnerException`.
`NativeTypeName` and
`NativeStackTrace` are optional diagnostics, not compatibility identities; missing native text,
symbol unavailability, or diagnostic allocation failure cannot suppress the authoritative error
kind. The managed `Exception.StackTrace` remains the managed throw path.
Native stack text is exposed only through `NativeStackTrace` and is never substituted for or
concatenated into `Exception.StackTrace`.

The projection copies all available text before consuming each nonempty native error owner slot,
performs that consumption exactly once, and throws only after cleanup is complete. It does not
retain the ABI carrier, expose its pointers, or throw from the cleanup path.

The supported native delivery attempts bounded stack capture for `Standard_Failure`; OCCT stack
capture is disabled by default, so the native ABI module enables it once with a fixed positive
finite depth before the first generated operation. The depth is a private delivery constant and no
public configuration surface is introduced. The public property remains nullable because capture, allocation, and
symbolization can fail. Extending platform-independent stack capture to ordinary `std::exception`
and unknown thrown values requires evidence on each supported native matrix and does not change the
managed exception contract.

## Why this decision now

The first callable managed package will establish both an exception hierarchy and caller catch
behavior as public compatibility commitments. Selecting the hybrid model before generating method
bodies prevents each generated operation from inventing its own mapping and preserves the stable
ABI kind even when the managed build encounters a future additive category.

The interface is necessary because the selected exceptions must retain applicable .NET base
semantics and C# does not support multiple class inheritance. A single OCCT base class would make
the hierarchy uniform at the cost of familiar .NET behavior; untyped `Exception.Data` would avoid
new classes but would not provide a discoverable compile-time contract.

Reconsider the mapping if a category is shown not to satisfy the semantic contract of its selected
.NET base, or if a future .NET target prevents a selected inheritance relationship.

## Evidence and links

- [C interoperability ABI major 1](ADR-0001-stable-c-interop-abi.md)
- [Runtime current surface](../../src/core/TedToolkit.Occt.Runtime/README.md)
- [.NET exception best practices](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)
- [C# inheritance](https://learn.microsoft.com/dotnet/csharp/fundamentals/object-oriented/inheritance)
- [OCCT `Standard_Failure` reference](https://dev.opencascade.org/doc/refman/html/class_standard___failure.html)

## Consequences and accepted trade-offs

- Callers may catch familiar .NET base exceptions or identify every native-origin exception through
  `IOcctException`.
- The enum and exception inheritance relationships become public compatibility contracts alongside
  generated method signatures.
- Construction of the library-provided exception types remains Runtime-owned; consumers catch those
  types but cannot create or derive them. `IOcctException` itself remains open and does not prove
  native provenance.
- A reserved future ABI kind remains diagnosable through its numeric enum value and a generic
  `OcctException` without requiring a new managed package before the native call can fail safely.
- Runtime owns one handwritten error projection and native diagnostic lifetime boundary; generated
  methods invoke it uniformly rather than generating independent exception classes.
- Native stack text may expose implementation details and may be absent or unsymbolized. Consumers
  decide whether to log or display the dedicated property.
- Several public exception types are accepted to preserve both .NET catch semantics and typed OCCT
  diagnostics.

## Downstream delivery constraints

- The public enum uses explicit `int` values identical to ABI major 1 and preserves undefined
  nonzero values without throwing during projection.
- Every native-origin exception implements `IOcctException` and exposes the authoritative kind plus
  optional copied type, message, and native stack diagnostics.
- Exception inheritance and kind mapping follow the selected table exactly; `NullReferenceException`
  and bare `System.Exception` are never intentionally thrown for native failures.
- Public API baselines fix leaf sealing, constructor accessibility, and the absence of legacy
  formatter-serialization surface. Missing-message fallback and native argument metadata follow the
  selected deterministic rules.
- Managed precondition failures remain ordinary .NET exceptions and occur before native invocation.
- Every generated operation uses one Runtime-owned projection path that copies diagnostics, clears
  the authoritative ABI owner slot in the native library, and then throws.
- Diagnostic loss never changes success into failure or failure into success. Every nonzero kind,
  including reserved values, produces a managed exception even when all text pointers are null.
- The native delivery enables bounded, nonzero OCCT `Standard_Failure` stack capture once before
  generated operation use on the supported matrix; `NativeStackTrace` remains nullable and
  separate from the managed stack.
- Public API compatibility proof covers enum values, exception base types, leaf sealing,
  constructor accessibility, interface properties, and the absence of raw ABI carriers,
  diagnostic pointers, or legacy formatter-serialization surface.

## Exit requirements

A replacement must preserve released callers' catch behavior or define an explicit compatibility
transition, retain every ABI error value and native diagnostic cleanup guarantee, and continue to
distinguish native from managed stack information. Changing a mapped base exception or enum value
after release is a public compatibility change.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Accept or reject the managed exception projection. | TedToolkit.Occt maintainers | Before approving callable managed binding delivery | Closed |
| Reassess ordinary `std::exception` stack capture. | TedToolkit.Occt maintainers | A supported platform-independent capture and symbolization path is proved | Open |
| Reassess a mapped .NET base type. | TedToolkit.Occt maintainers | Consumer evidence shows the selected catch semantics are misleading | Open |
| Add a managed mapping for an additive ABI v1 category. | TedToolkit.Occt maintainers | ABI v1 assigns a currently reserved value | Open |
