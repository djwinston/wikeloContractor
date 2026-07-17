# Verify the working tree

Full verification pass: tests → app smoke run → docs review. Run the steps **in order**;
do not skip a step because an earlier one "looks fine".

## 1. Run the tests

```powershell
dotnet test tests/WikeloContractor.Tests.csproj
```

- If tests fail, decide per failure which side is wrong:
  - the production code has a bug → fix the code;
  - behavior changed intentionally → update the test (and its fixtures) to the new contract.
- Re-run until green. Never delete a failing test just to pass; never weaken an assertion
  without stating why.

## 2. Smoke-run the application

Build and launch the built exe, wait ~8 seconds, confirm the process is still alive,
then stop it:

```powershell
dotnet build src/WikeloContractor.csproj
$p = Start-Process -FilePath "src\bin\Debug\net10.0-windows\WikeloContractor.exe" -PassThru
Start-Sleep -Seconds 8
if ($p.HasExited) { "App EXITED with code $($p.ExitCode)" } else { "App is running"; Stop-Process -Id $p.Id -Confirm:$false }
```

- Build warnings are not acceptable — investigate and fix (transient MSB3026/MSB3027 usually
  mean a stale app process is holding the exe; stop it and rebuild).
- If the app exits early, diagnose (usually a XAML resource/binding error), fix, repeat from step 1.

## 3. Review changes against the documentation

Analyze the session's changes (`git status`, `git diff`, staged and unstaged) and compare
them with what the docs claim:

- `CLAUDE.md` — conventions, commands, architecture notes
- `PLAN.md` — phase checklists (tick completed items, add newly agreed work)
- `README.md` — user-facing description, getting started
- `docs/data-pipeline.md` — caching / enrichment / rate limiting
- `docs/ui-notes.md` — UI patterns and WPF-UI quirks
- `docs/testing.md` — test conventions
- `.claude/skills/api-explore/SKILL.md` — API endpoint facts

If any file is now stale or missing something the changes introduced, list the concrete
updates you propose and **ask the user** which to apply (this is the one step where asking
is expected). If everything is in sync, say so explicitly.

## 4. Report

Summarize: test results (count), smoke-run outcome, and the documentation verdict.
