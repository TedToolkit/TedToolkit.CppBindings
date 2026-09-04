# CPB-003: Authoritative repository identity and durable maintainer guidance

<!-- work-item-format: 2 -->
<!-- work-item-id: CPB-003 -->
<!-- approval-source: Maintainer explicitly approved the complete CPB-001 through CPB-003 map and item set with "批准。" in this task on 2026-09-04. -->

## Outcome

The verified platform has one consistent repository identity across maintained metadata, CI,
documentation, and consumer entry points, with recoverable GitHub cutover evidence.

<!-- work-item: scope -->
## Scope and non-goals

- Migrate maintained repository URLs, badges, source/package metadata, CI references, root orientation,
  architecture/principle terminology, and remaining active change references to the proven platform.
- Reconcile enduring documentation with CPB-002's exact implementation evidence, preserving historical
  ADR/benchmark evidence as history rather than rewriting it to imply a different original decision.
- Likely touchpoints: README, shared repository/package properties, `.github/workflows/build.yml`,
  current architecture/principles, ADR dispositions, and the separate active benchmark record.
- Non-goals: new behavior or public contracts, historical evidence renaming for cosmetic consistency,
  benchmark execution/optimization, package publication, and treating an external rename as development work.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| CPB-002 | Verified integrated project graph, renamed solution, public inventory, real OCCT binding gates, and loader/ADR findings | Authoritative integration revision and passing AC-02 / AC-05 evidence |
| CPB-001 | Verified package identities, analyzer direct-consumption instructions, and Runtime contracts | AC-03 / AC-04 evidence retained through the CPB-002 integrated revision |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-01 | Owns | Maintained identity migration plus complete parent operational-handoff evidence establishing the authoritative remote and recoverability |

<!-- work-item: delivery-constraints -->
## Constraints

- Follow the parent GitHub handoff ordering exactly. The coordinator/maintainer performs the external
  operation; this item's internal code/documentation candidate can be reviewed before that operation,
  but AC-01 and item completion remain pending until post-rename validation succeeds.
- Before default-branch integration, close the Pages/hosted actions/reusable workflow and integration
  inventory with explicit not-applicable findings or named owners and resolved dispositions. Unknown
  external consumers are not evidence of absence; stop if the cutover prerequisites cannot be met.
- Rename only from the exact reviewed, verified, normally integrated candidate, never the dirty
  working tree. No package publishing, credential extraction, permission changes, or unrelated edits.
- Old identity occurrences may remain only as explicitly reviewed historical/migration references;
  redirects are not authoritative configuration. Reconcile the active benchmark's current paths
  without relabeling historical measurements or adopting a performance strategy.
- Apply the architecture workflow if CPB-002 evidence requires a superseding enduring decision;
  do not silently rewrite an accepted ADR to conceal a semantic change.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=boundary shape=manual -->

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | New GitHub identity, origin, maintained references, history, default branch, and required settings are authoritative and operational | Complete the parent's ordered preflight, exact-candidate integration, rename, post-validation, and recovery handoff; capture authoritative URL, before/after repository identity and settings, `git remote -v`, and clean clone/build evidence |
| Maintained identity | Conditional | No current artifact presents the old name as authoritative; CI, source links, badges, package metadata, and instructions agree with the verified graph | Search tracked and intended new artifacts for `TedToolkit.Occt`, `TTOCCT`, and old repository URLs; review all remaining historical/migration matches and verify maintained destinations |
| Candidate regression | Conditional | Metadata/CI/path changes do not invalidate CPB-001/002 evidence | Run `dotnet build TedToolkit.CppBindings.slnx -c Release` and affected pipeline/consumer gates; rerun any proof whose inputs changed, then validate a clean clone from the authoritative renamed URL |

<!-- work-item: definition-of-done -->
## Done

AC-01 and conditional gates pass, durable documents describe the delivered platform without CGAL or
unsupported-platform claims, and no unresolved external handoff remains. Only then may final change
review/completion and retention-policy cleanup proceed; a locally renamed solution is not completion.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record exact reviewed/integrated commits, changed artifacts, AC-01 proof purpose/shape, actual
commands and checks, inventory dispositions and owners, before/after URL/settings evidence, clean
clone/build logs, resource prerequisites, current documentation state, and rollback availability.
Record any inaccessible setting as unresolved, not passed. Keep mutable progress/results outside
this stable contract and status only in the map.
