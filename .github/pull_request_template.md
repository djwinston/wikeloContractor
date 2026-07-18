<!--
Default template for PRs into `dev`.
For a release PR into `main`, open the PR with `?template=release.md` appended to the URL
(or via the "Preview" → template picker) to get the release checklist.
-->

## Summary

<!-- What does this change do, and why? -->

## Type of change

- [ ] Feature (`add`)
- [ ] Fix (`fix`)
- [ ] Change to existing behaviour (`update`)
- [ ] Refactor / internal (`refactor`)
- [ ] Docs / tests / chore

## Checklist

- [ ] `dotnet test tests/WikeloContractor.Tests.csproj` passes locally
- [ ] Smoke-ran the app (built exe launches and stays alive)
- [ ] New UI strings added to **both** `Strings.en.xaml` and `Strings.uk.xaml`
- [ ] Docs updated where relevant (`PLAN.md`, `docs/`, `CLAUDE.md`)
