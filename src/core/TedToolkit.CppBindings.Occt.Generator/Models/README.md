# `Models`

This directory owns the normalized internal representation used by every binding emitter. It
separates declaration structure from physical and public type projection and remains the only
semantic boundary between native parsing and generated C# or C++ source.

## Responsibilities

- Represent parsed records, methods, fields, parameters, and enums after Clang-specific details have
  been normalized for generation.
- Carry the source, P/Invoke, and public managed type projections used by declaration models.
- Carry layout, transport, ownership, conversion, error, cleanup, and build-description facts
  required by both C# and C++ emitters. The generic plan snapshots the final source/export inventory;
  it does not expose this private OCCT graph.
- Remain passive data: it does not emit source, invoke a compiler, or depend on generated text.

## Organization

| Area | Responsibility |
| --- | --- |
| [`Declarations`](Declarations/README.md) | Normalized record, member, method, parameter, and enum declarations consumed by C# generators. |
| `Types` | Cross-language type projections and the result of resolving one Clang type. |

## Dependencies and boundaries

The accepted processing direction is:

```text
native inputs -> parse and normalize -> complete Model
                                      -> C# emitter
                                      -> C++ and native-build-description emitter
                                                              -> optional native compiler
```

Declaration models may depend on `Types`. Neither area may depend on generator implementations,
modules, services, emitted source, or compiler output. Resolution behavior belongs in `Services`;
emission behavior belongs in `Generators`. An emitter must not traverse Clang or reconstruct Model
semantics from another emitter's output.

A new type belongs here only when it is stable data passed between parsing/resolution and one or
more emitters. Put declaration-shaped data in `Declarations` and reusable type-projection data in
`Types`. Do not place diagnostics, service results unrelated to this flow, generators, validators,
compiler execution, or a second operation catalog here.

## Change impact

Changes can affect recursive model discovery, generated layout, method and enum surfaces, XML
documentation, and type names. Review `RecordModelManager`, `Resolver`, `CSharpGenerator`, and
`EnumGenerator`, then run the full Generator test project.

## Verify

From the repository root:

```powershell
dotnet build TedToolkit.CppBindings.slnx -c Release --no-restore
dotnet run --project tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
```

## Related documentation

- [Generator overview](../README.md)
- [Repository design principles](../../../../docs/principles/README.md)
- [Generated binding architecture](../../../../docs/architecture/generated-binding-system.md)
