# `Abi`

This directory owns the explicit, versioned C interoperability contract and the internal pipeline
that validates and emits its canonical C11 header.

## Responsibilities

- Define semantic ABI operations and complete cross-language type projections.
- Reject incomplete operations before they receive a stable exported name.
- Derive deterministic ABI-major-1 identities and emit the canonical header.
- Supply the currently approved ABI-major-1 conformance catalog.

It does not parse arbitrary Clang declarations or infer missing ABI semantics from generic binding
models.

## Organization

| Area | Responsibility |
| --- | --- |
| [`Contracts`](Contracts/README.md) | Passive semantic operation, value, and transport contracts. |
| `Validation` | Fail-closed completeness validation and deterministic rejection diagnostics. |
| `Generation` | Stable operation identity, collision checking, canonical C11 header emission, and generation output. |
| `Conformance` | The approved ABI-major-1 operation catalog used by native project materialization. |

## Dependencies and boundaries

The only supported flow is:

```text
Conformance -> Contracts -> Validation -> Generation
```

Generation consumes validated contracts; it must not fill missing projections or derive semantic IDs
from C++ or public managed spellings. Generic models must not depend on this directory. ABI changes
must preserve the versioning, transport, ownership, naming, error, and fail-closed rules in
[ADR-0001](../../../../docs/adr/ADR-0001-stable-c-interop-abi.md).

A new ABI type belongs in `Contracts` only when it adds contract vocabulary; validation outcomes and
diagnostics belong in `Validation`; deterministic naming or emitted artifacts belong in `Generation`;
approved version-specific mappings belong in `Conformance`. Avoid generic `Result`, `Helper`, or
`Model` files at this directory root.

## Change impact

Contract fields can affect symbol identity and compatibility even when generated C spelling does not
visibly change. Validation changes can alter supported coverage and diagnostics. Generation changes
can alter the canonical public header. Review all three boundaries together and preserve exact
generated text unless an approved ABI change explicitly says otherwise.

## Verify

From the repository root:

```powershell
dotnet build TedToolkit.Occt.slnx -c Release --no-restore
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
```

The focused regression areas are ABI operation identity, contract validation, C header generation,
and `GenerateCppModule` materialization.

## Related documentation

- [Generator overview](../README.md)
- [C interoperability ABI major 1](../../../../docs/interop-abi-v1.md)
- [ADR-0001: Establish a stable C interoperability ABI](../../../../docs/adr/ADR-0001-stable-c-interop-abi.md)

