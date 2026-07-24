# Sourcing knowledge base

One Markdown file per item Wikelo asks for. The app reads these to fill the **Where to Find** page:
the front matter's `summary` becomes the card's one-liner, the body becomes the **How to obtain**
guide on the item's detail page.

These files ship **inside the release** (copied to `Resources/sourcing/` next to the exe), so the app
needs no network to show them. Edit them here and open a pull request — the change reaches users with
the next release.

## Format

```markdown
---
name: "Carinite"
summary: "Align & Mine ore."
---

## Where to find it

Prose, **bold**, *italic*, `code`, [links](https://example.com).

## Step by step

1. First step.
2. Second step.
```

- **`name` is the key** and must match the item's name in a contract's required items *exactly*. The
  file name is cosmetic — renaming a file never breaks the lookup, changing `name` does.
- **`summary`** is one plain sentence. Leave it `''` if unknown; the app shows its own placeholder.
- **Body** is optional. A file with no body (only comments) correctly shows the "not written yet"
  placeholder.
- `<!-- comments -->` are stripped before rendering, so authoring hints are safe to leave in place.

## Supported Markdown

A deliberately small subset — see `src/Models/MarkdownDocument.cs`:

| Syntax | Result |
|---|---|
| `## Heading`, `### Sub-heading` | section headings |
| `- item` or `* item` | bullet |
| `1. item` | numbered step (renumbered automatically) |
| `**bold**`, `*italic*`, `` `code` `` | inline styling |
| `[text](https://…)` | link, opens in the browser |

Anything else renders as plain text rather than failing — a malformed guide can never break the page.
Inline markers do not nest: `**bold `code`**` renders the code run without the bold.

## Ground rules

- **Do not invent game facts.** An empty section is correct; a fabricated drop location is worse than
  no information at all. Cite the community sheet or your own testing.
- Item names are game data and stay **English**, as does everything in this folder.
- Only `http`/`https` links are opened; anything else is rendered inert on purpose.

## Personal overrides

`%AppData%\WikeloContractor\sourcing\*.md` layers over these and wins per item, so local notes
survive app updates. Same format.
