# Wikelo Contractor

Windows companion app for **Wikelo** trades in Star Citizen.

- **Catalog** — all Wikelo contracts (data from the [Star Citizen Wiki API](https://api.star-citizen.wiki/))
- **Inventory** — tracking of collected resources with per-contract progress and an in-game overlay

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

## Documentation

- [PLAN.md](PLAN.md) — development plan by phases
- [CLAUDE.md](CLAUDE.md) — project context for Claude Code
