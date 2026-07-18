<!--
Release PR into `main`. Open with `?template=release.md` in the PR URL.
Releases are cut by pushing a `vX.Y.Z` tag AFTER this PR merges — the tag drives the version,
so fill in the intended version below and push it once merged.
-->

## Release version

`vX.Y.Z`  <!-- e.g. v0.2.0 — this becomes the git tag that triggers release.yml -->

## Highlights / changelog

<!-- User-facing bullets that will inform the GitHub Release notes. -->

-

## Checklist

- [ ] All CI checks green (`build-and-test`)
- [ ] Version number above agreed and follows SemVer
- [ ] `PLAN.md` / `README.md` / `docs/` updated for anything user-facing in this release

## After merge

- [ ] Tag the merge commit and push it to trigger `release.yml`:
      `git tag vX.Y.Z && git push origin vX.Y.Z`
- [ ] Confirm the GitHub Release was created with the Velopack `Setup.exe` attached
