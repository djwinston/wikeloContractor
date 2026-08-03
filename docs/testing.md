# Testing

Read this before adding or changing tests in `tests/`.

- Framework: **xUnit**, project `tests/WikeloContractor.Tests.csproj`
  (references `src/WikeloContractor.csproj`; internals are visible via `InternalsVisibleTo`).
- Run: `dotnet test tests/WikeloContractor.Tests.csproj` (VS Code: task **test**).
- Workflow: after code changes, verify the app builds and launches (smoke run). Tests are run
  by the `/verify` command, not after every edit — see `.claude/commands/verify.md`.

## Layout & conventions

| Area | File | Technique |
|---|---|---|
| Model logic | `Models/ContractRequirementTests.cs` | plain asserts, `[Theory]` for value tables |
| API client | `Services/StarCitizenWikiClientTests.cs` | stub `HttpMessageHandler` returning fixture JSON |
| Catalog service | `Services/ContractCatalogServiceTests.cs` | `E2E/ScriptedWikiApi` + temp cache dir |
| Sync / availability | `E2E/*Scenarios.cs` | real services + ViewModels on a real WPF `Application` |
| JSON store | `Services/InventoryStoreTests.cs` | internal file-path ctor pointed at a temp file (per-test guid dir, deleted in `Dispose`) — same seam as `CatalogImageOverrideServiceTests` |
| Debounced writes | `Services/InventoryStoreTests.cs` | second internal ctor injecting the quiet period and hard-flush interval; poll the file for the expected content rather than sleeping a fixed time |
| Localization | `Localization/LocalizationParityTests.cs` | XAML dictionaries parsed as XML |

- **No mocking library** — hand-written fakes/stubs are enough at this scale; keep it that way
  unless they become painful.
- Client tests feed **realistic API-shaped JSON** (snake_case, envelope `{ "data": ... }`).
  When the API shape changes, update fixtures from live samples (see the api-explore skill).
- Service tests redirect the cache via the internal `ContractCatalogService(client, cacheDirectory)`
  constructor to a per-test temp directory (`Path.GetTempPath()/WikeloContractorTests/<guid>`),
  deleted in `Dispose`. Never let tests touch the real `%AppData%` cache.
- Background enrichment is awaited via a `TaskCompletionSource` on `CatalogUpdated`
  (+ `WaitAsync` timeout) — never `Task.Delay`-and-hope.
- The localization parity tests are the guard for the "add keys to BOTH dictionaries" rule:
  key sets, non-empty values and `{0}` placeholder parity. A new key in only one dictionary
  fails the build-gate, which is exactly the point.

## Synthetic E2E (`tests/E2E/`)

Scenario tests that drive the **real** service and ViewModel graph through a scripted API and
assert the state a user would see. They exist because the states that broke in production —
a sync nobody could see, a category filter matching nothing, a completion that silently deducted
the wrong inventory — are all cross-layer and none of them is reachable from a unit test.

Deliberately **not** a UI-automation framework (FlaUI/Appium): the assertions are about ViewModel
state, which is what the XAML binds to; rendering stays covered by the manual smoke run.

- `E2E/ScriptedWikiApi` — the one `IStarCitizenWikiClient` fake, shared with the service tests.
  Scripts a version bump (`BumpVersion`), an enrichment held open so assertions run mid-flight
  (`HoldEnrichment`/`ReleaseEnrichment` — the version and mission-list calls are exempt, holding
  those would stop the load from ever reaching enrichment), `ThrowRateLimitedOnCall` /
  `AlwaysRateLimited`, and `GoOffline`. **Never add a second fake for this interface.**
- `E2E/WpfAppFixture` — one real `Application` on an STA thread. `Application.Current` is a
  per-process singleton, hence `[Collection("WpfApp")]` with parallelization disabled. Only
  `Strings.en.xaml` is merged: `Localized` is the sole resource lookup ViewModels do.
- `E2E/CatalogHarness` — one app instance over a temp directory (`CompletionService`,
  `FavoritesService`, `InventoryStore`, `PinnedItemsService` and `SettingsService` all have file-path
  test seams). Pass another harness's `Root` to model an
  **app restart** over the same cache, and call `AgeCache()` to backdate the last version check —
  without it the service correctly serves the cache untouched for 12 h and never reaches the API.
- `E2E/OverlayDoubles` — `FakeHotkeyService` (the one `IHotkeyService` fake — **never add a second**)
  and `FakeOverlayWindow`. `Press(action, slot)` raises the same event the real service raises from
  `WM_HOTKEY`.

### Overlay scenarios

**The seam is `IHotkeyService.Pressed`, the event — not the Win32 message.** Two reasons, both
disqualifying for the alternative: claiming global combinations from a test would steal
`Ctrl+Alt+O` from whoever is at the keyboard, and whether the OS grants one depends on what else is
running, so `Assert(RegisterHotKey succeeded)` is flaky everywhere but the author's desk.

Decoding is covered below that seam, one file per backend, and neither asks the OS for anything:

- `E2E/HotkeyServiceTests` — `WM_HOTKEY` → action and slot, by calling the hook directly on the STA
  fixture. Every test pins `HotkeyBackendKind.RegisterHotKey`, both because that is the backend which
  decodes that message and so the suite does not depend on what `Auto` resolves to on the machine.
- `Services/RawInputBackendTests` — the Raw Input decode, with **no window at all**. The OS half (the
  subscription, and a `WM_INPUT` payload only Windows can produce) is left out; what is driven is
  `RawInputBackend.OnKeyDown` plus an injected modifier reader, which is the whole decision.
- `Models/HotkeyLookupTests` — the matching rule itself, pure.

The one machine-dependent thing asserted anywhere is that `Auto` resolves to *some* backend and says
which — that log line is the entire diagnostic when hotkeys go quiet in game, so it has to hold.

That seam is also why `OverlayViewModel` holds no `Window` and `OverlayService` reaches the window
through `IOverlayWindow`: keeping the window out of the decision layer is what makes the whole
feature — hotkey to store to catalog readiness chip — assertable without rendering anything.

Two traps worth knowing before writing a new scenario:

- **Never block the UI thread** on a task that awaits app work. Background enrichment marshals
  into that same dispatcher, so `OnUiAsync(() => something.Wait())` deadlocks. Start async work
  with `OnUiAsync(Func<Task>)` and await it from the test thread.
- **Never invoke the completion command.** `ContractCompletionInteraction` shows a
  `Wpf.Ui.Controls.MessageBox`, and with no `MainWindow` it hangs. Assert the *gate*
  (`ToggleCompletedCommand.CanExecute(null)`), which needs no dialog.

Write scenarios red first: they must fail against the unfixed code, and fail for the stated
reason. A scenario that cannot fail (e.g. asserting a block lifts without first asserting it went
up) is worse than none.

## What must get a test

- Any new parsing of API payloads (DTO or JsonDocument-based).
- Any new branch in cache/invalidation/rate-limit logic in `ContractCatalogService`.
- Any new user-visible state that depends on sync/offline/rate-limit — as an `E2E/` scenario,
  not a VM unit test.
- Pure model/helper logic (labels, category derivation, formatting).
- UI/ViewModels are currently not unit-tested (WPF dispatcher dependency) — verified by the
  smoke run instead; keep VM logic thin or push it into services so it stays testable.
