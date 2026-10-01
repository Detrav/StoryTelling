# Story engine — target design

**Status:** target design (describes the intended architecture; not all of it is built yet).
The current implementation is described in `architecture.md`; this document is the north star for
where the project is going.

Already built (see `architecture.md`):

- `World` (static world + narrative frame) and `KnowledgeEntry` + the *Knowledge* editor and
  Markdown AI import (phase 4).
- `StoryQuery`, BM25 retrieval, `StoryToolset`, `ToolAgent`, LLM tool calling (phase 5).
- The *Generate with AI* wizard is tool-backed: any setting is generated from the whole project
  (knowledge base included) through tools.
- *Knowledge review*: an AI consistency check of the knowledge base (non-blocking window).
  Findings can carry a structured **fix** (field replacements) applied in one click after a diff
  preview; **Fix with AI…** is offered on every finding — it re-generates the affected entry via
  the tool-backed wizard.
- Setup model simplification (phase 9): the typed form keeps only immutable facts (`World`);
  characters and all mutable facts live in the knowledge base; `Project.WorldState` is now
  `InitialWorldState`.

Still to build:

- A planner pass for the chapter pipeline (phase 6, optional).
- Remaining settings hardening (env-var overrides, *clear secrets*).

The product is, in the end, an AI **writer** that produces a coherent multi-chapter story plus an
AI **editor** that fixes it, backed by a **queryable knowledge base** and a set of read-only
**tools**. This document is the single reference for that direction so work can continue across
sessions and by different developers.

## 1. Principles

- **Never send whole previous chapters.** Context is bounded chapter over chapter.
- **Store knowledge addressable.** Every fact/snippet has an id, a kind, a title and tags so it
  can be *listed, fetched and searched*.
- **Keep the always-sent part tiny.** World (title + body + frame) + current state + chapter brief.
- **Typed where semantics matter, free-form where the author writes prose.** The world and the
  current state are typed; the knowledge base holds free-form prose with light metadata (a
  character is a `KnowledgeEntry` with `Kind = Character`).
- **Structured JSON for anything the app must parse.** Returned state/logline/etc. are validated
  against a schema; invalid output is retried or surfaced, never parsed leniently.
- **Every AI step is a separate, testable pass.** Planner / writer / editor / summarizer are
  independent units.

## 2. Data model (target)

```text
Project
  SchemaVersion, Id, Name, CreatedUtc, UpdatedUtc
  Settings:          StorySettings { OriginalLanguage, TargetLanguages[] }
  World:             World { Title, Body, Tags[], Genre, Tone, Style, PointOfView, Tense, Rating }
  Knowledge:         KnowledgeEntry[]
  InitialWorldState: WorldState { TimeAndPlace, Description }   // situation before chapter 1
  Chapters:          Chapter[]

World = the static "story bible": the setting (title + body) and the narrative frame
        (genre, tone, style, point of view, tense, rating). It never changes.

KnowledgeEntry {
  Id, Kind, Title, Tags[], Content
}

KnowledgeKind = Note | Character | Place | Item | Event | Faction | Rule | Background

Chapter {
  Number, Title, Direction, Notes,
  ContentOriginal,
  Translations{ languageCode -> text },
  StaleTranslations[],               // language codes whose translation is out of date
  Logline,
  WorldState?,                       // snapshot of the situation AFTER this chapter
  KnowledgeChanges[],                // this chapter's diff against the knowledge base (RAG-like)
  EditorNotes[],                     // what the editor changed (kind + note)
  Status: Draft | Generated | Edited | Stale,
  CreatedUtc
}
```

A chapter's summary is therefore `Logline` + `WorldState` + `KnowledgeChanges` (its database
mutations) — structured, retriable information rather than a free-form recap.

Notes on the model:

- `World` holds the immutable facts: the setting (`Title`, `Body`, `Tags`) and the always-sent
  narrative constraints (`Genre`, `Tone`, `Style`, `PointOfView`, `Tense`, `Rating`).
- There is **no separate `Frame`, `Premise` or `Direction`**: style constraints live in `World`,
  and plot/arc is expressed through the knowledge base and the per-chapter briefs.
- A character is a `KnowledgeEntry` with `Kind = Character` (`Title` = name, `Content` = a free-form
  description). There is no `Character` entity and no per-chapter character selection: the writer
  pulls characters through tools.
- `ExtraFile` is **removed**: files, items, events, places, factions, rules and free notes all
  become `KnowledgeEntry` records whose `Kind` differs. This is what makes them uniformly
  queryable and mutable.
- `InitialWorldState` is the small rolling seed for chapter 1; each chapter stores the snapshot of
  the situation *after* it in `Chapter.WorldState`. The state is **not** merged into the knowledge
  base — it changes every chapter and must stay cheap.

## 3. Setup sequence

Project setup is how the author builds the story "bible". It should read like a guided sequence,
because each step constrains the next:

1. **World** — book name, world title / body, genre, tone, style/POV/tense, rating.
   *The setting and the hard narrative constraints; they never change.*
2. **Knowledge** — characters and entities (places, items, events, factions, rules).
   *The durable, mutable facts; a character is a `Kind = Character` entry. Imported files land here.*
3. **Initial world state** — time and place + a free-form description of the situation before
   chapter 1. *The rolling state's seed; depends on the world and the knowledge base.*
4. **Languages / misc** — target translation languages and other options.

Rationale:

- World first, because genre/tone/style constrain the wording of every generated field.
- Knowledge next, because the cast and plot are grounded in it and it is what the writer pulls.
- State last, because it summarises the whole starting situation.
- The AI helps at each step using the already-filled steps (world, knowledge, initial state).

This sequence is a *recommended order*, not a blocking wizard; the author may jump between steps.

## 4. Knowledge and retrieval

- **Addressable entries.** Everything the writer may need is a `KnowledgeEntry` with `Kind`,
  `Title` and `Tags`. Retrieval and tools operate on these records.
- **Import (Markdown).** Importing a `.md` file runs the AI over the content and turns it into
  several typed entries: the text is chunked — preferring Markdown heading boundaries, but
  **merging small sections** up to the size limit so each request carries enough (coherent)
  text — each chunk is extracted into `KnowledgeEntry` records via a `json_schema` (kind enum),
  and the results are merged and de-duplicated by title. The same pipeline has a **design mode**:
  the author types a large prompt describing the world/story and the AI *designs* a coherent set
  of entries (characters, places, factions, items, events, rules, background) instead of extracting
  them from source material. A file/prompt that would need more than the chunk limit is **rejected
  with an error** before running. The user reviews the proposed entries (with checkboxes) before
  they are added. Import targets large chunks (~8000 characters, fewer requests); retrieval keeps
  small fragments (~800) for BM25 precision — both sizes are defined once in `KnowledgeChunker`.
- **Retrieval.** A keyword/BM25 ranker (embeddings later) selects the most relevant entries or
  fragments for a query. Retrieval is exposed to the model as `search_knowledge`.
- **State vs knowledge.** The rolling world state (the `InitialWorldState` seed plus each
  chapter's snapshot) is always sent and kept tiny; durable facts live in the knowledge base and
  are pulled on demand.

## 5. Story query layer (Application)

A pure, read-only facade over a `Project` snapshot. It is the only place that knows how to read
the model; tools are thin wrappers over it. Being pure, it is unit-testable on a plain `Project`.

```text
Story()                     -> world (name, title/body, genre/tone/style/...)
Characters()                -> knowledge entries with Kind = Character [{ title, tags }]
Character(name)             -> the full character entry
InitialWorldState()         -> { timeAndPlace, description }
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
| `story` | — | world (name, title/body, frame) |
| `characters` | — | `[{ title, tags }]` (entries with `Kind = Character`) |
| `character` | `name` | the full character entry |
| `initial_world_state` | — | `{ timeAndPlace, description }` |
| `recent_loglines` | `count` | `[{ number, logline }]` |
| `list_entries` | `kind?` | `[{ title, kind, tags }]` |
| `get_entry` | `title` / `id` | `{ title, kind, tags, content }` |
| `search_knowledge` | `query`, `kind?`, `topK?` | ranked fragments |

The writer and the editor are both seeded with only a minimal fixed context (brief, world frame,
current state, manifest) and pull the rest — characters, knowledge, history, search — through the
read-only tools. The editor then also returns change notes. If a provider does not support tool
calling, the feature is unavailable for that provider (no fallback path is planned for now).

The same toolset also backs the *Generate with AI* wizard: field generation seeds the world, the
target's own values as a draft and the manifests, then lets the model pull whatever else it needs —
characters, initial world state, knowledge entries and search — bounded by a configurable
`MaxToolCalls` (default 12). This is how the knowledge base and every other setting take part in
generating any single field. When creating new named entries (knowledge, chapters) the wizard also
passes the already-used titles as a **forbidden list**, and the assistant drops options that reuse a
taken name or duplicate each other — retrying with new names — so the model cannot hand back the
same character twice.

## 7. Roles (passes)

- **Planner** (optional) — world + state + cast → chapter directions / beats. Keeps the arc
  coherent. Tool-enabled.
- **Writer** — chapter brief + tools → the chapter text, streamed to the UI.
- **Editor** — finished draft + tools → a revised text and structured **change notes**
  (continuity, style, pacing, repetition, clarity). Tool-enabled like the writer.
- **Summarizer** — the *final* chapter text + previous state + the current knowledge base → a
  logline, the new `WorldState` and a **knowledge diff** (`knowledgeChanges`: create / update /
  delete, validated against the schema).

Per chapter the order is: `Planner? → Writer → Editor → Summarizer`. The state is derived from
the final, edited text.

## 8. Chapter pipeline

For chapter N:

1. (optional) plan the chapter's beats;
2. **writer**: minimal seed (brief + world frame + current state + manifest), run the tool loop,
   stream the draft;
3. **editor**: minimal seed + tool loop, revise the draft, then produce structured change notes;
4. **summarizer**: produce the logline, the new state and the knowledge diff from the final text;
5. save atomically: text, translations (marked stale), logline, `WorldState` snapshot, knowledge
   diff and editor notes.

State carry-forward: chapter N is written from the `WorldState` after chapter N-1 (the project's
`InitialWorldState` for chapter 1). Later chapters that depended on an edited chapter are marked
**stale**; a manual *recompute from here* refreshes their summaries/states.

Knowledge carry-forward: the project's knowledge base is **never mutated**. Each chapter stores only
its own diff (`Chapter.KnowledgeChanges`); the knowledge for chapter N is composed on demand from
the project base plus the diffs of chapters 1..N-1 (a character that died earlier is already
updated), so chapter N writes and pulls from the state after N-1 while the project settings stay as
authored. *Recompute from here* simply replaces the affected chapters' diffs.

## 9. Context budget

The always-seeded part is deliberately small:

1. system / style rules (the world's frame);
2. current chapter brief (title/direction/notes);
3. current world state;
4. cheap manifest (cast, entry titles).

Everything else (full profiles, entry contents, history depth) is pulled by the model through
tools. Budgets apply to (a) the seed, (b) accumulated tool results, (c) recent loglines. The
assembler drops in priority order when over budget.

## 10. Roadmap

1. **Data model migration** — *done*: `World`, `KnowledgeEntry`; `ExtraFile`, `StoryFrame`,
   `WorldLore`, `Character` and `ChapterCharacter` retired; characters are knowledge entries.
2. **Story query layer** — *done*: `StoryQuery` over `Project` + BM25 retrieval.
3. **Tool calling** — *done*: LLM client `tools`/`tool_calls`, `StoryToolset` + `ToolAgent`;
   field generation is tool-backed.
4. **Agent pipeline** — *done*: writer, editor and summarizer combined per chapter by
   `IChapterWorkflow`; `IChapterRunner` persists text/logline/state, marks later chapters stale and
   recomputes from a chapter. Next: planner, editor change notes, per-chapter knowledge diff.
5. **Setup rework** — *done* (phase 9): the typed form keeps only immutable facts (`World`);
   characters and mutable facts live in the knowledge base; the review is knowledge-only.

## 11. Open questions

- Embeddings vs BM25 for retrieval once projects grow.
- Whether the planner produces directions for all chapters up front or one ahead.
- How aggressively the editor may rewrite (style-only vs structural).
- Storing "appeared characters" per chapter for UI/continuity once participation is model-driven.
