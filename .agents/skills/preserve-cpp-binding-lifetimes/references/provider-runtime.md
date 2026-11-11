# Provider runtime semantics

## Cgal

- Keep CGAL-specific diagnostic kinds separate from Shared native errors and keep precondition diagnostics available without changing the carrier cleanup path.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.

## Fcl

- Keep FCL native errors on Shared’s error projection and preserve the provider’s explicit model build status.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.

## Manifold

- Keep Manifold native errors on Shared’s error projection and preserve the lifetime of returned native owners.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.

## Occt

- Preserve the distinction between the owning `Handle<T>` and the non-owning lowercase `handle<T>` storage projection.
- Use generated entry points for native operations and keep borrowed values tied to the relevant owner.
- Exercise an ordinary call and a provider failure to verify the intended outcome and cleanup behavior.
