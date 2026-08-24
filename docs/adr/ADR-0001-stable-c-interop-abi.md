# ADR-0001: Establish a stable C interoperability ABI

- Status: Accepted
- Date: 2026-08-24
- Decision owner: TedToolkit.Occt maintainers
- Approval: User approval in the current Codex task on 2026-08-24.
- Decision scope: The native boundary between generated OCCT C++ adapters and non-C++ consumers, beginning with ABI version 1 and governing all later compatible evolution.
- Applicable product intent: None
- Applicable principles: None
- Supersedes: None
- Superseded by: None

## Decision at a glance

TedToolkit.Occt will expose OCCT through a versioned C11-compatible ABI made only from explicit transport scalars, semantic value structs, buffers, and typed opaque handles; generated C++ adapters will own all conversion, lifetime, and exception handling, and no C++ type or layout will cross the boundary.

## Context and decision question

The generator currently stores a raw C++ spelling, a P/Invoke type, and a public C# type in one projection model. The fallback resolver copies `ClangSharp.Type.AsString` into `CppTypeName`, and the C++ generator writes that string directly into functions declared with `extern "C"`. As a result, generated exports can contain C++ references, rvalue references, classes, `std::*` templates, `NCollection_*` templates, and `opencascade::handle<T>`. C linkage controls symbol naming; it does not make those parameter and return representations a C ABI.

The current runtime already assumes a native boundary: it uses `CallingConvention.Cdecl`, models a native error structure, and intends to own native OCCT objects. However, the generated C# methods do not yet invoke the generated symbols, and the handwritten `gp_Pnt2d` import still uses the placeholder library name `Name`. The existing generated surface is therefore a prototype, not a compatibility baseline.

OCCT adds two distinct object-lifetime models that the ABI must preserve:

- descendants of `Standard_Transient` participate in intrusive reference counting through `opencascade::handle<T>`; a handle increments the counter on acquisition and decrements it on release, deleting through `Standard_Transient::Delete()` only when the count reaches zero;
- ordinary OCCT records and value classes do not share that intrusive lifetime and require explicit value conversion or wrapper-owned allocation.

The decision question is: **what native protocol can represent the OCCT cases already visible in this repository without exposing compiler-specific C++ ABI details, ambiguous ownership, or exceptions to C# and other non-C++ consumers?**

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | A public export must be declarable and consumable as C11; C++ references, classes, templates, RTTI types, and standard-library types cannot appear in it. | [`TypeModel`](../../src/core/TedToolkit.Occt.Generator/Models/TypeModel.cs), [`Resolver`](../../src/core/TedToolkit.Occt.Generator/Services/Resolver.cs), and [`CppGenerator`](../../src/core/TedToolkit.Occt.Generator/Generators/CppGenerator.cs) show the current raw-type flow. | Must |
| Hard constraint | No C++ exception may cross the ABI. Failure must be observable without relying on thread-local global state. | [`csharp_interop.h`](../../src/core/TedToolkit.Occt.Cpp/csharp_interop.h) and [`interop_error`](../../src/core/TedToolkit.Occt.Runtime/interop_error.cs) already establish an exception-translation intent. | Must |
| Hard constraint | Allocation and release must occur in the same native library, including errors, strings, arrays, ordinary objects, and transient references. | The runtime currently calls native `free_error`; the existing header allocates error strings with `new[]`. | Must |
| Hard constraint | Ownership, nullability, direction, and valid lifetime must be explicit for every pointer-shaped transport value. | [`Handle<TElement>`](../../src/core/TedToolkit.Occt.Runtime/Handle.cs) owns a raw pointer, while [`handle<TElement>`](../../src/core/TedToolkit.Occt.Runtime/HandleValue.cs) is a non-owning view; current native deletion does not yet distinguish OCCT lifetime models safely. | Must |
| Hard constraint | Unsupported types must be rejected before an export is emitted; recursive discovery must not turn STL, compiler internals, or arbitrary templates into wrapper targets. | [`RecordModelManager`](../../src/core/TedToolkit.Occt.Generator/Services/RecordModelManager.cs) recursively adds record declarations and unwraps `opencascade::handle<T>`. | Must |
| Hard constraint | The native ABI is the authority. Managed P/Invoke and public C# projections consume it but cannot define or infer it. | Current `CSharpPInvokeType` and `CSharpPublicType` already represent different consumer concerns. | Must |
| Decision driver | The first boundary must cover common OCCT geometry values, `Standard_Transient` hierarchies, enums, strings, arrays, in/out references, and errors without promising all OCCT types. | Current target generation reaches `gp_*`, `Geom2d_*`, `NCollection_Array1<T>`, handles, strings, and stream methods. | High |
| Decision driver | ABI evolution must be detectable and additive within one major version, while incompatible majors can coexist in one process. | There is no released ABI baseline yet, so version 1 can establish versioned library, header, identifier, and symbol boundaries without migration. | High |
| Decision driver | The design should remain usable from .NET P/Invoke and a plain C consumer on each supported target triplet. | The runtime uses P/Invoke; the generated native library already uses portable visibility branches for Windows and non-Windows. | High |
| Assumption | Initial delivery and proof will target the repository's currently exercised Windows x64 vcpkg environment, while the protocol avoids Windows-only types. | The documented native-boundary baseline is `opencascade:x64-windows` 8.0.1; the delivery must supply the actual environment before native proof. | Medium |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep raw C++ parameter types under `extern "C"` | Documented in the current generator; generated signatures compile only as C++ and contain compiler-specific types. Confidence: High. | No | Minimal generator work, but no reliable C/P/Invoke contract and no portable ownership semantics. | Rejected |
| Generate an explicit C transport ABI with C++ adapters | C ABI primitives are supported by the existing library/P/Invoke shape; OCCT operations remain inside C++. Confidence: High for the boundary, Medium for the eventual supported-type breadth. | Yes | Requires explicit mapping rules, adapters, more exports, and compatibility discipline. | Selected |
| Replace the boundary with C++/CLI | It can directly consume C++ types. Confidence: High. | Partially | Couples the product to Windows, MSVC, and a managed C++ toolchain, and does not provide a general C consumer boundary. | Rejected |
| Bind to the compiler-specific C++ ABI through generated P/Invoke declarations | Current raw signatures approximate this direction. Confidence: High that compiler and layout coupling remains. | No | Avoids adapters but inherits mangling, layout, reference, exception, allocator, and toolchain compatibility risks. | Rejected |
| Expose a single serialized command/RPC boundary | Technically isolates C++ ABI. Confidence: Medium. | Partially | Adds serialization, dispatch, allocation, and debugging costs and loses the direct typed geometry API this repository is building. | Rejected |

## Decision

### 1. Boundary layers

Every projected type must have separate representations for:

1. the canonical OCCT C++ type and qualifiers;
2. the ABI transport type and its direction, nullability, and ownership;
3. the C++ adapter operations that convert to and from OCCT semantics;
4. the managed P/Invoke transport type; and
5. the public managed type.

No layer may fall back to the C++ spelling when its own representation is missing. A method is exportable only when every parameter, result, receiver, and error path has a complete ABI mapping.

### 2. Canonical ABI surface and major-version boundary

- The generator will produce a canonical header that is valid C11 and C++. This header, not generated C++ implementation text or managed code, defines the ABI.
- Exports use C linkage, default visibility, and the C calling convention (`cdecl`). Exported functions are non-throwing at the boundary.
- Every generated OCCT operation returns one `ted_occt_v1_error` value. OCCT results, constructed objects, and converted values are written through validated out parameters. A failure must not expose a partially initialized result. Bootstrap ABI-version and idempotent cleanup exports may be infallible and use direct return values where their complete contract is representable without an error payload.
- ABI major 1 uses canonical header `ted_toolkit_occt_v1.h`, native library basename `ted_toolkit_occt_abi_v1`, lowercase C identifiers beginning with `ted_occt_v1_`, and preprocessor constants beginning with `TED_OCCT_V1_`. A later incompatible major uses a distinct header, library, identifier namespace, and symbol prefix so both majors can coexist in one process.
- ABI version 1.0 is discoverable through `uint32_t ted_occt_v1_abi_version(void)`, encoded as `(major << 16) | minor`; version 1.0 is `0x00010000`. A consumer loads the major-specific library, calls this bootstrap export before any other operation, requires an equal major, and may accept an equal or newer compatible minor.
- Existing prototype symbols are not ABI version 1 and receive no compatibility promise.
- Exported symbol names are deterministic and globally unique under the rule below. Once ABI version 1 is released, a symbol's name, parameter types, result convention, ownership, nullability, and meaning are immutable within version 1.
- Public ABI structs begin with a size/version field when forward-compatible extension is required. Fixed semantic value structs are frozen instead of being extended in place.

Generated operation symbols have the form
`ted_occt_v1_<owner>_<operation>__<digest>`. `<owner>` and `<operation>` are readable ASCII stems
derived from the approved semantic owner and operation identifiers; they are not the uniqueness
authority. `<digest>` is the first 128 bits of SHA-256, rendered as 32 lowercase hexadecimal
characters, over this UTF-8 canonical identity:

```text
v1|owner=<semantic-owner-id>|kind=<constructor|method|operator|conversion|destroy|retain|release>
|operation=<semantic-operation-id>|receiver=<none|borrowed-const|borrowed-mutable>
|parameters=<direction>:<nullability>:<ownership>:<transport-id>,...
|result=<none|direction:nullability:ownership:transport-id>
```

Semantic IDs and transport IDs come from approved mapping rules, never from C# projected names,
compiler mangling, `ClangSharp.Type.AsString`, typedef spelling, or generated file order. Whitespace
is exactly as shown: the canonical identity is one line with no spaces or trailing newline. A
canonical-identity or exported-name collision is a generation error; the generator does not add an
order-dependent suffix. Fixed bootstrap and cleanup exports use their explicitly specified names
instead of this operation-name rule.

Owner, operation, and transport IDs are versioned lowercase ASCII tokens matching
`[a-z][a-z0-9_]*`. The readable stems use the same tokens. An OCCT identifier that cannot be assigned
an unambiguous token under an approved mapping is unsupported; locale-sensitive case conversion and
lossy punctuation removal are forbidden.

### 3. Error contract

- `ted_occt_v1_error` is the sole outcome carrier for native failure. There is no separate status return and no duplicated status field.
- `ted_occt_v1_error_kind` is a fixed-width `int32_t`. Kind `0` is success. Version 1 freezes the categories below; values `10` through `254` are reserved for additive version-1 categories.

```c
typedef int32_t ted_occt_v1_error_kind;

#define TED_OCCT_V1_ERROR_NONE                  ((ted_occt_v1_error_kind)0)
#define TED_OCCT_V1_ERROR_ARGUMENT              ((ted_occt_v1_error_kind)1)
#define TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE ((ted_occt_v1_error_kind)2)
#define TED_OCCT_V1_ERROR_ARITHMETIC            ((ted_occt_v1_error_kind)3)
#define TED_OCCT_V1_ERROR_INVALID_OPERATION     ((ted_occt_v1_error_kind)4)
#define TED_OCCT_V1_ERROR_NULL_OBJECT           ((ted_occt_v1_error_kind)5)
#define TED_OCCT_V1_ERROR_OUT_OF_MEMORY         ((ted_occt_v1_error_kind)6)
#define TED_OCCT_V1_ERROR_OVERFLOW              ((ted_occt_v1_error_kind)7)
#define TED_OCCT_V1_ERROR_OCCT_FAILURE          ((ted_occt_v1_error_kind)8)
#define TED_OCCT_V1_ERROR_STD_EXCEPTION         ((ted_occt_v1_error_kind)9)
#define TED_OCCT_V1_ERROR_UNKNOWN               ((ted_occt_v1_error_kind)255)

typedef struct ted_occt_v1_error
{
    ted_occt_v1_error_kind kind;
    const char* type_name;
    const char* message;
    const char* stack_trace;
} ted_occt_v1_error;

uint32_t ted_occt_v1_abi_version(void);
void ted_occt_v1_error_clear(ted_occt_v1_error* error);
```

- `kind` is authoritative. `TED_OCCT_V1_ERROR_NONE` means success and requires all text pointers to be null. Every nonzero value means failure, including a value introduced by a newer compatible minor version that the consumer does not recognize, even when one or all text allocations are unavailable.
- `type_name`, `message`, and `stack_trace` are optional, NUL-terminated UTF-8 diagnostic strings. They do not support embedded NUL and do not need explicit length fields.
- Diagnostic string storage is owned by `ted_toolkit_occt_abi_v1`. The caller may read but must not mutate or free that storage. Ownership transfers from the callee into the single receiving `ted_occt_v1_error` owner slot. A non-empty error is move-only at the contract level: callers must not bitwise-copy it, because copying does not duplicate ownership and clearing either duplicate would leave the other stale.
- `ted_occt_v1_error_clear` consumes the authoritative owner slot, frees its diagnostic strings, resets its kind to `NONE`, and nulls every pointer. Passing a null slot or an already-empty error is safe, so clearing the same authoritative slot repeatedly is idempotent; this guarantee does not make stale copied values safe.
- The kind converts native exceptions into stable consumer categories while `type_name` preserves the concrete C++ or OCCT exception type for diagnostics.
- Native adapters map caught exceptions in the following precedence order. Each row is tested before every later row; the first matching row supplies the kind.

| Precedence | Caught native type | Kind |
| ---: | --- | --- |
| 1 | `Standard_OutOfMemory` | `OUT_OF_MEMORY` |
| 2 | `Standard_NullObject`, `Standard_NullValue` | `NULL_OBJECT` |
| 3 | `Standard_NoSuchObject`, `Standard_ProgramError` | `INVALID_OPERATION` |
| 4 | `Standard_OutOfRange`, `Standard_RangeError` | `ARGUMENT_OUT_OF_RANGE` |
| 5 | `Standard_Overflow` | `OVERFLOW` |
| 6 | `Standard_Underflow` | `ARITHMETIC` |
| 7 | `Standard_ConstructionError`, `Standard_DimensionMismatch`, `Standard_DimensionError`, `Standard_DomainError` | `ARGUMENT` |
| 8 | any other `Standard_Failure` | `OCCT_FAILURE` |
| 9 | `std::bad_alloc` | `OUT_OF_MEMORY` |
| 10 | `std::out_of_range` | `ARGUMENT_OUT_OF_RANGE` |
| 11 | `std::overflow_error` | `OVERFLOW` |
| 12 | `std::underflow_error` | `ARITHMETIC` |
| 13 | `std::invalid_argument`, `std::domain_error` | `ARGUMENT` |
| 14 | any other `std::logic_error` | `INVALID_OPERATION` |
| 15 | any other `std::exception` | `STD_EXCEPTION` |
| 16 | any other thrown value | `UNKNOWN` |

`Standard_Failure` derives from `std::exception` in the OCCT 8.0.1 baseline, and several specifically mapped OCCT types derive from broader mapped OCCT types. The ordering above is therefore part of the contract, not an implementation detail. Unlisted OCCT subclasses inherit the first listed base category they match. Consumers map the stable kind to their error model and may use `type_name` only to refine diagnostics; a concrete C++ type name is not a compatibility contract.
- Failure remains reportable through `kind` even if diagnostic allocation fails. Error construction is non-throwing: if any optional text allocation fails, it releases any diagnostic storage already acquired, returns the original authoritative kind with all text pointers null, and does not terminate the process.
- No exported operation depends on a process-global or thread-local “last error.” Concurrent calls receive independent error values.
- All native exceptions, including `Standard_Failure`, `std::exception`, and unknown exceptions, are caught before the ABI boundary. Native destructors and release functions are also non-throwing at the boundary.
- Operations known not to throw may avoid a catch path, but they still return an empty `ted_occt_v1_error` so wrapper validation and consumer invocation remain uniform.

### 4. Approved transport vocabulary

| C++ semantic case | ABI version 1 direction | Required rule |
| --- | --- | --- |
| `void` | No result payload | The `ted_occt_v1_error` return remains present. |
| `bool` / `Standard_Boolean` | `uint8_t` | `0` is false, `1` is true; other input values are invalid. Native `bool` layout is never exposed. |
| Signed and unsigned integers | Exact-width `int8_t` through `uint64_t` | Width and signedness come from the parsed target ABI. Narrowing and out-of-range conversion fail rather than wrap silently. C/C++ `long` is not exposed directly. |
| `float` / `double` | `float` / `double` | IEEE representation is required on a supported target. `long double`, 128-bit, half, and compiler-specific floating types remain unsupported until separately specified. |
| Character code units | `uint8_t`, `uint16_t`, or `uint32_t` | Encoding is part of the mapping. Plain `wchar_t` is never an ABI type because its width is platform-dependent. |
| Address-sized integer semantics | `uintptr_t` / `intptr_t` | Allowed only when the native meaning is actually pointer-sized, not as a substitute for an unknown integer width. |
| Count, byte length, and element length | `uint64_t` | Adapters range-check before converting to `size_t` or OCCT integer types. |
| Enum | Fixed-width signed or unsigned integer | The exact underlying width is recorded. Named constants may be emitted for C; the C++ enum object itself is not exposed. |
| Nullable scalar | `{ has_value, value }` semantic transport | Sentinel values are used only when the OCCT contract already defines that sentinel. |
| Function pointer or callback | Unsupported by default | A later decision must define calling convention, `user_data`, threading, reentrancy, exception containment, and registration lifetime. |

### 5. Records and value semantics

- A C++ record is opaque by default. Its `sizeof`, field offsets, vtable, base layout, padding, and compiler-generated special members are not an ABI contract.
- A small value such as a point, vector, direction, axis, matrix, quaternion, or transform may receive a named C semantic transport struct only when its meaningful components and invariants are known. The adapter reads and writes those components through OCCT operations; it does not `memcpy` the C++ object representation.
- Each semantic value struct has fixed-width fields, frozen order, defined units, and defined validation behavior. A structurally similar OCCT class does not automatically share the same transport type.
- Mutable C++ references use explicit input/output or in/out transport pointers. Const references are borrowed inputs for the duration of the call. C++ references never appear in the header.
- A returned C++ reference or pointer cannot escape as an unowned C pointer. It is copied into a semantic value, retained as an owned transient handle, or represented by a new owned ordinary-object wrapper. If none is valid, the member is unsupported.
- Bitfields, unions, vector extensions, compiler runtime records, RTTI objects, iostreams, allocators, iterators, and implementation-detail records are unsupported unless a separate semantic adapter is approved.

### 6. Ordinary non-transient objects

- A non-transient OCCT class that lacks an approved semantic value struct is represented by a typed opaque pointer.
- Creation and copy/clone operations return an owned opaque object. Every owned object has a matching destroy operation in the same native library.
- Inputs borrow the object for the duration of the call. An operation that stores an input beyond the call must instead take or create an owned object according to an explicit member contract.
- Consumers never call `delete`, invoke a C++ destructor, or free object storage directly.
- Destroy consumes exactly one ownership token and accepts a pointer to the caller's owner slot so that success can replace it with null. Destroying an already-null slot is safe. Releasing a stale copied pointer is invalid; copying a pointer does not duplicate ownership.
- Direct field exposure is disallowed for opaque objects; behavior is accessed through generated functions.

### 7. `Standard_Transient` and `opencascade::handle<T>`

- A transient object is represented by a typed opaque pointer to the target object, never by the binary representation of `opencascade::handle<T>`.
- Each handle returned across the boundary is owned by the caller and accounts for one intrusive reference. Returning from a temporary native handle must retain the target before the temporary handle releases it.
- An input handle is borrowed only for the call. The consumer must keep its owned handle alive until the call finishes. A native operation that stores the handle creates its own OCCT reference.
- Retain duplicates ownership by calling the OCCT reference-count operation. Release decrements the OCCT reference count and calls `Delete()` only when OCCT reports zero. Direct `delete` of a transient target is forbidden.
- Retain is the only operation that duplicates a transient ownership token. Release consumes one token through a pointer to the caller's owner slot and nulls that slot. Releasing an already-null slot is safe; releasing a stale bitwise copy that was not retained is invalid.
- Null handles are represented by null pointers only where the specific OCCT API permits null. Required receivers and arguments are validated before dereference.
- Upcasts, checked downcasts, and runtime type queries occur through native adapter exports. Consumers do not reinterpret opaque handle types.
- No borrowed transient result escapes the call. A returned raw pointer or reference to a transient target is promoted to an owned handle or rejected.
- Weak handles are unsupported because OCCT handles do not provide weak-pointer semantics.

This follows the documented OCCT handle model: `Standard_Transient` owns the atomic reference counter, and `opencascade::handle<T>` increments on acquisition and decrements on release, deleting when the count reaches zero.

### 8. Strings, buffers, and collections

- Input byte/text buffers are borrowed pointer-plus-length views. A null pointer is valid only with zero length unless a member explicitly allows a null optional value.
- `const char*`, `Standard_CString`, and `TCollection_AsciiString` map to UTF-8 when the source API is textual. `TCollection_AsciiString::Length()` is a byte count, not a Unicode scalar count.
- `char16_t*`, `Standard_ExtString`, and `TCollection_ExtendedString` may map to UTF-16 code-unit views. `wchar_t*` is not a portable transport and requires conversion inside the adapter.
- An API that requires NUL termination receives an adapter-owned temporary terminator. Embedded NUL is supported only when the target OCCT operation has an explicit length contract; otherwise it is rejected.
- Output text and arbitrary output bytes use either a caller-sized two-call protocol or a library-owned buffer with an explicit release operation. A borrowed pointer into an OCCT object does not escape.
- `NCollection_Array1<T>` and similar contiguous collections may map to an element view only when `T` has an approved transport mapping. Array1 adapters also carry the logical lower bound because OCCT indexing is not universally zero-based.
- Collection adapters preserve input/output direction and ownership. They do not expose the C++ template object, allocator, iterator, or `myIsOwner` representation.
- Nested, associative, node-based, polymorphic, or non-contiguous collections remain unsupported until a dedicated adapter defines their observable semantics.

ABI version 1 uses these canonical byte-buffer carriers. A mapping states separately whether the
bytes are text and which encoding applies. `ted_occt_v1_owned_bytes` is move-only at the contract
level and is cleared through its authoritative owner slot by the allocating library.

```c
typedef struct ted_occt_v1_bytes_view
{
    const uint8_t* data;
    uint64_t length;
} ted_occt_v1_bytes_view;

typedef struct ted_occt_v1_owned_bytes
{
    uint8_t* data;
    uint64_t length;
} ted_occt_v1_owned_bytes;

void ted_occt_v1_owned_bytes_clear(ted_occt_v1_owned_bytes* buffer);
```

An empty view or owned buffer has a null pointer and zero length. A non-empty view requires a
non-null pointer. Cleanup accepts a null owner slot or an already-empty authoritative slot, releases
storage in `ted_toolkit_occt_abi_v1`, and resets the slot to the empty state. Copying an owned buffer
does not duplicate ownership; clearing a stale copy is invalid.

### 9. Methods, constructors, conversions, and overloads

- Instance receivers are explicit first parameters. Receivers are required unless the member is explicitly static or nullable by OCCT contract.
- Constructors write a semantic value, owned ordinary object, or owned transient handle to an out parameter only after successful construction.
- Destruction is represented by the matching value/object/handle release contract, not a generated C++ destructor call on consumer memory.
- A non-const lvalue reference is modeled as in/out only when writes are part of the documented OCCT behavior. A const lvalue reference is input. An rvalue reference or consuming move is unsupported until a member-specific ownership-transfer contract exists.
- C++ operator and conversion methods may be projected only when their semantic operation and all transport types are unambiguous. C++ overload resolution never occurs at the ABI; each overload has a distinct stable symbol.
- Default arguments are resolved by the generated public API or by separate exports; they are not encoded in the C ABI.
- Variadic functions are unsupported.

### 10. Validation, threading, and unsupported cases

- Adapters validate required pointers, boolean domain, length/pointer consistency, range conversions, and output storage before invoking OCCT.
- The ABI adds no synchronization to OCCT objects. Concurrent use of one object is allowed only when the underlying OCCT type and operation are documented as safe.
- Retain/release follows OCCT's atomic counter semantics, but that does not make other object operations thread-safe.
- Callbacks, long-lived borrowed views, platform handles, GPU resources, file descriptors, custom allocators, placement construction, multiple-inheritance pointer adjustment not covered by native casts, and types whose lifetime cannot be proven are unsupported by default.
- An unsupported declaration produces a deterministic generator diagnostic naming the declaration, source location, source type, missing transport rule, and required ownership/direction information. It does not trigger recursive wrapper generation and does not emit a partial export.

### 11. Compatibility and versioning

- ABI version 1 begins only after this decision is Accepted and its delivery passes the approved C consumer boundary proof. Current generated exports and handwritten runtime imports are pre-version prototypes.
- Within ABI version 1, new symbols and new enum constants may be added. Existing symbols, transport layouts, numeric error-kind values, encodings, ownership rules, and release obligations may not be changed or removed.
- A breaking change requires ABI version 2, its own header, library basename, C identifier namespace, symbol prefix, and an explicit transition decision. Version 1 remains loadable while supported; version 2 does not replace a version-1 library in place.
- The initial conformance and release matrix is Windows x64, the MSVC x64 ABI, `cdecl`, and OCCT 8.0.1 from vcpkg triplet `x64-windows`. The portable C vocabulary is a design constraint, not evidence that another triplet is supported. A new triplet joins ABI version 1 only after its C layout, calling convention, ownership, error, and representative OCCT behavior pass the same boundary proof.
- Rebuilding the same ABI against a newer OCCT version is allowed only when the canonical header is compatible and contract and behavioral proofs show that the exposed semantics remain compatible.
- The runtime must verify that the loaded native library reports the ABI major it was generated to consume before using other exports.
- Target architecture and calling convention remain part of binary compatibility. Fixed-width transport types remove avoidable source-language ambiguity but do not make binaries portable across CPU architectures.

## Why this decision now

The project cannot safely complete managed invocation while its native symbols still expose raw C++ semantics. Separating the ABI now is cheaper than preserving accidental signatures after consumers ship. The selected option retains the project's existing generator/native-library/runtime shape while replacing the unsafe boundary with an explicit protocol.

The status quo and compiler-specific ABI option fail the non-negotiable C/P/Invoke requirement. C++/CLI would solve only one managed platform and replace the current architecture. A serialized command boundary would be defensible for process isolation, but no current requirement justifies its complexity or loss of direct typed calls.

This decision should be reconsidered if the product deliberately becomes Windows/MSVC-only, moves OCCT into a separate process, requires language-neutral remote calls, or adopts an authoritative upstream C API that covers the required OCCT surface.

## Evidence and links

Repository evidence:

- [`Helpers`](../../src/core/TedToolkit.Occt.Generator/Helpers.cs) maps Clang built-ins, pointers, references, and public managed types today.
- [`Resolver`](../../src/core/TedToolkit.Occt.Generator/Services/Resolver.cs) currently falls back to raw C++ spelling and recursively exposes record declarations.
- [`RecordModelManager`](../../src/core/TedToolkit.Occt.Generator/Services/RecordModelManager.cs) discovers records, fields, methods, inheritance, enums, templates, and OCCT handle specializations.
- [`CppGenerator`](../../src/core/TedToolkit.Occt.Generator/Generators/CppGenerator.cs) currently writes C++ receiver/reference/result types into exported functions and directly increments or deletes transient instances.
- [`CSharpGenerator`](../../src/core/TedToolkit.Occt.Generator/Generators/CSharpGenerator.cs) demonstrates the existing separation between public type intent and the still-incomplete invocation layer.
- [`csharp_interop.h`](../../src/core/TedToolkit.Occt.Cpp/csharp_interop.h) and [`interop_error`](../../src/core/TedToolkit.Occt.Runtime/interop_error.cs) establish the existing error translation and same-library release intent.
- [`Handle<TElement>`](../../src/core/TedToolkit.Occt.Runtime/Handle.cs), [`handle<TElement>`](../../src/core/TedToolkit.Occt.Runtime/HandleValue.cs), and [`IStandard_Transient`](../../src/core/TedToolkit.Occt.Runtime/IStandard_Transient.cs) show the current managed ownership/view model and the unresolved native release contract.

External primary evidence:

- [OCCT V8_0_1 source baseline](https://github.com/Open-Cascade-SAS/OCCT/tree/V8_0_1).
- [OCCT Foundation Classes: Programming with Handles](https://github.com/Open-Cascade-SAS/OCCT/wiki/foundation_classes).
- [OCCT `Standard_Transient` reference](https://dev.opencascade.org/doc/refman/html/class_standard___transient.html).
- [OCCT Foundation Classes guide, including `NCollection_Array1`](https://dev.opencascade.org/doc/overview/html/occt_user_guides__foundation_classes.html).

## Consequences and accepted trade-offs

Positive consequences:

- C, P/Invoke, and other foreign-function consumers receive one testable source of ABI truth.
- Compiler-specific layouts, references, templates, exceptions, and allocators remain behind the C++ adapter.
- Ownership and release become reviewable per parameter and result rather than inferred from pointer syntax.
- Unsupported OCCT surface fails explicitly instead of silently generating unusable bindings.
- ABI compatibility can be evaluated independently from public C# API evolution.

Accepted costs and limitations:

- The generator needs a richer type model and semantic adapters; automatic raw-type projection is no longer acceptable.
- Some values require copies and some calls require temporary OCCT objects or handles.
- One OCCT method may require several C exports or helper operations for construction, buffers, casts, retain/release, and errors.
- Initial supported coverage will be intentionally smaller than the set of declarations Clang can parse.
- ABI versioning and cross-language boundary tests become permanent maintenance obligations.
- Opaque objects reduce direct field access and may allocate where a native C++ caller would use stack values.

Security and reliability consequences:

- All length conversions, pointers, handles, and out parameters become explicit validation boundaries.
- Use-after-free, double-release, invalid downcast, buffer overrun, and allocator mismatch remain material risks and require contract-level proof.
- Error type names and native stack text may expose implementation details; consumers decide whether to surface them, while the native ABI preserves them for diagnostics.

## Downstream delivery constraints

- A generated export is valid only when every receiver, parameter, result, error, lifetime, nullability, and direction has an approved ABI mapping.
- The canonical generated ABI declaration must compile as C11 and C++ and must not include OCCT or C++ standard-library headers.
- Generated implementation code may use OCCT and C++, but every exported declaration must use only the approved transport vocabulary.
- Every generated OCCT operation returns `ted_occt_v1_error`; result values are committed only when its kind is `NONE`.
- Every library-owned allocation has a same-library release path. Error and buffer clear operations, and owner-slot release after that slot has been nulled, are idempotent. Releasing a stale copied object or handle token is invalid.
- A non-empty error value has one authoritative owner slot and must not be copied. `ted_occt_v1_error_clear` is idempotent only for that slot after it has been reset; a stale copied error is invalid.
- Transient releases follow OCCT reference counting and never directly delete a live `Standard_Transient` target.
- Ordinary opaque objects and transient handles remain distinct ownership categories even if both are represented by pointer-sized values.
- Semantic value structs are defined from meaning and invariant, never inferred from C++ `sizeof` and field offsets alone.
- Unsupported mappings fail generation before emitting a partial symbol.
- ABI version 1 compatibility and exact calling-convention proof are required from a real C consumer, not only from C++ compilation or textual signature snapshots.

## Exit requirements

Replacing this direction requires a successor that preserves or deliberately migrates released ABI consumers, supplies an explicit ownership and error boundary, and prevents C++ exceptions and compiler-specific layouts from reaching non-C++ callers. An accepted successor ADR must define coexistence or removal rules for every released ABI major before this decision can be superseded.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Review and either accept or reject this proposed ABI direction. | TedToolkit.Occt maintainers | Before approving the explicit interop ABI delivery change | Completed 2026-08-24 |
| Preserve the canonical C consumer boundary as CMake presets named `ted-occt-abi-v1-consumer` for configure/build and CTest, exercising the generated `ted_toolkit_occt_v1.h` against the built `ted_toolkit_occt_abi_v1` shared library. | TedToolkit.Occt maintainers | Before ABI version 1 delivery can complete | Open |
| Reassess type vocabulary and compatibility. | TedToolkit.Occt maintainers | Any required callback, cross-process boundary, new CPU ABI, public custom allocator, unsupported GPU/platform resource, or breaking OCCT upgrade | Open |
| Reassess transient ownership. | TedToolkit.Occt maintainers | Any evidence that a required OCCT API cannot safely promote returned transient pointers to owned handles | Open |

When superseded, retain this record, update its status and `Superseded by` field, and place the changed decision in a new ADR rather than rewriting this rationale.
