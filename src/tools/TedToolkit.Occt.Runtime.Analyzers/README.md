# TedToolkit.Occt.Runtime.Analyzers

This package supplies compile-time guardrails for `TedToolkit.Occt.Runtime` consumers. It reports
`TTOCCT001` when handwritten code references a Runtime member marked with
`GeneratedCodeOnlyAttribute` or a callable member declared by a marked Runtime type. Standard
generated code is exempt.

`TTOCCT002` reports the supported locally demonstrable lifetime hazards around the non-owning
`Handle<T>.Value` and `Owned<T>.Value` references: escape, suspension, temporary owners, known
disposal, routine operation receivers, and fixed pointer use without a following owner
`GC.KeepAlive`. Ordinary scoped data access remains available.

The package contains compiler assets only. It does not provide runtime authorization, complete
ownership or alias proof, concurrency safety, or disposal rules for exact-layout structs.
