# `Abi/Contracts`

This directory owns the passive semantic vocabulary required to decide whether an operation can
enter the stable C ABI.

## Responsibilities

- Describe an operation's semantic owner, operation ID, category, receiver, ordered parameters, and
  optional result.
- Describe each value's direction, nullability, ownership, and explicit source, transport, adapter,
  P/Invoke, and public managed type projections.
- Preserve distinct parameter and result roles so a result cannot accidentally acquire a parameter
  name and a parameter cannot lose one.

## Organization

| Group | Responsibility |
| --- | --- |
| Operations | `AbiOperationModel`, `AbiParameterModel`, and `AbiResultModel` define the operation shape. |
| Type projection | `AbiTypeProjectionModel` keeps every cross-language representation explicit; incomplete candidates remain representable until validation. |
| Contract vocabulary | Operation kind, receiver kind, direction, nullability, and ownership enums replace ambiguous strings and Boolean combinations. |

## Dependencies and boundaries

Contracts contain data and local invariants only. They do not validate completeness, generate
diagnostics, hash identities, emit source, or select the ABI-major-1 surface. Those responsibilities
belong to sibling directories described by the [`Abi` README](../README.md).

The contracts intentionally do not fall back to a source C++ spelling when a transport or managed
projection is absent. A new contract type is justified only when it represents independent ABI
semantics that cannot be expressed without invalid states in an existing type.

## Change impact

Treat additions or reinterpretations as compatibility-sensitive: operation identity includes kind,
receiver, parameter direction/nullability/ownership/transport, and result semantics. Changes require
review against [ADR-0001](../../../../../docs/adr/ADR-0001-stable-c-interop-abi.md), validation,
identity tests, and canonical header tests.

## Verify

Run the Release build and full Generator TUnit project documented in the
[`Abi` README](../README.md#verify).

## Related documentation

- [ABI boundary](../README.md)
- [ABI-major-1 contract](../../../../../docs/interop-abi-v1.md)

