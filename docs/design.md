# Story engine — target design

**Status:** target design (describes the intended architecture; not all of it is built yet).
The current, smaller implementation is described in `architecture.md`; this document is the
north star for where the project is going.

The product is, in the end, an AI **writer** that produces a coherent multi-chapter story plus an
AI **editor** that fixes it, backed by a **queryable knowledge base** and a set of read-only
**tools**. This document is the single reference for that direction so work can continue across
sessions and by different developers.

## 1. Principles

- **Never send whole previous chapters.** Context is bounded chapter over chapter.
- **Store knowledge addressable.** Every fact/snippet has an id, a kind, a title and tags so it
  can be *listed, fetched and searched*.
- **Keep the always-sent part tiny.** Frame + lore + current state + chapter brief.
- **Typed where semantics matter, free-form where the author writes prose.** The frame, cast and
  current state are typed; the knowledge base holds free-form prose with light metadata.
- **Structured JSON for anything the app must parse.** Returned state/logline/etc. are validated
  against a schema; invalid output is retried or surfaced, never parsed leniently.
- **Every AI step is a separate, testable pass.** Planner / writer / editor / summarizer are
  independent units.

## 2. Data model (target)

```text
Project
  SchemaVersion, Id, Name, CreatedUtc, UpdatedUtc
  Settings:   StorySettings { OriginalLanguage, TargetLanguages[] }
  Frame:      StoryFrame    { Genre, Tone, Style, PointOfView, Tense, Rating, Premise, Direction }
  Lore:       WorldLore     { Title, Body, Tags[] }
  Characters: Character[]
  Knowledge:  KnowledgeEntry[]
  WorldState: WorldState    { TimeAndPlace, Description }      // rolling situation
  Chapters:   Chapter[]

Character {
  Id, Name, Role, Age, Description, Personality, Background, Goals, Traits[]
}

KnowledgeEntry {
  Id, Kind, Title, Tags[], Content
}

KnowledgeKind = Note | Place | Item | Event | Faction | Rule | Background

Chapter {
  Number, Title, Direction, Notes,
  ContentOriginal,
  Translations{ languageCode -> text },
  Logline,
  WorldState?,                       // snapshot of the situation AFTER this chapter
  Status: Draft | Generated | Edited,
  CreatedUtc
}
```

Notes on the model:

- `Frame` gains `Style`, `PointOfView`, `Tense`, `Rating` (today only genre/tone/premise/direction
  exist). These are always-sent constraints.
- `ExtraFile` is **removed**: files, items, events, places, factions, rules and free notes all
  become `KnowledgeEntry` records whose `Kind` differs. This is what makes them uniformly
  queryable.
- Per-chapter character selection (the old `ChapterCharacter` / `CharacterPresence` and the
  checkboxes) is **removed**: the writer decides which characters to pull via tools.
- `WorldState` stays a small rolling value (time and place + free-form description) and is
  **not** merged into the knowledge base — it changes every chapter and must stay cheap.

## 3. Setup sequence

Project setup is how the author builds the story "bible". It should read like a guided sequence,
because each step constrains the next:

1. **Frame** — book name, genre, tone, style/POV/tense, rating, premise, direction.
   *Sets the voice and the hard constraints for everything else.*
2. **World (lore)** — the static background: title + body (+ tags).
   *The world the story lives in; referenced everywhere but never changes.*
3. **Cast** — character profiles.
   *Who exists; needed before plot/state can reference them.*
4. **Knowledge** — notes and entities (places, items, events, factions, rules).
   *Optional; the durable facts the writer may need. Imported files land here.*
5. **Initial state** — time and place + a free-form description of the situation before chapter 1.
   *The rolling state's seed; depends on frame, lore and cast.*
6. **Languages / misc** — target translation languages and other options.

Rationale:

- Frame first, because genre/tone/style constrain the wording of every generated field.
- Lore next, because the cast and plot are grounded in the world.
- Cast before knowledge, because entity entries often name characters.
- State last, because it summarises the whole starting situation.
- The AI helps at each step using the already-filled steps (this already works for world, plot,
  characters and initial state; knowledge parsing comes later).

This sequence is a *recommended order*, not a blocking wizard; the author may jump between steps.

## 4. Knowledge and retrieval

- **Addressable entries.** Everything the writer may need is a `KnowledgeEntry` with `Kind`,
  `Title` and `Tags`. Retrieval and tools operate on these records.
- **Import.** Importing a file creates one or more entries. Initially one entry per file
  (`Kind = Note`); later an AI pass can split a large document into several typed entries.
- **Retrieval.** A keyword/BM25 ranker (embeddings later) selects the most relevant entries or
  fragments for a query. Retrieval is exposed to the model as `search_knowledge`.
- **State vs knowledge.** The rolling `WorldState` is always sent and kept tiny; durable facts
  live in the knowledge base and are pulled on demand.

## 5. Story query layer (Application)

A pure, read-only facade over a `Project` snapshot. It is the only place that knows how to read
the model; tools are thin wrappers over it. Being pure, it is unit-testable on a plain `Project`.

```text
Story()                     -> frame (name/genre/tone/style/... ) + lore
Characters()                -> [{ name, role }]
Character(name)             -> full profile
WorldState()                -> { timeAndPlace, description }
RecentLoglines(count)       -> [{ number, logline }]
ListEntries(kind?)          -> [{ title, kind, tags }]
GetEntry(titleOrId)         -> { title, kind, tags, content }
SearchKnowledge(query, kind?, topK) -> [ fragments with source ]
```

Adding a new capability the AI can ask for = add one method here + one tool declaration (below).

## 6. Tools (model-facing)

Read-only, deterministic, deduplicated, and bounded by a max-call count and a token budget.

| Tool | Arguments | Returns |
|------|-----------|---------|
| `story` | — | frame + lore |
| `characters` | — | `[{ name, role }]` |
| `character` | `name` | full profile |
| `world_state` | — | `{ timeAndPlace, description }` |
| `recent_loglines` | `count` | `[{ number, logline }]` |
| `list_entries` | `kind?` | `[{ title, kind, tags }]` |
| `get_entry` | `title` / `id` | `{ title, kind, tags, content }` |
| `search_knowledge` | `query`, `kind?`, `topK?` | ranked fragments |

The writer is seeded with the cheap frame + a **manifest** (character names+roles, entry
titles+kinds, chapter count) and pulls the rest itself with these tools. If a provider does not
support tool calling, the feature is unavailable for that provider (no fallback path is planned
for now).

## 7. Roles (passes)

- **Planner** (optional) — frame + state + cast → chapter directions / beats. Keeps the arc
  coherent. Tool-enabled.
- **Writer** — chapter brief + tools → the chapter text, streamed to the UI.
- **Editor** — finished chapter + constraints + tools → a revised text and a short list of the
  changes made (continuity, repetitions, style, pacing).
- **Summarizer** — the *final* chapter text + previous state → a logline and the new `WorldState`
  (structured JSON).

Per chapter the order is: `Planner? → Writer → Editor → Summarizer`. The state is derived from
the final, edited text.

## 8. Chapter pipeline

For chapter N:

1. (optional) plan the chapter's beats;
2. **writer**: seed frame + brief + manifest, run the tool loop, stream the draft;
3. **editor**: revise the draft against frame/state/cast;
4. **summarizer**: produce the logline and the new state from the final text;
5. save atomically: text, translations (invalidated), logline, `WorldState` snapshot.

State carry-forward: chapter N is written from the `WorldState` after chapter N-1 (the project's
initial state for chapter 1). Later chapters that depended on an edited chapter are marked
**stale**; a manual *recompute from here* refreshes their summaries/states.

## 9. Context budget

The always-seeded part is deliberately small:

1. system / style rules (frame);
2. current chapter brief (title/direction/notes);
3. current world state;
4. cheap manifest (cast, entry titles).

Everything else (full profiles, entry contents, history depth) is pulled by the model through
tools. Budgets apply to (a) the seed, (b) accumulated tool results, (c) recent loglines. The
assembler drops in priority order when over budget.

## 10. Roadmap

1. **Data model migration** — `StoryFrame` (style/POV/tense/rating), `KnowledgeEntry`, retire
   `ExtraFile` and `ChapterCharacter`.
2. **Story query layer** — `StoryQuery` over `Project` (+ BM25 retrieval).
3. **Tool calling** — extend the LLM client with `tools`/`tool_calls`; a declarative tool registry
   over `StoryQuery`.
4. **Agent pipeline** — writer loop, then summarizer, then editor; stale marking.
5. **Setup rework** — ordered steps, knowledge editor (import + manual), AI parsing of imports.

## 11. Open questions

- Embeddings vs BM25 for retrieval once projects grow.
- Whether the planner produces directions for all chapters up front or one ahead.
- How aggressively the editor may rewrite (style-only vs structural).
- Storing "appeared characters" per chapter for UI/continuity once participation is model-driven.
