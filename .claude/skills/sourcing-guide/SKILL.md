---
name: sourcing-guide
description: Turn a player's own knowledge of how an item is obtained into the repo's sourcing guides under docs/sourcing/. Use whenever a "Where to Find" / "How to obtain" guide has to be written, extended or corrected for one or more required items.
---

# Writing a sourcing guide

Invoked as `/sourcing-guide <Item Name>`, optionally followed by a description of how the item is
obtained. The user is the source of truth — this workflow **structures what they know**, it does not
research the answer on its own.

Read `docs/sourcing/README.md` first; it is the format contract these files must satisfy.

## Why the user, not the web

Measured while designing this skill — do not redo this research, it came out empty:

- `api.star-citizen.wiki` `/api/commodities` carries `methods` / `systems` / `locations[]`, but of the
  95 required items only **13** match a commodity and only **1** (Bluemoon Fungus) has locations
  filled in. The `shops` include is deprecated upstream ("shop data is not available in the source
  files anymore"), so the API cannot say where anything is sold.
- `starcitizen.tools` has an `== Acquisition ==` section per item, reachable through `api.php`
  (the HTML site answers 403). Coverage is uneven: useful for Vanduul Metal, empty for Yormandi Eye.
- Community Wikelo trackers (`seeknd/Wikelo` and friends) ship contract/recipe data only — their
  `sources` / `notes` / `ingredients_info` fields are empty in every record.
- Spectrum is a client-rendered SPA: a plain fetch returns the thread title and nothing else, and its
  internal search API rejects every documented `type` value while throttling after ~4 requests.

Mission chains, experiment mechanics and alternate entrances exist in **no** dataset. Ask the user.

## 1. Resolve the item and the fan-out set

The front matter `name` is the lookup key and must match the required item's name in a contract
*exactly*; the file name is cosmetic. Confirm the item already has a file:

```powershell
Select-String -Path docs\sourcing\*.md -Pattern '^name:' | Select-String '<item>'
```

If nothing matches, stop and ask — a guessed name silently never resolves at runtime.

Then find every **other** item the same source covers. One facility usually feeds many items, and
writing them together is the whole point: the Onyx Facility chain filled 12 files at once
(`asd-secure-drive`, `yormandi-eye`, `yormandi-tongue`, and the nine `rcmbnt-*`). Grep the existing
`summary:` lines for the location or activity name to find the siblings.

## 2. Ask before writing

Ask **only about genuine ambiguities** in what the user said — contradictions, missing counts,
unclear ordering, inconsistent place names. Do not re-ask what they already stated plainly, and do
not ask for things a careful reader would infer (if beams are described as numbered 1/2/3, the beam
number *is* the experiment number).

Batch the questions into one numbered list and wait for answers before writing any file.

## 3. Write the files

Every file stands alone — the app renders one item's page at a time — but text the siblings share
does **not** get copied into each of them. Put it once in `docs/sourcing/_shared/<key>.md` (front
matter `name: <key>`, no other required fields) and pull it into each guide with a line of exactly
`{{include: <key>}}`. Only the distinguishing part stays in the guide itself.

The Onyx set is the worked example: `onyx-site-b` holds the experiment mechanic the nine RCMBNT
samples share, `onyx-sources` holds the source links all twelve carry, and each guide keeps only its
own steps. When siblings differ by a regular pattern (series × number), generate them from one
template in a throwaway script rather than editing nine files by hand.

Suggested shape, per `docs/sourcing/README.md`:

```markdown
## Where to find it     — what it is, which activity, what a run yields
## Step by step         — numbered, in the order the player performs them
## Notes                — cross-references to sibling items, then source links
```

Front matter carries two optional fields on top of `name` / `summary`, rendered as labelled rows at
the top of the **How to obtain** section and hidden individually when absent:

- `contract:` — the **candidate** mission name. Two names in circulation? Give both, ` / ` separated.
- `faction:` — the organisation handing it out, only when one is actually named.

**Omit the key rather than leaving it blank**, and set neither for anything bought or mined — which is
most of the corpus. Those are the only four keys read; a typo renders nothing, so the data tests
reject any other key.

## 4. Format rules — these are hard

`Models/MarkdownDocument.Parse` trims every line before classifying it, so layout that looks fine in
a normal Markdown editor renders wrong here:

| Rule | Why |
|---|---|
| A bullet or numbered step must be **one physical line** | A wrapped line becomes a separate paragraph *and* resets the counter, so the next step renders as "1." |
| **No nested bullets** | Indentation is trimmed away; sub-items flatten to top level |
| Paragraphs *may* wrap | Consecutive non-blank lines join as one paragraph |
| Only `##` / `###`, `-` / `*`, `1.`, `**bold**`, `*italic*`, `` `code` ``, `[link](https://…)` | Everything else degrades to plain text |
| `![alt](url)` must sit **alone on its line** | Inside a sentence it stays literal text — there is nowhere to put it in a text stack |
| Only `http`/`https` links are live | Other schemes render inert by design |
| `{{include: key}}` must sit **alone on its line** | Mid-sentence it stays literal text; expansion is one level, so fragments cannot include each other |

An image reference is either a remote `https` URL (downloaded once into the disk cache) or a path
relative to the install directory, e.g. `img/onyx-site-b.png` for a picture committed under
`src/Resources/img/`. Videos and playlists are **not** images — they stay plain links.

Use `### Sub-heading` + a flat bullet list when a step needs alternatives (e.g. two ways into a
section), instead of nesting under the numbered step.

`<!-- comments -->` are stripped before rendering — the right place to park an authoring note.

## 5. Never bake in live data

**Do not name shops and do not quote prices.** They change every patch, so a terminal list or a price
captured today is misinformation next month — and the app already has live data for that. Write that
the item is a shop purchase and hand the reader a lookup instead:

- `{{include: shop-purchase}}` — the shared fragment, which links the
  [cstone.space shop finder](https://finder.cstone.space/).
- `{{include: contract-lookup}}` — links [SCMDB](https://scmdb.net/) for contract wording,
  requirements and rewards. Include it **directly in the guide**: nesting it inside another fragment
  would be silently dropped, since expansion is one level deep.

The same reasoning covers anything else that shifts patch to patch. Describe the route, not the
current state of the universe. Named *locations* (an outpost, a station, a mission) are fine — those
are geography, not stock.

## 6. Never invent a game fact

`docs/sourcing/README.md` states it and it is the rule that matters most here: a fabricated drop
location is worse than no information. An empty section is a correct outcome. Anything the user did
not confirm either goes back to them as a question or stays in an HTML comment — never into prose
that a player will read as fact.

## 7. Verify

Lint the written files against the parser rules, then run the tests:

```powershell
dotnet test tests\WikeloContractor.Tests.csproj --filter "FullyQualifiedName~Sourcing|FullyQualifiedName~Markdown"
```

`tests/Resources/SourcingGuideDataTests.cs` validates the shipped data; `MarkdownDocumentTests`
covers the parser. A quick self-check for wrapped list items: read each file back and confirm no
paragraph directly follows a list item, and that numbered steps run 1..n without restarting.

## 8. Ask about media last

Close every run by asking whether there are **additional media or links** — a video walkthrough, a
playlist, map slides, a community guide post. Map slides and screenshots can go in as `![alt](url)`
images next to the step they illustrate; videos, playlists and guide posts stay plain links in
`## Notes`, one per line:

```markdown
- Community walkthrough with mission maps: [Onyx Companion Guide (4.3)](https://…).
- Video walkthrough playlist: [Onyx Facility on YouTube](https://…).
```

## Known project facts

- Worked examples of a finished guide: `docs/sourcing/carinite.md` (simple) and the twelve Onyx
  files (a chain, a boss encounter, and a 3 × 3 variant matrix sharing one mechanic).
- Personal overrides live in `%AppData%\WikeloContractor\sourcing\*.md` and win per item, so a user
  can keep private notes that survive updates. Same format.
- Item names are game data and stay **English**, as does everything in `docs/sourcing/`.
