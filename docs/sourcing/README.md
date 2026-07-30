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
contract: "Retrieve Additional Smuggler Intel"
faction: "InterSec Defense Solutions"
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
- **`contract`** and **`faction`** are optional. They render as two labelled rows at the top of the
  **How to obtain** section, each hidden on its own when absent — so a bought or mined item shows
  neither, which is most of the corpus. **Omit the key rather than leaving it blank**: an empty value
  renders a label with nothing after it, and the data tests fail on it.
  - `contract` is the *candidate* mission name. Where two names are in circulation, give both
    separated by ` / ` rather than picking one.
  - `faction` is who hands the contract out, and only when a real organisation is named. It is blank
    far more often than `contract` — a mission name is usually recorded while its client is not.
  - Only these four keys are read. A typo is silently ignored at runtime, so the data tests reject any
    key outside `name` / `summary` / `contract` / `faction`.
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
| `![alt](https://…)` or `![alt](img/file.png)` | picture, on its own line |

Anything else renders as plain text rather than failing — a malformed guide can never break the page.
Inline markers do not nest: `**bold `code`**` renders the code run without the bold.

An image must sit **alone on its line** — `![…]()` inside a sentence stays literal text, since there
is nowhere sensible to put it in a stack of text blocks. A remote URL is downloaded once into the
disk cache; a relative path resolves against the install directory, so a picture committed under
`src/Resources/img/` ships with the release and works offline. Placing one between two numbered
steps is fine: it does **not** restart the numbering.

**Keep every bullet and numbered step on one physical line.** The parser trims each line before
classifying it, so a wrapped list item becomes a separate paragraph *and* resets the counter — the
step after it renders as "1." again. For the same reason there are no nested bullets: indentation is
trimmed away and sub-items flatten to top level. Use a `### Sub-heading` with a flat list when a step
needs alternatives. Ordinary paragraphs may wrap freely; consecutive lines join into one.

## Ground rules

- **Do not invent game facts.** An empty section is correct; a fabricated drop location is worse than
  no information at all. Cite the community sheet or your own testing.
- **Never name a shop or quote a price.** Both are live data that shift every patch, so a list baked
  into a guide is wrong almost immediately. Say the item is bought over the counter and let the reader
  look it up. Same for anything else that changes patch to patch — describe the *route*, not the
  current state of the universe.
- **Two canonical lookups**, used instead of a hardcoded list:
  [cstone.space shop finder](https://finder.cstone.space/) for where something is sold, and
  [SCMDB](https://scmdb.net/) for contract wording, requirements and rewards.
- Item names are game data and stay **English**, as does everything in this folder.
- Only `http`/`https` links are opened; anything else is rendered inert on purpose.

## Shared fragments

When several items come from one place, the shared text lives once in `_shared/` and each guide pulls
it in with a line of exactly:

```markdown
{{include: onyx-site-b}}
```

- A fragment is an ordinary `.md` file in `docs/sourcing/_shared/` whose front matter `name` is the
  include key — same rule as the guides, so renaming the file changes nothing.
- Fragments are **never** guides: the guide scan only reads the top folder, so a fragment cannot be
  attached to an item even though it declares a `name`.
- Expansion is **one level deep**. A marker inside a fragment is dropped rather than expanded, so
  fragments cannot reference each other into a loop.
- An unknown key renders as **nothing** — a reader is never shown raw markup. The data tests fail on
  the dangling reference instead, so a typo is caught at review time, not by a user.
- The marker only works alone on its line; `{{include: x}}` mid-sentence stays literal text.

The Onyx guides are the worked example: `onyx-site-b` carries the experiment mechanic the nine RCMBNT
samples share, and `onyx-sources` carries the two source links all twelve Onyx guides carry.

## Personal overrides

`%AppData%\WikeloContractor\sourcing\*.md` layers over these and wins per item, so local notes
survive app updates. Same format. `%AppData%\WikeloContractor\sourcing\_shared\*.md` does the same
for fragments, so a personal rewrite of a shared block reaches every guide that includes it.
