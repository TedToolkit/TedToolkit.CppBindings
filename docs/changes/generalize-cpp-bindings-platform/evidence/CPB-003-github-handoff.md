# CPB-003 GitHub handoff evidence

- Evidence state: Pre-rename inventory; rename and post-rename validation pending
- Inventory captured: 2026-09-04 18:59 UTC
- Verified integration baseline: `e94f10a9bb9490d47363cf43d8ce17600b435b8a`
- Internal identity candidate: `aeafbb3`
- Current authoritative repository: `TedToolkit/TedToolkit.Occt`
- Target repository: `TedToolkit/TedToolkit.CppBindings`
- Responsible maintainer: `Ted-Jin-Lab`

## Internal candidate

The candidate updates maintained repository/package metadata, root orientation and commands,
current architecture and principles, and contributor test instructions to the delivered
`TedToolkit.CppBindings` graph. ADR-003 records the verified `NativeLibrary` plus immutable native
function-table bootstrap and supersedes ADR-001 without rewriting its historical measurements or
compatibility evidence.

The tracked identity scan excludes only ADR-001 evidence, its benchmark solution, and the active
migration contract. The remaining old-identity occurrences are reviewed history or migration
before/after statements; no maintained current code, project, package, workflow, or instruction
uses the old identity as authoritative.

## Exact-candidate verification

Candidate `aeafbb3` completed the full Release solution gate on 2026-09-04 UTC:

```text
dotnet build TedToolkit.CppBindings.slnx -c Release --no-restore
Build succeeded.
0 Warning(s)
0 Error(s)
Time Elapsed 00:46:46.56
```

The gate regenerated the complete binding corpus and compiled and linked all 6,989 generated C++
units into `ted_toolkit_occt.dll`. The five produced `TedToolkit.CppBindings*` NuGet packages each
contain `https://github.com/TedToolkit/TedToolkit.CppBindings` as `projectUrl`. The documentation-only
review corrections recorded after `aeafbb3` do not change the built source or package inputs.

## Pre-rename GitHub inventory

Read-only GitHub REST queries used the configured Git credential without printing or persisting it.
The public Packages result was verified separately through the public organization package listing.

| Boundary | Before state | Disposition |
| --- | --- | --- |
| Administration | Authenticated repository permission reports `admin: true`; sole collaborator `Ted-Jin-Lab` has the admin role | Authorized owner performs and validates the rename |
| Target name | `GET /repos/TedToolkit/TedToolkit.CppBindings` returned 404 | Target name available at inventory time; recheck immediately before rename |
| Default branch | `development`; remote SHA `19cee2f94741f988a2b408b5d7e3acf0e16b925e` | Preserve and verify after rename |
| Other remote branch | `main`; remote SHA `23c6e58e0c1400a99b0c170b96bb59d6832bbc0e` | Preserve and verify after rename |
| Pull requests | Open PR 1, `🔖 Release` | Preserve and verify after rename |
| Issues | No standalone issues | Not applicable; verify count after rename |
| Tags and releases | None | Not applicable |
| GitHub Pages | Repository reports `has_pages: false`; Pages endpoint returned 404 | Not applicable |
| Repository-hosted actions | No `action.yml` or `action.yaml` is tracked | Not applicable; GitHub's action-rename exception does not apply |
| Reusable workflows | The sole active workflow is `.github/workflows/build.yml`; it has no `workflow_call` trigger and calls the pinned shared `TedToolkit/TedToolkit` workflow | No consumer cutover required; verify workflow remains active |
| Maintained external code references | Authenticated organization code searches for the old repository URL and `uses: TedToolkit/TedToolkit.Occt` returned zero results | No coordinated consumer update required |
| Webhooks | Empty repository hook inventory | Not applicable |
| Environments | Empty environment inventory | Not applicable |
| Branch protection and rulesets | No `development` protection; empty repository ruleset inventory | Preserve absence and verify after rename |
| Actions policy | Enabled, all actions allowed, SHA pinning not required; default workflow permission is write and workflows may approve pull-request reviews | Preserve and verify after rename |
| Actions secrets and variables | Both inventories empty | Not applicable |
| Deploy keys | Empty | Not applicable |
| GitHub App integrations | Organization installation inventory empty | Not applicable |
| Published packages | Public organization listing filtered to `TedToolkit.Occt` reports zero packages | No package link requires migration; publication remains out of scope |

GitHub's maintained rename guidance states that repository traffic and Git operations redirect, but
project-site URLs do not and actions hosted by a renamed repository are not redirected. The
inventory above proves that neither exception applies. Redirects remain recovery aids only; every
maintained local and metadata URL must use the new repository after cutover.

## Required cutover and post-validation

1. Finish the exact-candidate build and independent review.
2. Integrate the candidate through the `development` default-branch history and push the exact
   resulting revision while the old repository name is authoritative.
3. Recheck target-name availability, then rename the repository through the GitHub API.
4. Change every maintained local worktree's `origin` to the new URL.
5. Verify repository name/URL, default branch and both remote heads, history, PR 1, permissions,
   settings inventories, workflow visibility, and old-URL redirect.
6. Clone from the new authoritative URL into a fresh short path and run the required clean
   Release build/gates.

If the rename or post-validation fails, rename back while the old name remains available, restore
maintained remotes and metadata, and revert the identity candidate through normal Git history.
