# TedToolkit.CppBindings.Cgal.Runtime

This package contains CGAL-specific runtime semantics used by independently generated bindings. It
depends only on the provider-neutral Runtime package and does not inherit OCCT ownership or failure
rules.

`NativeErrorProjection.ThrowIfFailed` delegates copying, exact-once cleanup, and common kinds to
Shared. CGAL supplies only its local kinds 10 through 16 for error, precondition, postcondition,
assertion, test, warning, and failure diagnostics. Common failures throw the corresponding Shared
`INativeException`; malformed diagnostics become unavailable rather than preventing cleanup.
Success carriers are non-owning and are not cleared.

Generated operation-specific optional/variant/Object result types expose a finite `None` plus named
alternatives. A nonempty undeclared alternative or invalid tag throws `CgalUnknownResultException`.
Generated native adapters must destroy their temporary polymorphic container exactly once after
transfer or failure; no `std::variant`, `boost::any`, or borrowed temporary crosses the ABI.

Consumers catch Shared common exceptions or the documented CGAL-local exception types. Only
`CgalUnknownResultException` has a public constructor because independently generated
operation-specific result projections create it; native-failure exceptions are constructed
exclusively by projection.
