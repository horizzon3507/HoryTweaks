# Upstream sync

HoryTweaks forks [D1GQ/BetterAmongUs](https://github.com/D1GQ/BetterAmongUs).
`.github/workflows/upstream-sync.yml` keeps the fork tracking it.

## What the workflow does

- Runs every Monday at 06:00 UTC and on demand via
  **Actions > Upstream sync > Run workflow**.
- Fetches `upstream` and counts commits on its default branch that `main`
  lacks (`git rev-list --count`). If there are none, the run exits quietly.
- Otherwise it creates `upstream-sync-<yyyymmdd>-<sha>` and tries
  `git merge upstream/<default> --no-edit`:
  - **Clean merge** — pushes the branch and opens a PR listing the upstream
    commits it merged.
  - **Conflicts** — aborts the merge and opens an issue titled
    "Upstream sync needs manual merge" with the conflicting file list and
    manual steps. A re-run comments on the existing open issue instead of
    filing duplicates.

## Manual merge

```bash
git remote add upstream https://github.com/D1GQ/BetterAmongUs.git   # once
git fetch upstream
git checkout -b upstream-merge-manual origin/main
git merge upstream/main        # resolve conflicts, then:
git add -A && git merge --continue
dotnet build src/BetterAmongUs.csproj -c Release -p:CopyToGame=false
git push -u origin upstream-merge-manual    # then open a PR
```

## Fork gotchas — files that commonly conflict

- **Renamed output**: our assembly is `HoryTweaks.dll`
  (`<AssemblyName>` in `src/BetterAmongUs.csproj`); upstream ships
  `BetterAmongUs.dll`. Keep our name — the `api/` feeds and installers point
  at it.
- **`api/` is fork-only**: update feeds and news entries consumed by the mod.
  Upstream has no `api/`; keep ours untouched when resolving.
- **Fork-only modules**: `src/Commands/`, `src/Modules/Moderation`,
  `src/Modules/Updater`, `src/Modules/Support`, the `installers/` tree and
  this `.github/workflows/` set have no upstream counterpart.
- **Heavily rewritten shared files**: `README.md`, `CHANGELOG.md` and the 16
  `src/Resources/Lang/*.json` catalogs diverge by design — expect conflicts;
  resolve in our favor unless upstream changed a real key set, then re-run
  `pwsh src/build/ValidateTranslations.ps1`.
- **Namespaces**: code stays `BetterAmongUs.*` even though the product is
  HoryTweaks — upstream C# merges cleanly most of the time; only watch for
  upstream renames that would collide with our extra partial classes.
