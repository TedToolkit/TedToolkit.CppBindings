# ADR-0003: Distribute generated OCCT bindings as a ready-to-use package

- Status: Accepted
- Date: 2026-08-25
- Decision owner: TedToolkit.Occt maintainers
- Approval: User approval in the current Codex task on 2026-08-25.
- Decision scope: Generation, packaging, platform claims, and dependency boundaries for the public
  `TedToolkit.Occt` managed binding package.
- Applicable product intent: None
- Applicable principles: None
- Supersedes: None
- Superseded by: None

## Decision at a glance

`TedToolkit.Occt` will be generated from every public OCCT `.hxx` header during the controlled
release build, ship supported managed bindings and their complete RID-specific native runtime
closure in one ready-to-use NuGet package, and omit unsupported declarations with explicit
diagnostics and coverage evidence instead of exposing STL or unproven lifetime semantics.

## Context and decision question

The repository currently contains a generator, a shared managed runtime, and a versioned C ABI,
but no public binding package. Generation depends on a machine-level vcpkg installation, accepts an
explicit list of headers, and does not yet produce a complete managed invocation layer. Consumers
therefore cannot install one package and call the generated OCCT surface.

The requested public library must consider all OCCT public headers and remain architecturally
portable to Windows and Linux. The available environment can prove only Windows x64. OCCT headers
also expose STL, compiler-specific implementation types, complex containers, and operations whose
ownership cannot always be established; treating parseability as safe interop would violate the
ABI rules in ADR-0001.

The decision question is: **where should generation occur, what does full-header coverage mean,
and how should managed and native artifacts be packaged without claiming unsupported surface or
platforms?**

## Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | No C++ type, STL type, compiler-specific type, exception, or unproven ownership crosses the public C ABI. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) | Must |
| Hard constraint | Every public OCCT `.hxx` header is considered and receives an observable coverage result; this does not imply that every declaration is exportable. | Requested product scope; the current generator selects only named headers. | Must |
| Hard constraint | Consumers install and use the binding without vcpkg, Clang, CMake, the Generator, or an OCCT development environment. | The requested artifact is a consumable library rather than a generator development entry point. | Must |
| Hard constraint | A platform is advertised as supported only after its managed/native package is exercised on that platform. | The repository currently proves only Windows x64. | Must |
| Hard constraint | Native assets follow NuGet RID selection and include the complete runtime dependency closure needed by the binding. | [Native files in .NET packages](https://learn.microsoft.com/nuget/create-packages/native-files-in-net-packages) | Must |
| Decision driver | Generation must be reproducible from a pinned OCCT input and generator revision. | The current machine-level vcpkg input is not pinned. | High |
| Decision driver | Unsupported surface must be reviewable rather than silently disappearing or failing the entire supported subset. | ADR-0001 requires deterministic rejection before partial export. | High |
| Decision driver | Adding Linux later should not change the managed API or ABI-major-1 transport contract merely because the native artifact changes. | [ADR-0001](ADR-0001-stable-c-interop-abi.md) and the [.NET RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog) | High |
| Assumption | The first package targets SDK-style .NET 8-or-newer consumers; legacy .NET Framework and `packages.config` support are not initial requirements. | No legacy consumer requirement has been supplied; native dependency packaging requires additional handling for those consumers. | Medium |

## Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep only Generator and Runtime packages | Documented current repository state. Confidence: High. | No | Avoids a release pipeline but leaves every consumer responsible for native toolchains and generation. | Rejected |
| Generate in each consumer build | The current generator can use local vcpkg headers. Confidence: High. | No | Adapts to a local OCCT install but makes restore/build environment-dependent and cannot define one reproducible package API. | Rejected |
| Generate once in a pinned release build and ship one managed package with RID-native assets | NuGet selects native assets from `runtimes/{rid}/native/`; the existing ABI already reserves one portable major-version boundary. Confidence: High for Windows packaging, Medium for future Linux behavior until proved. | Yes | Release builds become heavier and must own native dependency and license closure. | Selected |
| Publish one managed package plus separate native packages per RID | NuGet supports platform-specific packages and assets. Confidence: High. | Partially | Can reduce downloads later, but introduces package-version coordination before more than one supported RID exists. | Deferred until package size or release operations justify it. |

## Decision

### Public package and dependency direction

- The public consumer entry point is a new package and assembly named `TedToolkit.Occt`.
- Generated public API and P/Invoke code belong to that assembly. Shared ownership, exception, and
  transport mechanisms remain in `TedToolkit.Occt.Runtime`, referenced as a package dependency.
- `TedToolkit.Occt.Generator` and its analyzer remain release/development dependencies. They do not
  flow transitively to consumers and do not execute during consumer restore or build.
- The generated managed layer calls only the versioned ABI governed by ADR-0001; it does not bind
  directly to OCCT's C++ ABI.

### Meaning of complete header coverage

- The controlled release input enumerates every public `*.hxx` below the pinned OCCT include root.
- Each header and discovered declaration receives a stable disposition: generated, unsupported, or
  excluded by an explicit repository rule with a reason.
- A declaration or complete operation is generated only when its source type, C transport, C++
  adapter, managed transport, public projection, direction, nullability, and lifetime are all
  defined.
- STL and compiler implementation types, complex or non-contiguous containers, callbacks, and
  ownership-ambiguous operations are unsupported by default. The package does not create public
  wrapper types for them merely to increase coverage.
- One unsupported declaration does not invalidate unrelated supported declarations. It is omitted
  before symbol emission and appears in deterministic diagnostics and the release coverage report.
- “All headers are covered” means every header has a disposition; it does not mean every member is
  callable. README wording and package metadata must preserve that distinction.

### Reproducible release input

- A release pins the OCCT source/package version, vcpkg baseline and triplet, generator revision,
  ABI major, and generation rules that define its public surface.
- Generated public API, native binaries, and coverage evidence are outputs of the controlled
  release build. Consumers never regenerate them.
- A release fails when an input header is unaccounted for, generated API or ABI changes without an
  reviewed baseline change, a supported operation lacks an implementation, or a required native
  runtime dependency/license artifact is missing.

### Package and platform boundary

- One `TedToolkit.Occt` NuGet package carries the managed assembly and native runtime closure.
- Native libraries use NuGet's `runtimes/{rid}/native/` convention. The first support claim and
  required proof are `win-x64`; its assets include `ted_toolkit_occt_abi_v1` and every runtime OCCT
  dependency not otherwise guaranteed by the target system.
- The managed API and ABI remain RID-neutral. A later `linux-x64` asset uses the same ABI major and
  managed contract, but Linux is not advertised or treated as supported until equivalent package,
  ABI, ownership, error, and representative OCCT behavior proof passes on Linux.
- Running on a RID for which the package has no verified native asset must fail with a clear
  platform/library-load error. It must not silently load an arbitrary system OCCT installation.
- Initial consumer support is SDK-style .NET 8 or newer. Expanding to .NET Framework,
  `packages.config`, another architecture, or another RID requires separate compatibility proof.

## Why this decision now

Release-time generation creates one reviewable API and removes native toolchain requirements from
consumers. A complete header disposition satisfies the requested breadth without weakening the
explicit ABI and lifetime requirements already accepted in ADR-0001. RID-native packaging makes
Windows delivery usable now and leaves a standard location for later Linux assets without turning
portability intent into an unsupported claim.

Consumer-time generation is rejected because the current input is machine-dependent and would make
the same package reference produce different APIs. Generating wrappers for STL or ownership-unknown
surface is rejected because apparent coverage would create unsafe or unusable contracts. Separate
RID packages remain a valid later optimization, but one initial supported RID does not justify the
coordination cost.

Reconsider this decision if generated artifacts make the package operationally impractical, a
consumer must bind to a user-selected OCCT installation, NuGet RID assets cannot satisfy a required
deployment model, or complete Linux proof exposes an unavoidable platform-specific managed API.

## Evidence and links

- [Repository overview](../../README.md)
- [Generator contract and limitations](../../src/core/TedToolkit.Occt.Generator/README.md)
- [Runtime contract and limitations](../../src/core/TedToolkit.Occt.Runtime/README.md)
- [C interoperability ABI major 1](../interop-abi-v1.md)
- [NuGet native asset selection](https://learn.microsoft.com/nuget/create-packages/native-files-in-net-packages)
- [.NET Runtime Identifier catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)

The current workspace has no `VCPKG_ROOT`, so the number and exact disposition of OCCT 8.0.1
headers has not yet been measured. That inventory is delivery evidence, not assumed architecture
evidence.

## Consequences and accepted trade-offs

- Consumers receive one package and do not need the code-generation or native development stack.
- The release pipeline, rather than each consumer, owns reproducibility, native dependency closure,
  license notices, package size, and per-RID verification.
- API coverage grows only through explicit safe mappings. Unsupported surface remains visible in a
  report and may be added by later reviewed changes.
- Windows x64 can ship before Linux, but documentation must distinguish portable design from proved
  support.
- The public package creates a compatibility surface. Removing or changing generated public members
  after release requires the repository's compatibility policy and, when the C ABI changes, the
  major-version rules in ADR-0001.
- Dynamic native dependencies may make the initial package large. Static linking, trimming native
  modules, or splitting RID packages requires license and compatibility review before adoption.

## Downstream delivery constraints

- The release build accounts for every pinned public OCCT `.hxx` header and emits a deterministic
  disposition report.
- Only operations with complete cross-language and lifetime mappings enter generated public API.
- No unsupported type is replaced with `void*`, raw `IntPtr`, copied C++ spelling, or an invented
  ownership contract.
- The public package does not carry Generator/analyzer assets or execute generation for consumers.
- Package verification uses a clean consumer with no vcpkg or system OCCT dependency.
- A supported RID includes and loads the exact ABI-major library and complete native dependency
  closure produced by its release build.
- README and package metadata list the proved platform/OCCT/.NET matrix, explain full-header
  disposition, and document unsupported STL, complex-container, callback, and ownership-ambiguous
  cases.
- Linux remains an unverified target until its independent native and managed boundary evidence is
  complete.

## Exit requirements

Any replacement must preserve already released managed API and ABI consumers or define an explicit
major-version transition. Moving generation into consumer builds requires a reproducible input and
must not allow package references to produce incompatible APIs. Splitting native assets into
separate packages requires atomic version coordination and must preserve one ready-to-use consumer
entry point.

## Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Accept or reject this package and coverage architecture. | TedToolkit.Occt maintainers | Before drafting the dependent delivery change | Completed 2026-08-25 |
| Reassess managed target frameworks. | TedToolkit.Occt maintainers | A required consumer cannot use SDK-style .NET 8 or newer | Open |
| Reassess one-package RID distribution. | TedToolkit.Occt maintainers | A second RID is ready or native assets make package size/release operations unacceptable | Open |
| Add Linux to the supported matrix. | TedToolkit.Occt maintainers | Equivalent `linux-x64` package and boundary proof passes | Open |
| Expand unsupported type policy. | TedToolkit.Occt maintainers | A required OCCT operation needs STL, callbacks, complex collections, or a new lifetime model | Open |
