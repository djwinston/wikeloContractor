---
title: "Git Commit Task"
read_only: true
type: "command"
model: opus
---

# Create new git commit task

Inspect staged changes and produce a commit message. If nothing is staged, abort with: "No changes staged for commit. Aborting." Never run `git add`. Read only staged files. Match the *format/style* of recent commits (`git log -n 100 --oneline`).

## Prefix

- Prefix the commit summary with a base action prefix, not a ticket number or branch name:
  `<prefix>: <imperative summary>`, e.g. `add: disk cache for contract responses`, `fix: overlay hotkey not released on exit`.
- Allowed prefixes: `add`, `fix`, `update`, `refactor`, `remove`, `docs`, `test`, `chore`, `build`.
- Pick the prefix from what the staged change *does*, not from which files it touches:
  new capability → `add`; corrected behaviour → `fix`; existing behaviour extended/changed → `update`;
  internal restructuring with no behaviour change → `refactor`; deletion → `remove`;
  docs/comments only → `docs`; tests only → `test`; tooling/config/housekeeping → `chore`;
  csproj/packages/CI → `build`.
- If the staged changes mix several kinds, choose the prefix of the dominant change; if there is no clear dominant one, say so and suggest splitting the commit.
- Commit messages are in English (project language policy).

## Reply format

Reply MUST contain these 3 sections, in this order, never merged:

### 1. Message options
At least 5 numbered commit message options.

### High-level topics
A fenced ` ```markdown ` block of user-facing bullets ("what changed for the user").

### Technical description
A fenced ` ```markdown ` block of technical bullets capturing the *intent* of the change — the problem each edit solves or the behaviour it enables ("why this changed"), not a diff-derived enumeration of edited lines. Group bullets under bold sub-headings (e.g. `**API layer**`, `**Overlay**`).

Both description sections MUST be inside ` ```markdown ` fences so list/bold markers stay copy-pastable.

## Commit flow

- Wait for explicit approval of a message — never run `git commit` first.
- After approval, run `git commit -m "<message>"`. Do NOT add a Claude co-authorship footer.
- `git push` only if the user explicitly says you may push.
