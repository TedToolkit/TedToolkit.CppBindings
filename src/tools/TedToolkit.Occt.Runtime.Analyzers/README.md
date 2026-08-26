# TedToolkit.Occt.Runtime.Analyzers

This internal build component supplies compile-time guardrails for `TedToolkit.Occt.Runtime`
consumers. It reports
`TTOCCT001` when handwritten code references a Runtime member marked with
`GeneratedCodeOnlyAttribute` or a public field/callable member declared by a marked Runtime type.
Standard generated code is exempt.

`TTOCCT002` reports the supported locally demonstrable lifetime hazards around the non-owning
`Handle<T>.Value` and `Owned<T>.Value` references: escape, suspension, temporary owners, known
disposal, routine operation receivers, and fixed pointer use without a following owner
`GC.KeepAlive`. Ordinary scoped data access remains available.

The component is not a standalone library or NuGet package. The Runtime project builds it, embeds
its output under `analyzers/dotnet/cs` in the Runtime package, and does not load it while compiling
Runtime itself. The analyzer provides no runtime authorization, complete ownership or alias proof,
concurrency safety, or disposal rules for exact-layout structs.
