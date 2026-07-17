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
| Catalog service | `Services/ContractCatalogServiceTests.cs` | fake `IStarCitizenWikiClient` + temp cache dir |
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

## What must get a test

- Any new parsing of API payloads (DTO or JsonDocument-based).
- Any new branch in cache/invalidation/rate-limit logic in `ContractCatalogService`.
- Pure model/helper logic (labels, category derivation, formatting).
- UI/ViewModels are currently not unit-tested (WPF dispatcher dependency) — verified by the
  smoke run instead; keep VM logic thin or push it into services so it stays testable.
