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

Distributed as a **Velopack** installer with in-app auto-update. The build is
framework-dependent; the installer downloads the .NET 10 Desktop Runtime on first install if it's
missing. Once installed, **Settings → Check for app updates** pulls new versions from GitHub
Releases (and the app checks in the background on launch).

CI runs on every PR to `dev`/`main` (`.github/workflows/ci.yml`). To cut a release, merge into
`main`, then push a SemVer tag — `.github/workflows/release.yml` builds and publishes the Release:

```powershell
git tag v1.2.3
git push origin v1.2.3
```

For a release PR into `main`, open it with the release template
(`?template=release.md` on the "compare" URL) to capture the intended version and post-merge steps.

## If Windows blocks the app from running

The app is **not code-signed** — a code-signing certificate is a paid, recurring cost, and
Microsoft's own service (Azure Artifact Signing) is not available to solo developers outside the
US/Canada. On most machines the app runs after the normal SmartScreen prompt (**More info → Run
anyway**).

On hardened setups Windows may **hard-block** it instead — typically **Smart App Control** or the
Attack Surface Reduction rule *"Block executable files from running unless they meet a prevalence,
age, or trusted list criterion."* Note that unblocking the file (`Unblock-File`, or the *Unblock*
checkbox in file properties) only clears the download warning — it does **not** lift these blocks.

If an **ASR rule** is blocking it, allow the app folder from an **elevated** PowerShell:

```powershell
# Allow the app folder (adjust the path to your install / extract location).
# Installed build: %LocalAppData%\WikeloContractor ; portable: the folder you unzipped.
Add-MpPreference -AttackSurfaceReductionOnlyExclusions "$env:LOCALAPPDATA\WikeloContractor"

# See what is currently excluded:
Get-MpPreference | Select-Object -ExpandProperty AttackSurfaceReductionOnlyExclusions
```

**Smart App Control** has no per-app allowance — it can only be turned off entirely (Settings →
Privacy & security → Windows Security → App & browser control), which is a machine-wide change that
cannot be re-enabled without reinstalling Windows. Do this only if you understand the trade-off.

The proper fix is code signing; it is on the roadmap if the project obtains a certificate.

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
