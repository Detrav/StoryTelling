# Context-window strategy

The model's context is limited, so the app never sends whole previous chapters. Continuity is
preserved through a small, evolving state plus a **queryable knowledge base that the model pulls
on demand**. The full target is described in `design.md`; this document focuses on how the
context window is managed.

| Technique | Purpose |
|-----------|---------|
| Never send previous chapters | Bounded prompt size chapter over chapter |
| Static world | Background and narrative frame that never change (title + body + genre/tone/style/...) |
| Initial world state (2 fields) | `TimeAndPlace` + a free-form `Description`, seeded in Setup and rewritten after each chapter |
| Per-chapter state snapshot | Reproducible regeneration; chapter N reads the state after N-1 |
| Addressable knowledge base | Facts/entities (`KnowledgeEntry`; characters are `Kind = Character`) the model can list, fetch and search |
| Model-pulled context (tools) | The writer asks for what it needs instead of the app guessing |
| Structured (JSON) outputs for state | Deterministic parsing, fewer tokens, no drift |
| Per-chapter knowledge diff | The summarizer returns create/update/delete edits stored on the chapter; the next chapter's knowledge is composed from the project base plus the previous diffs |
| Token budget on the seed + tool results | Truncation when over budget |

## Model requirements

The prompt is deliberately small, but the model still needs room for the seed, the tool-call loop
and the generated chapter at once. A context window of **64k tokens is recommended**; on a smaller
window (e.g. 16k) the provider may truncate the input or the output, which shows up as cut-off or
inconsistent chapters. The seed budget, the number of recent loglines, the required-section cap and
the tool-result budget are user settings (*Settings → Context*), so the footprint can be tuned down
to fit a smaller window at the cost of continuity.

## The world state evolves

- **Setup** seeds the situation *before chapter 1* on the project (`Project.InitialWorldState`):
  when and where the story opens, plus a free-form description.
- After a chapter is written, the model returns a **logline** and the **new state**
  (`TimeAndPlace` + `Description`). The state is stored on that chapter (`Chapter.WorldState`).
  Chapter N is written from the state after chapter N-1.
- The static background (geography, customs) and the narrative frame live in `World`, not in the
  state; they never change and are sent every time.
- The initial state stops being sent once a later chapter has a snapshot.

## What is sent, and what is pulled

Always sent (deliberately small):

1. system / style rules (the story `World`);
2. the current chapter brief (title / direction / notes);
3. the chapter's **position** (*chapter N of M*, continue rather than restart);
4. the current world state;
5. the **story so far** — the first chapter's logline plus the most recent ones, so the writer
   sees the plot progression and not only the current snapshot;
6. a cheap **manifest** (character names from `Kind = Character`, knowledge-entry titles + kinds,
   chapter count).

Pulled on demand by the model through read-only **tools** (`design.md` §6):

- full character entries, knowledge entries and their content, deeper history
  (`recent_loglines`), and `search_knowledge` (BM25 retrieval over the knowledge base).

This is the key change from earlier drafts: the app no longer decides up front which characters or
notes to include — **the writer pulls exactly what it needs**. Per-chapter character checkboxes
are removed.

## Seed priority order

The writer seed is built by `ChapterContextAssembler` under a budget. The budget and the number of
recent loglines are user settings — `ContextTokenBudget` (default 4000 tokens, roughly 16000
characters), `RecentLoglineCount` (default 3) and `ContextRequiredSectionMaxChars` (default 6000,
the cap for the world state). Sections are added in priority order; the **required** ones (brief,
frame, position, state, story so far) are always included, and the **optional** ones (the world's
title + body, then the manifest) are added only while budget remains and are truncated when they do
not fit:

1. chapter brief (number / title / direction / notes);
2. story frame from `World` (genre, tone, style, point of view, tense, rating);
3. chapter position (*chapter N of M*; continue, do not restart);
4. current world state (capped by `ContextRequiredSectionMaxChars`);
5. story so far (first logline + the last `RecentLoglineCount`, with an omitted marker);
6. the world itself (title + body) — optional, truncated;
7. cheap manifest (book, chapter count, cast, entry titles + kinds) — optional, truncated.

Tool results are appended after the seed and are themselves bounded (`ToolResultMaxChars`, default
24000 characters).

## Invariants

- A finished chapter yields a logline, a new world state and a **knowledge diff**, returned as
  structured JSON validated against the typed schema. Invalid output is retried or surfaced as an
  error, never parsed leniently. The diff is stored on the chapter; the knowledge for the next
  chapter is composed from the project base plus the previous chapters' diffs, so the project's
  knowledge base is never mutated.
- Tool results are deduplicated and bounded (max calls + token budget); the seed is bounded too.
- Editing or regenerating a chapter marks every later chapter **stale**; a manual
  *recompute from here* refreshes them. Nothing downstream is recomputed automatically.
- The context is always assembled by `IContextAssembler`; UI code never builds prompts.
