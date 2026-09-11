# OCCT compiler model adaptation

This directory contains only OCCT/Clang-facing transport results that have not yet crossed the
provider boundary. The provider converts those results into the normalized declarations under
[`TedToolkit.CppBindings.Generator.Semantics`](../../../../shared/TedToolkit.CppBindings.Generator/Semantics),
which are the single model consumed by both Shared emitters.

## Responsibilities

- Carry a compiler-specific resolution result while OCCT traverses Clang declarations.
- Keep Clang objects and OCCT classification out of Shared.
- Remain passive data: it does not emit source, invoke a compiler, or form another declaration graph.

## Dependencies and boundaries

The accepted processing direction is:

```text
native inputs -> OCCT/Clang parse and classify -> Shared normalized model
                                             -> Shared C# emitter
                                             -> Shared C++ emitter
                                             -> OCCT support/build metadata
```

Reusable declaration or type facts belong in Shared. A type belongs here only while it directly
depends on the OCCT parser or Clang AST and is consumed during conversion to Shared's model. Neither
Shared emitter may traverse Clang or infer semantics from the other emitter's text.

## Change impact

Changes can affect type resolution and conversion into Shared's model. Review `RecordModelManager`,
`Resolver`, both Shared emitters, and the full Generator test project.

## Verify

From the repository root:

```powershell
dotnet build TedToolkit.CppBindings.slnx -c Release --no-restore
dotnet run --project tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj -c Release --no-build -- --report-trx
```

## Related documentation

- [Generator overview](../README.md)
- [Repository design principles](../../../../../docs/principles/README.md)
- [Generated binding architecture](../../../../../docs/architecture/generated-binding-system.md)
