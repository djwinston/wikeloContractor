<!--
Release PR into `main`. Open with `?template=release.md` in the PR URL.
Releases are cut by pushing a `vX.Y.Z` tag AFTER this PR merges — the tag drives the version,
so fill in the intended version below and push it once merged.
-->

## Release version

`vX.Y.Z`  <!-- e.g. v0.2.0 or 0.2.0 — this becomes the git tag that triggers release.yml.
              The `v` prefix is optional; both `vX.Y.Z` and `X.Y.Z` work. -->

## Highlights / changelog

<!-- Author-facing summary for reviewers. The published GitHub Release notes are NOT built from
     this — release.yml generates them from the commit subjects and bodies in the tag range, so
     write those bodies as user-facing text (they ship verbatim). -->

-

## Checklist

- [ ] All CI checks green (`build-and-test`)
- [ ] Version number above agreed and follows SemVer
- [ ] `PLAN.md` / `README.md` / `docs/` updated for anything user-facing in this release

## After merge

- [ ] Tag the merge commit and push it to trigger `release.yml`:
      `git tag vX.Y.Z && git push origin vX.Y.Z`
- [ ] Confirm the GitHub Release has `WikeloContractor-win-Portable.zip`, `SHA256SUMS.txt` and a
      build-provenance attestation, and that the unsigned `Setup.exe`/`.msi` were unpublished
      (portable-only until code signing)
