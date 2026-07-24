![Wikelo Contractor](docs/banner.png)

# Wikelo Contractor

Windows companion app for **Wikelo** trades in Star Citizen.

- **Catalog** — all Wikelo contracts (data from the [Star Citizen Wiki API](https://api.star-citizen.wiki/)),
  with a full-window preview for reward and item images; contracts that grant a crafting blueprint show it
  (detail page section + a "BP:" chip on the card)
- **Reputation** — mark contracts completed and track your Wikelo standing (New Customer → Very Good
  Customer → Very Best Customer) on a progress bar
- **Inventory** — track collected resources with an editable quantity box (type any value or step by one),
  grouped by category; each contract shows readiness (which required items you already have) and completing
  one deducts them *(in-game overlay coming next)*

## Stack

.NET 10 · WPF · [WPF-UI (lepoco)](https://github.com/lepoco/wpfui) · CommunityToolkit.Mvvm · Generic Host DI

## Getting started

```powershell
dotnet restore
dotnet run --project src/WikeloContractor.csproj
```

In VS Code: `Ctrl+Shift+B` — build, `F5` — run with debugger (VS Code will suggest the recommended extensions).

### VS Code profile (optional)

VS Code cannot disable extensions per workspace via config files. To work with a clean,
minimal extension set, import the bundled profile once: `Ctrl+Shift+P` →
**Profiles: Import Profile...** → select [WikeloContractor.code-profile](WikeloContractor.code-profile),
then switch to it in this workspace (`Ctrl+Shift+P` → **Profiles: Switch Profile**).
VS Code remembers the chosen profile per workspace.

## Tests

```powershell
dotnet test tests/WikeloContractor.Tests.csproj
```

## Releases & updates

Built with **Velopack**. Download the latest build from the
[Releases page](https://github.com/djwinston/wikeloContractor/releases):

- **`WikeloContractor-win-Portable.zip`** — unzip anywhere and run `WikeloContractor.exe`. No
  installation, nothing to elevate.

The build is framework-dependent — it uses the **.NET 10 Desktop Runtime**; install it from
[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) if the app reports it is
missing.

> **Why portable-only for now?** Installers (`Setup.exe` / `.msi`) and in-app auto-update return once
> the app is **code-signed**. An unsigned installer is a self-extracting executable that hardened
> Windows setups block outright, and auto-update would only ship more unsigned builds — so until
> signing lands, the portable zip is the whole story. Code signing via
> [SignPath Foundation](https://signpath.org/) is on the roadmap.

### Verifying a download

Every release carries a `SHA256SUMS.txt`; check a file with `Get-FileHash <file> -Algorithm SHA256`.
Release artifacts also ship a GitHub build-provenance attestation — proof they were built by this
repository's release workflow — which you can verify with the GitHub CLI:

```powershell
gh attestation verify WikeloContractor-win-Portable.zip --repo djwinston/wikeloContractor
```

CI runs on every PR to `dev`/`main` (`.github/workflows/ci.yml`). To cut a release, merge into
`main`, then push a SemVer tag — `.github/workflows/release.yml` builds and publishes the Release.
The `v` prefix is optional (SemVer doesn't require it); both `v1.2.3` and `1.2.3` trigger a release:

```powershell
git tag 1.2.3
git push origin 1.2.3
```

For a release PR into `main`, open it with the release template
(`?template=release.md` on the "compare" URL) to capture the intended version and post-merge steps.

## If Windows warns about the app

The app is **not yet code-signed** — a certificate is a recurring cost; free OSS signing via
[SignPath Foundation](https://signpath.org/) is on the roadmap once the project qualifies. So on
first run Windows SmartScreen shows *"Windows protected your PC"* — click **More info → Run anyway**.
That is a one-time prompt, not a security exception.

Before running it, confirm the download is genuine — see [Verifying a download](#verifying-a-download):
match the `SHA256SUMS.txt` hash and/or verify the build-provenance attestation.

On locked-down machines (**Smart App Control**, or restrictive **Attack Surface Reduction** policies)
Windows may block unsigned apps outright. We deliberately **don't** publish steps to add antivirus
exclusions or turn those protections off — that would mean weakening your security for our sake. If
your system enforces such policies, the right fix is a signed build: please wait for a code-signed
release rather than lowering your protections.

## Documentation

- [PLAN.md](PLAN.md) — development plan by phases
- [CLAUDE.md](CLAUDE.md) — project context for Claude Code
- [docs/data-pipeline.md](docs/data-pipeline.md) — catalog data: caching, enrichment, rate limiting
- [docs/ui-notes.md](docs/ui-notes.md) — UI patterns and WPF-UI quirks
- [docs/testing.md](docs/testing.md) — test layout and conventions
- [docs/api-item-fields.md](docs/api-item-fields.md) — field inventory of the item/vehicle API responses
- [docs/reward-images.md](docs/reward-images.md) — which reward items still need a manual image URL

## License, attribution & disclaimer

- The application source code is licensed under the [MIT License](LICENSE).
- Game data is provided by the [Star Citizen Wiki API](https://api.star-citizen.wiki)
  (community-maintained, unofficial). Per its terms of use, this credit is required for
  public projects, and commercial use of the data is not permitted.
- This is an unofficial fan-made application, not affiliated with or endorsed by
  Cloud Imperium Games or Roberts Space Industries. Star Citizen®, Roberts Space
  Industries® and Cloud Imperium® are registered trademarks of Cloud Imperium Rights LLC.
  All game data belongs to Cloud Imperium Games.
