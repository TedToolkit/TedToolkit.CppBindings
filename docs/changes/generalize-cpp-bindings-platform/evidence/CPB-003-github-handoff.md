# CPB-003 GitHub handoff evidence

- Evidence state: Complete
- Inventory captured: 2026-09-04 18:59 UTC
- Verified integration baseline: `e94f10a9bb9490d47363cf43d8ce17600b435b8a`
- Built identity candidate: `aeafbb3ab1a1dca1817dbc19c4fa6b94fd3f582e`
- Reviewed identity candidate: `ed90895eb2293ac9c0c10e34d492a32624e14595`
- Integrated cutover revision: `37aee103432c23fef97885d67895bb99c870363a`
- Previous repository: `TedToolkit/TedToolkit.Occt`
- Current authoritative repository: `TedToolkit/TedToolkit.CppBindings`
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

The gate regenerated the complete binding corpus and completed all 6,989 native build steps,
compiling 6,988 generated C++ translation units and linking `ted_toolkit_occt.dll`. The five produced
`TedToolkit.CppBindings*` NuGet packages each
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

## Post-rename validation

The reviewed candidate was fast-forwarded through `codex/cppbindings-integration` and pushed to the
default `development` branch at `37aee103432c23fef97885d67895bb99c870363a` before the rename. The
repository was then renamed through the GitHub REST API under the authorized administrator account.

| Boundary | After state |
| --- | --- |
| Repository identity | `TedToolkit/TedToolkit.CppBindings`; `https://github.com/TedToolkit/TedToolkit.CppBindings` |
| Default and other branch | `development` at `37aee103432c23fef97885d67895bb99c870363a`; `main` unchanged at `23c6e58e0c1400a99b0c170b96bb59d6832bbc0e` |
| Old identity | Old GitHub API URL returns HTTP 301 to repository identity `1151932989`; redirect is recovery-only |
| Local remote | Shared `origin` fetch and push URL is `https://github.com/TedToolkit/TedToolkit.CppBindings.git` |
| Pull requests and issues | PR 1, `🔖 Release`, remains open; no standalone issues |
| Workflow | `.github/workflows/build.yml` remains active |
| Administration | `Ted-Jin-Lab` remains the sole admin collaborator; authenticated repository permission remains admin |
| Pages and protection | `has_pages` remains false; Pages and `development` protection endpoints return 404 |
| Hooks, environments, rulesets | All remain empty |
| Actions policy | Enabled; all actions allowed; SHA pinning not required; default workflow permission remains write; workflows may approve pull-request reviews |
| Secrets, variables, deploy keys | All remain empty |
| Releases and tags | Both remain empty |

A fresh single-branch clone from the new authoritative URL checked out the exact integrated revision
and initialized `externals/TedToolkit` at `3ffa097c26349fdd60d1f86cc6d684e5808e2337` with a clean tracked
tree. The clone then completed the full post-rename gate:

```text
dotnet build TedToolkit.CppBindings.slnx -c Release
Build succeeded.
0 Warning(s)
0 Error(s)
Time Elapsed 00:47:27.37
```

The clean-clone gate restored dependencies, regenerated the complete corpus, and completed all 6,989
native build steps, compiling 6,988 generated C++ translation units and linking the DLL from the
renamed authoritative repository. AC-01 and CPB-003 are therefore verified on the authoritative
integration revision.
