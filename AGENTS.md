# AGENTS.md

## Testing

- This repository uses TUnit and Microsoft Testing Platform for test projects.
- Prefer running test projects with `dotnet run` instead of `dotnet test`.
- Use `--no-build` when the target has already been built, and pass `-- --report-trx` to emit TRX output when needed.

Example:

```powershell
dotnet run --project tests/TedToolkit.Occt.Generator.Tests/TedToolkit.Occt.Generator.Tests.csproj --no-build -- --report-trx
```

## Documentation language

- Write repository documentation, architecture records, change records, and new code comments in
  English.

## Preserve native semantics

- Generated bindings describe the capability and semantics of the C++ API; they do not make policy
  decisions for consumers.
- Preserve C++ reference returns directly: generate `ref readonly T` for `const T&` and `ref T` for
  `T&`.
- Never turn a borrowed reference return into an implicit value copy, `Owned<T>`, `Handle<T>`, clone,
  retain, or allocation. Copying or ownership changes require an explicit native operation chosen
  by the consumer.
- Document that returned references remain subject to the original C++ owner lifetime and
  invalidation rules; do not hide that obligation behind generated safety machinery.
- Project `opencascade::handle<T>` storage as the pointer-sized, non-owning lowercase `handle<T>`
  layout. Keep it distinct from uppercase `Handle<T>`, which owns and releases one intrusive
  reference.
- Keep lowercase `handle<T>` minimal: one private `T*` field and one public non-owning `ref T Value`
  property only. Expose no raw pointer, constructor, conversion, retain, release, or disposal API.
- Project an `opencascade::handle<T>` returned by value as owning uppercase `Handle<T>`; preserve
  lowercase `handle<T>` for fields, parameters, and borrowed references.
- Generate separate direct extension overloads for `Handle<T>` and `in handle<T>` receivers. Both
  call the same `NativeApi` slot; do not add a common public handle interface or a generated Core
  forwarding method. Apply owner liveness only to the uppercase overload.

## Change record retention

- Keep `docs/changes/` for active delivery records only.
- After a change is genuinely completed and its enduring decisions, current documentation, and
  verification coverage remain elsewhere in the repository, delete its completed change record;
  Git history provides delivery-record recovery only when that record was previously committed.
