# `Models`

This directory owns the normalized internal representation used to generate managed OCCT bindings.
It separates declaration structure from type projection and does not define the stable C ABI.

## Responsibilities

- Represent parsed records, methods, fields, parameters, and enums after Clang-specific details have
  been normalized for generation.
- Carry the source, P/Invoke, and public managed type projections used by declaration models.
- Keep ABI contracts, validation, naming, and header generation in [`../Abi`](../Abi/README.md).

## Organization

| Area | Responsibility |
| --- | --- |
| [`Declarations`](Declarations/README.md) | Normalized record, member, method, parameter, and enum declarations consumed by C# generators. |
| `Types` | Cross-language type projections and the result of resolving one Clang type. |

## Dependencies and boundaries

The processing direction is:

```text
Clang AST -> Resolver -> Types -> RecordModelManager -> Declarations -> C# generators
```

Declaration models may depend on `Types`. Neither area may depend on `Abi`, generator implementations,
modules, or services. Resolution behavior belongs in `Services`; emission behavior belongs in
`Generators`.

A new type belongs here only when it is stable data passed between parsing/resolution and managed
generation. Put declaration-shaped data in `Declarations` and reusable type-projection data in
`Types`. Do not place diagnostics, service results unrelated to this flow, generators, validators,
or version-specific ABI catalogs here.

## Change impact

Changes can affect recursive model discovery, generated layout, method and enum surfaces, XML
documentation, and type names. Review `RecordModelManager`, `Resolver`, `CSharpGenerator`, and
`EnumGenerator`, then run the full Generator test project.

## Verify

From the repository root:

```powershell
dotnet build TedToolkit.Occt.slnx -c Release --no-restore
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
```

## Related documentation

- [Generator overview](../README.md)
- [ABI internals](../Abi/README.md)

