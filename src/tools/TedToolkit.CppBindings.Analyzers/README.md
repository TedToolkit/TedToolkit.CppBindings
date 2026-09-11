# TedToolkit.CppBindings.Analyzers

A standalone compiler-only package for generated C++ binding hooks and borrowed owner references.
It has no Runtime assembly dependency and is not embedded in either Runtime package.

## Consume

The new package identity is unreleased. Build/pack it locally, then reference that local feed
directly in each consumer project:

```xml
<PackageReference Include="TedToolkit.CppBindings.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

With central package management, put the version in `Directory.Packages.props` instead.
The package supplies its assembly only under `analyzers/dotnet/cs`, not `lib`. Do not expect
a transitive dependency on Runtime to enable diagnostics.

## Diagnostics and suppression

- `TTCB001`: handwritten operational use of an API marked with
  `TedToolkit.CppBindings.GeneratedCodeOnlyAttribute`, including public members of a marked type.
  Use a generated factory/operation instead. Roslyn-recognized generated code and marked
  implementation scopes are exempt.
- `TTCB002`: locally demonstrable unsafe use of the non-owning `ICppOwner<T>.Value` contract.
  It recognizes interface implementations rather than provider type names. Supported hazards
  include escaping references, temporary owners, known disposal/alias disposal, suspension,
  routine operation receivers, and fixed pointer use without subsequent `GC.KeepAlive(owner)`.
  Ordinary scoped data access remains available.

Both rules default to errors and can be suppressed for an explicitly reviewed caller obligation:

```csharp
#pragma warning disable TTCB001, TTCB002
// Reviewed native-lifetime-sensitive interop code.
#pragma warning restore TTCB001, TTCB002
```

Project `NoWarn` and standard analyzer configuration can also suppress them. Suppression does not
change Runtime validation or make native access safe. These checks are not complete lifetime,
alias, concurrency, or use-after-free proof. Non-owning layout views that do not implement the
owner contract are not classified as managed owners.

## Verify locally

```powershell
dotnet run --project tests/TedToolkit.CppBindings.Analyzers.Tests/TedToolkit.CppBindings.Analyzers.Tests.csproj -c Release -- --report-trx
dotnet pack src/tools/TedToolkit.CppBindings.Analyzers/TedToolkit.CppBindings.Analyzers.csproj -c Release -o out/packages
pwsh -NoProfile -File build/VerifyAnalyzerPackage.ps1
```

Run commands from the repository root. Package validation also uses isolated direct-reference
consumers; a source-project test alone does not establish NuGet asset delivery. The verification
script requires PowerShell 7.5 and .NET 8 targeting references (downloaded from NuGet when needed).
It uses fresh package caches, verifies restored package hashes, and retains logs under `out/verification`.

[Generic Runtime](../../core/TedToolkit.CppBindings.Runtime/README.md) · [Repository overview](../../../README.md)
