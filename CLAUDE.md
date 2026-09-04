# CLAUDE.md

## Testing

- This repository uses TUnit and Microsoft Testing Platform for test projects.
- Prefer running test projects with `dotnet run` instead of `dotnet test`.
- Use `--no-build` when the target has already been built, and pass `-- --report-trx` to emit TRX output when needed.

Example:

```powershell
dotnet run --project tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj --no-build -- --report-trx
```
