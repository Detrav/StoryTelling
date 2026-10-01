# Architecture

```
src/
  StoryTelling.sln             # solution
  .gitignore                   # the repository's single ignore file
  StoryTelling.Domain          # entities, value objects, state schema (no dependencies)
  StoryTelling.Application     # services + interfaces, orchestration, use cases
  StoryTelling.Infrastructure  # JSON project repository, OpenAI-compatible LLM client,
                               # user settings, file logging, text diff
  StoryTelling                 # Avalonia app (Views + ViewModels, CommunityToolkit.Mvvm)
  StoryTelling.Tests           # xUnit unit tests (Domain + Application, mocked I/O)
  StoryTelling.Cli             # `storydev`, developer CLI for the engine (no UI)

examples/                      # sample *.story.json projects
docs/                          # this documentation
```

Dependency rule:

```
StoryTelling              -> Application -> Domain
StoryTelling.Infrastructure -> Application -> Domain
StoryTelling.Cli          -> Infrastructure -> Application -> Domain
```

`Domain` depends on nothing. `Application` never depends on `Infrastructure` (inversion via
interfaces). `Infrastructure`, the app project and the CLI are wired together in their own
composition roots (`AppServices`, `Program`).

> This document describes the **current** architecture. The intended direction — a knowledge
> base, read-only query tools the model pulls context with, and AI writer/editor passes — is
> described in `design.md`. Where they differ, `design.md` is the target and this file is the
> present state.

## Layers

- **Domain** — pure data: `Project`, `World`, `KnowledgeEntry`, `KnowledgeChange`, `EditorNote`,
  `WorldState`, `Chapter`, `MetadataTranslation`.
- **Application** — behaviour: prompt building, pipeline orchestration, retrieval, validation.
  Declares the interfaces implemented by `Infrastructure`.
- **Infrastructure** — external concerns: JSON project persistence, the HTTP LLM client,
  the retrieval index and user settings.
- **StoryTelling (app)** — Avalonia UI, MVVM with CommunityToolkit.Mvvm.
- **StoryTelling.Cli** — the same engine driven from a terminal; see `cli.md`.

## Key abstractions (Application)

- `ILlmClient` — `CompleteAsync`, `StreamAsync`, `CompleteJsonAsync` (name + JSON schema),
  `CompleteStructuredAsync<T>` (JSON schema), `CompleteWithToolsAsync` (tool calling),
  `CheckStructuredOutputAsync` (capability probe).
- `IGenerationAssistant` — options for the *Generate with AI* wizard (small seed + tool loop) for
  the targets `World`, `ProjectName`, `Knowledge`, `InitialWorldState`, `ChapterSettings`,
  `ChapterPlan` (whole-book arc) and `Finale` (concluding chapter). New named entries get an
  **avoid list** of already-used titles.
- `IProjectReviewAssistant` — reviews the **knowledge base** for inconsistencies and gaps
  (tool-backed), returning findings (severity, area, title, detail, suggestion). A finding may
  carry an optional structured `fix` (validated `ReviewEdit`s: target, reference, field, value) so
  it can be applied against the in-progress setup after a diff preview; otherwise the UI falls back
  to *Fix with AI…*.
- `IKnowledgeImporter` — turns imported Markdown **or a pasted prompt** into typed `KnowledgeEntry`
  records via the LLM (`KnowledgeImportMode.Extract` / `Design`).
- `IChapterAgent` — tool-backed writer; streams the chapter text.
- `IChapterEditor` — tool-backed editor; returns the revised text plus structured `EditorNote`s
  built from the local diff.
- `IChapterSummarizer` — final text + state + knowledge → a logline, the new `WorldState` and a
  `KnowledgeChange` diff.
- `IChapterWorkflow` / `IChapterRunner` — orchestrate writer → editor → summarizer, persist the
  result, mark later chapters stale and recompute from a chapter.
- `IContextAssembler` — assembles the writer seed within a token budget (brief, frame, state →
  optional world body, manifest).
- `ITranslationService` — chapter translation per target language, cached in `Chapter.Translations`;
  paragraphs that come back in the wrong script are re-translated.
- `IMetadataTranslator` — one structured request per language for the book title, the annotation and
  every chapter title, cached in `Project.MetadataTranslations` and `Chapter.TranslatedTitles`.
- `MetadataTranslationCoverage` — the single place that decides whether a language's book metadata is
  complete (book title, annotation, every titled chapter) or stale; shared by the workspace, the
  translation dialog and the export dialog.
- `KnowledgeComposer` — composes the knowledge a chapter sees (project base + previous chapters'
  diffs); the base is never mutated.
- `StoryQuery` — read facade over a `Project` (world, cast, initial state, loglines, knowledge).
- `IKnowledgeRetriever` / `Bm25KnowledgeRetriever` — BM25 over chunked `KnowledgeEntry` content.
- `StoryToolset` / `ToolAgent` — declarative read-only tools for the model + the bounded gather loop.
- `Fb2Exporter` — deterministic FictionBook 2.0 export (`System.Xml.Linq`). It is a pure function:
  it reads the cached translations and metadata and falls back to the English source, never calling
  the model.
- `IProjectRepository` — load/save `*.story.json` (migrations run on load).
- `ISettingsService` — load/save `settings.json` (AppData / XDG config), with
  `STORYTELLING_BASE_URL` / `STORYTELLING_MODEL` / `STORYTELLING_API_KEY` overrides.
- `IUndoRedoService` — snapshot + text-diff history (undo/redo).
- `ITextDiff` — produces a reversible line patch between two texts (DiffPlex).
- `IClock` / `IGuidGenerator` — injectable time and identity for testability.

Not yet implemented: an OS keychain for the API key, a translation glossary, and embeddings for
retrieval.

## Project file format

`Project.SchemaVersion` is **5** (`ProjectSchema.Version`). On load, `ProjectMigrations` upgrades v1
(`frame`/`lore`/`characters`/`worldState` → `world`/`knowledge`/`initialWorldState`) and re-stamps v2–v4
to the current version; the fields added in v5 (`MetadataTranslations`, `StaleMetadataTranslations`,
`TranslatedTitles`) default when absent. A newer file is rejected rather than truncated.

## Application foundation

- **Composition root** — the app project builds a `Microsoft.Extensions.DependencyInjection`
  service provider (`AppServices.Initialize`) and resolves `MainWindowViewModel` from it.
- **Settings** — `settings.json` under the app data directory (`%APPDATA%/StoryTelling` on
  Windows, `~/.config/StoryTelling` elsewhere). Loaded once at startup; written when the user
  presses *Apply* in the Settings dialog. `STORYTELLING_BASE_URL`, `STORYTELLING_MODEL` and
  `STORYTELLING_API_KEY` override the stored values on load, which keeps the key out of the file.
- **Logging** — `Microsoft.Extensions.Logging` with a minimal file provider writing one file
  per run to `<config>/logs/app-YYYYMMDD-HHMMSS.log`, keeping the 20 newest. *Help → Open logs
  folder* opens it. If the log file cannot be created the app keeps running without logging.
- **Persistence** — projects are single `*.story.json` files via `IProjectRepository`.
  Unknown/missing JSON fields are ignored, so the schema can evolve without breaking old files;
  `schemaVersion` guards against a file written by a newer major version.
- **Errors** — external failures (file not found, unreadable/corrupt JSON, write errors) are
  logged and surfaced to the user through an error dialog.
- **LLM client** — `OpenAiCompatibleLlmClient` posts to `{baseUrl}/chat/completions` (SSE for
  streaming); a bare host such as `http://127.0.0.1:1234` gets `/v1` appended, so LM Studio,
  Ollama and OpenAI all work from the same dialog. The shared `HttpClient` has no timeout of
  its own — each call uses `LlmConnection.Timeout`. Failures are mapped to typed `LlmException`
  kinds (authentication, rate-limited, timeout, invalid response, network). The Settings dialog
  *Test connection* button calls the configured provider directly.
- **Structured output** — `CompleteStructuredAsync<T>` sends `response_format: json_schema`
  (schema exported from the type's `JsonTypeInfo` via `JsonSchemaExporter`) and retries up to
  three times on malformed JSON. `json_object` is *not* used: some providers (e.g. this LM Studio
  build) reject it. `CheckStructuredOutputAsync` probes support with a minimal schema; *Test
  connection* runs both a chat ping and this probe and warns when structured output is missing.
  Typed operations (e.g. world-state updates) will require the probe to succeed.
- **Generate with AI** — generation is group-based and **tool-backed**. A `GenerationTarget`
  defines the fields and their JSON-schema names (e.g. `World` = title + body + genre/tone/style/
  POV/tense/rating, `Knowledge` = kind/title/tags/content). `GenerationAssistant` seeds the model
  with a small context — the target's own values as a "current draft", the other filled fields as
  fixed constraints, a knowledge-base manifest (including the character entries) and the initial
  world state — and instructs the model to consult the project with the tools first. It then runs a
  `ToolAgent` loop: the model pulls what it needs through the read-only `StoryToolset`
  (`characters`, `character`, `initial_world_state`, `list_entries`, `get_entry`,
  `search_knowledge`, `recent_loglines`), bounded by `AppSettings.MaxToolCalls`; identical calls
  are served from a cache, and accumulated tool output is bounded (excess is truncated, then
  reported as exhausted). A final `CompleteJsonAsync` with a `json_schema` fixes the item shape and
  `minItems`/`maxItems` to the requested variant count. The tools read a `Project` snapshot built
  from the dialog's current values, so the knowledge base and every other setting participate.
  `AiWizardViewModel` shows the stage and the tool-call count (kept after success, e.g.
  "1 options · 6 tool calls"), caches the gathered context between *More options* (per
  `GenerationSession`), and supports *Stop* / cancel-on-close. Reused in the setup dialog, the
  knowledge-entry dialog, the chapter Settings tab (*Generate with AI*), the chapter planner and the
  *Finish* action; single-field targets allow a free-text override. New named entries are filtered
  against the already-used titles (the `Cast` concept was retired — characters are knowledge
  entries, delivered through the manifest and tools).
- **Chapter pipeline** — `ChapterWorkflow` runs writer → editor → summarizer for one chapter;
  `ChapterRunner` persists text, logline, world state, knowledge diff and editor notes, marks later
  chapters stale, recomputes from a chapter, and regenerates a summary alone. The knowledge a
  chapter sees is composed on demand (`KnowledgeComposer`). The writer and editor are both
  tool-backed with a minimal seed; the editor also returns change notes derived from the local diff.
  A chapter's stored summary is `Logline` + `WorldState` + `KnowledgeChanges` (no free-form recap).
- **Translation** — `TranslationService` translates a chapter per target language (cached in
  `Chapter.Translations`, per-language `StaleTranslations`), retries truncated output and
  re-translates only paragraphs that come back in the wrong script.
- **Book metadata** — `MetadataTranslationService` sends **one** structured request per language for
  the book title, the annotation and every chapter title (`MetadataTranslationSchema`), cached in
  `Project.MetadataTranslations` and `Chapter.TranslatedTitles`. A field the model omits is stored
  empty on purpose so `MetadataTranslationCoverage` keeps reporting it as missing; only the exporter
  substitutes the English source. `Project.StaleMetadataTranslations` marks the languages whose
  cached metadata is out of date (book name, annotation source, chapter set or a chapter title
  changed) so the next run refreshes them. Languages without a cache entry are simply *missing*, not
  *stale*.
- **Export** — `Fb2Exporter` builds FictionBook 2.0 from the project (English or a target language),
  using the cached chapter translations and book metadata and falling back to the English text for
  anything missing. It is a pure function and never calls the model.
- **Undo / redo** — `IUndoRedoService` keeps the current state (the project as JSON) plus a
  list of line diffs (like git) computed with DiffPlex. `Push(name)` is called after an
  explicit action; if nothing changed no entry is added. Before an undo/redo a safety snapshot
  is taken, so any change made without a push is committed first (dropping the redo tail).
  Undo/redo are async and guarded by a gate that drops calls while one is running. The history
  is session-only and each project/workspace starts fresh. Native `TextBox` undo is disabled;
  text edits commit on `LostFocus` and after a short debounce, and the standard shortcuts
  (`Ctrl+Z/Y`, `Ctrl+S`, ...) are bound at the window level. Dialogs reuse the same service
  through `UndoableDialogViewModel`, each creating its own `UndoRedoService` instance and
  routing `Ctrl+Z/Y` via `UndoRedoKeyboard`.

## Notes

- The Avalonia base class is written as `Avalonia.Application` in the app project, because the
  `StoryTelling.Application` namespace would otherwise shadow the `Application` type.
- Prompts live in a single place in `Application` (`PromptTemplates`), never inlined in the UI.
- Persisted JSON is versioned (`schemaVersion`) for forward migration.
