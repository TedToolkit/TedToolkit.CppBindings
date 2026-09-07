# TedToolkit.CppBindings.Cgal.Runtime

This package contains CGAL-specific runtime semantics used by independently generated bindings. It
depends only on the provider-neutral Runtime package and does not inherit OCCT ownership or failure
rules.

`NativeErrorProjection.ThrowIfFailed` copies strict UTF-8 type, message, and stack diagnostics,
clears the native-owned carrier exactly once in a `finally` path, and throws the documented
`CgalException` subtype. Malformed diagnostics become unavailable rather than preventing cleanup.
Success carriers are non-owning and are not cleared.

Generated operation-specific optional/variant/Object result types expose a finite `None` plus named
alternatives. A nonempty undeclared alternative or invalid tag throws `CgalUnknownResultException`.
Generated native adapters must destroy their temporary polymorphic container exactly once after
transfer or failure; no `std::variant`, `boost::any`, or borrowed temporary crosses the ABI.

Consumers catch the documented exception types. Only `CgalUnknownResultException` has a public
constructor because independently generated operation-specific result projections create it;
native-failure exceptions are constructed exclusively by `NativeErrorProjection`.
