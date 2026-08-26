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

## Change record retention

- Keep `docs/changes/` for active delivery records only.
- After a change is genuinely completed and its enduring decisions, current documentation, and
  verification coverage remain elsewhere in the repository, delete its completed change record;
  Git history provides delivery-record recovery only when that record was previously committed.
