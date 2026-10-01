# AI calls — data and tools cross-reference

Every place the app talks to the model, what it sends, what the model may pull, and what comes
back. Provider and model come from user settings (`AppSettings`); all calls share
`Temperature`, `MaxTokens` (default 16384) and `TimeoutSeconds` unless noted.

## Defaults

| Knob | Value | Where |
|------|-------|-------|
| `MaxToolCalls` | 12 | `AppSettings` — per tool-gather loop |
| Tool-result budget | 24000 chars | `ToolAgent.DefaultMaxResultChars` |
| Writer seed budget | 2000 tokens (~8000 chars) | `ChapterContextAssembler.DefaultTokenBudget` |
| Required seed section cap | 4000 chars (state) | `ChapterContextAssembler` |
| Knowledge manifest cap (seed) | 120 entries | `PromptTemplates.Build` |
| Summary knowledge digest | 2000 chars/entry, 16000 total | `PromptTemplates.BuildSummarizer` |
| Import chunk | 8000 chars | `KnowledgeChunker.ImportMaxChars` |
| Retrieval fragment | 800 chars | `KnowledgeChunker.RetrievalMaxChars` |
| Max import chunks | 20 | `KnowledgeImportRequest.DefaultMaxChunks` |
| Structured retries | 3 | `GenerationAssistant` / `ChapterSummarizer` |
| Temperature | `AppSettings.Temperature`, capped at 0.2–0.3 for deterministic calls (translation, import, design, summary, review, editor notes) | each service |

## 1. Calls overview

| Call | Triggered from | Tools | Output | Persisted on |
|------|----------------|-------|--------|--------------|
| **Generate — World** | Setup *World* tab | yes | JSON array (title/body/genre/tone/style/POV/tense/rating ×N) | applied into Setup |
| **Generate — Book name** | Setup header | yes | JSON array (`ProjectName` ×N) | applied into Setup |
| **Generate — Knowledge entry** | knowledge editor | yes | JSON array (kind/title/tags/content ×N) | applied into editor |
| **Generate — Initial world state** | Setup *Initial world state* tab | yes | JSON array (timeAndPlace/description ×N) | applied into Setup |
| **Generate — Chapter settings** | chapter *Settings* tab | yes | JSON array (title/direction ×N) | chapter Title/Direction |
| **Generate — Chapter plan** | toolbar *Plan chapters* | yes | JSON array of N chapters (title/direction, in reading order) | replaces the chapter list (with confirmation) |
| **Generate — Final chapter** | toolbar *Finish* | yes | JSON (title/direction) resolving the story | appends a chapter |
| **Writer** | Generate / Write next | yes | streamed prose | `Chapter.ContentOriginal` |
| **Editor** | same run (after writer) | yes | streamed revised prose | `Chapter.ContentOriginal` (replaces draft) |
| **Editor notes** | same run (after edit) | no | JSON (`changes[]`: kind + note) from the local diff hunks | `Chapter.EditorNotes` |
| **Summarizer** | chapter run / *Regenerate summary* / recompute | no | JSON (logline/timeAndPlace/description/knowledgeChanges) | `Chapter.Logline`, `WorldState`, `KnowledgeChanges` |
| **Knowledge review** | Setup *Knowledge review* | yes | JSON (findings + optional fix) | shown in review window |
| **Knowledge import (extract)** | Setup *Import…* (`.md`) | no | JSON (`entries[]`) per chunk | reviewed → Setup |
| **Knowledge design (prompt)** | Setup *From prompt…* | no | JSON (`entries[]`) — one request for the whole prompt | reviewed → Setup |
| **Translation** | translation tab / *Translate chapter* / CLI | no | plain text; paragraphs that end up in the wrong script are re-translated individually | `Chapter.Translations[code]` |
| **Probe** | Settings *Test connection* / CLI `ping` | no | chat ping + structured-output probe | none |

## 2. What is always sent in the seed

Legend: `yes` always · `opt` included but truncated/optional · `-` not included.

| Call | System | Other project fields | Cast* | Knowledge manifest | Prev chapters + state | Own draft | Instruction | Avoid list | Brief |
|------|--------|----------------------|-------|--------------------|-----------------------|-----------|-------------|------------|-------|
| Generate — World / Book name / Initial state | generic | yes | - | yes (titles+kinds) | - | yes | yes | - | yes |
| Generate — Knowledge entry | generic | yes | - | yes | - | yes | yes | yes (existing titles except the edited one) | yes |
| Generate — Chapter settings | generic | yes (world) | - | yes (composed) | yes (≤5 loglines + previous state) | yes | yes | yes (other chapter titles) | yes |
| Generate — Chapter plan | generic | yes (world) | - | yes (base) | - | - | yes | - | yes |
| Generate — Final chapter | generic | yes (world) | - | yes (composed) | yes (≤5 loglines + previous state) | - | yes | yes (other chapter titles) | - |
| Writer | WriterSystem | - | - | opt (manifest, budgeted) | state yes; loglines via tool | - | WriterWrite | - | yes |
| Editor | EditorSystem | - | - | yes (manifest) | state yes; loglines via tool | draft (write step) | via write step | - | yes |
| Editor notes | notes system | - | - | - | - | original + revised text | yes | - | - |
| Summarizer | summary system | - | - | - | previous state yes | - | yes | - | - |
| Knowledge review | reviewer system | book name | - | yes | - | - | yes | - | optional |
| Import (extract) | importer system | - | - | - | - | source chunk | yes | - | optional |
| Design (prompt) | designer system | - | - | - | - | description chunk | yes | - | optional |
| Translation | translator system | - | - | - | - | text to translate | yes | - | - |

\* **Cast is currently unused** in production: `GenerationContext.Cast` is never set (characters are
knowledge entries, delivered through the manifest and tools). The "Characters in the story" block
only appears if a caller fills it.

## 3. Tools (read-only, model-facing)

Available to the **tool-backed** calls above (generation wizard, writer, editor, review), bounded by
`MaxToolCalls` and a 24000-char result budget; identical calls are cached.

| Tool | Arguments | Returns |
|------|-----------|---------|
| `story` | — | book name + world (title/body) + frame (genre/tone/style/POV/tense/rating) |
| `characters` | — | `[{ title, tags }]` for entries with `Kind = Character` |
| `character` | `name` | the full character entry |
| `initial_world_state` | — | `{ timeAndPlace, description }` (before chapter 1) |
| `recent_loglines` | `count?` | `[{ number, title, logline }]`, oldest first |
| `list_entries` | `kind?` | `[{ title, kind, tags }]` |
| `get_entry` | `key` (id or title) | `{ title, kind, tags, content }` |
| `search_knowledge` | `query`, `kind?`, `topK?` | ranked BM25 fragments |

## 4. Snapshot each call reads

| Call | Snapshot | Knowledge the tools see | Chapters the tools see |
|------|----------|-------------------------|------------------------|
| Generate — World / Book name / Knowledge / Initial state | current Setup | Setup knowledge (base) | none |
| Knowledge review | current Setup | Setup knowledge (base) | none |
| Generate — Chapter settings | effective project | base + diffs of chapters `< N` | chapters `< N` only |
| Writer / Editor | effective project | base + diffs of chapters `< N` | all chapters (state passed separately) |
| Summarizer | knowledge list passed directly (no tools) | base + diffs of chapters `< N` | previous state passed directly |

The project's knowledge base is **never mutated**; the knowledge a chapter sees is composed on
demand from the base plus the previous chapters' diffs (`KnowledgeComposer`).

## 5. Output validation

- Generation and summary parse strict JSON against a `json_schema`; invalid output is retried up to
  3 times, then surfaced (never parsed leniently).
- The editor streams text and then runs a separate structured call for change notes over the
  **local diff hunks** (best-effort: parse failure yields no notes; no notes when nothing changed).
- Generation options are de-duplicated and filtered against the **avoid list** before being shown.
- The writer and editor are told not to start with the chapter title or a heading; a deterministic
  cleanup also strips a leading line that repeats the chapter title (or a `Chapter N` heading).
- Editing a chapter's knowledge diff / summary, and changing the Setup world / knowledge / initial
  state, mark the affected chapters **stale**.
- Translation is checked deterministically: a paragraph whose letters are in the wrong script for the
  target language (e.g. Chinese in a Russian translation) is re-translated on its own, without a
  full AI review of the chapter.

## 6. Token/context notes

- The writer seed is assembled under a 2000-token budget with a fixed priority order
  (brief, frame, state → optional world body → manifest); tool results are bounded separately.
- The editor seed is not budgeted (it is small); the draft is passed in the write step.
- The summarizer includes the knowledge base with each entry's content truncated to 600 chars.
- Translation and import/design chunking keep requests inside `MaxTokens`.
