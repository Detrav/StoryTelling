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
```

Dependency rule:

```
StoryTelling              -> Application -> Domain
StoryTelling.Infrastructure -> Application -> Domain
```

`Domain` depends on nothing. `Application` never depends on `Infrastructure` (inversion via
interfaces). `Infrastructure` and the app project are wired together in the composition root.

> This document describes the **current** architecture. The intended direction — a knowledge
> base, read-only query tools the model pulls context with, and AI writer/editor passes — is
> described in `design.md`. Where they differ, `design.md` is the target and this file is the
> present state.

## Layers

- **Domain** — pure data: `Project`, `World`, `KnowledgeEntry`, `WorldState`, `Chapter`.
- **Application** — behaviour: prompt building, pipeline orchestration, retrieval, validation.
  Declares the interfaces implemented by `Infrastructure`.
- **Infrastructure** — external concerns: JSON project persistence, the HTTP LLM client,
  the retrieval index and user settings.
- **StoryTelling (app)** — Avalonia UI, MVVM with CommunityToolkit.Mvvm.

## Key abstractions (Application)

Implemented:

- `ILlmClient` — `CompleteAsync`, `StreamAsync`, `CompleteStructuredAsync<T>` (JSON schema),
  `CompleteWithToolsAsync` (tool calling), `CheckStructuredOutputAsync` (capability probe).
- `IGenerationAssistant` — field options for the *Generate with AI* wizard (small seed + tool loop).
- `IProjectReviewAssistant` — reviews the **knowledge base** for inconsistencies and gaps
  (tool-backed), returning findings (severity, area, title, detail, suggestion). A finding may
  carry an optional structured `fix` (validated `ReviewEdit`s: target, reference, field, value) so
  it can be applied against the in-progress setup after a diff preview; otherwise the UI falls back
  to *Fix with AI…*.
- `IKnowledgeImporter` — turns imported Markdown into typed `KnowledgeEntry` records via the LLM.
- `StoryQuery` — read facade over a `Project` (world, cast, initial state, loglines, knowledge).
- `IKnowledgeRetriever` / `Bm25KnowledgeRetriever` — BM25 over chunked `KnowledgeEntry` content.
- `StoryToolset` / `ToolAgent` — declarative read-only tools for the model + the bounded gather loop.
- `IProjectRepository` — load/save `*.story.json`.
- `ISettingsService` — load/save `settings.json` (AppData).
- `IUndoRedoService` — snapshot + text-diff history (undo/redo).
- `ITextDiff` — produces a reversible line patch between two texts (DiffPlex).
- `IClock` / `IGuidGenerator` — injectable time and identity for testability.

Planned (not yet implemented):

- `IContextAssembler` — assembles a chapter prompt within a token budget.
- `IChapterWriter` / `IChapterEditor` / `IChapterSummarizer` / `ITranslationService` — chapter passes.
- `IStoryGenerationService` — pipeline orchestration (single chapter + batch run).

## Application foundation

- **Composition root** — the app project builds a `Microsoft.Extensions.DependencyInjection`
  service provider (`AppServices.Initialize`) and resolves `MainWindowViewModel` from it.
- **Settings** — `settings.json` under the app data directory (`%APPDATA%/StoryTelling` on
  Windows, `~/.config/StoryTelling` elsewhere). Loaded once at startup; written when the user
  presses *Apply* in the Settings dialog. Environment-variable overrides come later.
- **Logging** — `Microsoft.Extensions.Logging` with a minimal file provider writing one file
  per run to `<config>/logs/app-YYYYMMDD-HHMMSS.log`. *Help → Open logs folder* opens it.
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
  `GenerationSession`), and supports *Stop* / cancel-on-close. Reused in the setup dialog and the
  knowledge-entry dialog; single-field targets allow a free-text override.
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
