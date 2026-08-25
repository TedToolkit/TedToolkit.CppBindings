# `Models/Declarations`

This directory owns normalized C++ declaration shapes that the managed generators can consume
without traversing the Clang AST again.

## Responsibilities

- Represent records and their inheritance, layout, fields, and methods.
- Represent parameters and method categories separately because they have different generation and
  filtering rules.
- Represent enums and enum members separately because declaration metadata and member expressions
  have different consumers.

## Organization

| Group | Responsibility |
| --- | --- |
| Records | `RecordModel` owns one projected record; `FieldModel` and `MethodModel` own its ordered members. |
| Calls | `ParameterModel` owns parameter metadata; `MethodModelType` classifies constructors, destructors, operators, conversions, and ordinary methods. |
| Enums | `EnumModel` owns enum-level metadata; `EnumMemberModel` owns individual documented values. |

## Dependencies and boundaries

These models may depend on [`../Types`](../Types) for type projections and on RoslynHelper's
documentation syntax abstractions. They must remain passive normalized data: Clang traversal belongs
in `RecordModelManager`, type conversion belongs in `Resolver`, and source emission belongs in the
managed generators.

Do not reuse these declarations as ABI contracts. The ABI requires explicit direction, nullability,
ownership, transport, and adapter semantics owned by [`../../Abi/Contracts`](../../Abi/Contracts/README.md).

A new declaration model belongs here when it represents a distinct parsed declaration role with
different required data or generation rules. Do not create a new model merely to rename an existing
property group or wrap a single nullable result.

## Change impact

Changing a declaration model normally requires coordinated updates to `RecordModelManager`, service
interfaces, C# or enum generation, and their tests. Generated C# text is the observable regression
boundary.

## Verify

Run the Release build and the full Generator TUnit project documented in the
[`Models` README](../README.md#verify).

## Related documentation

- [Models boundary](../README.md)
- [Generator pipeline](../../README.md#2-build-the-model-from-the-clang-ast)

