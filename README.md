# StoryTelling

A cross-platform desktop application (C# / .NET 10 / Avalonia) for writing multi-chapter
stories with an AI assistant. The user describes characters, the world, the desired plot
direction and a chapter count; the app then generates a coherent, connected story chapter
by chapter, staying inside the LLM context window through an explicit context-management
strategy.

> All product content, prompts, code, UI strings and docs are written in **English**.
> The end user reads the story in the **original language (English)**, and may view a
> machine translation into their chosen target language, produced on demand and cached.

## Core idea

1. **Project-based workflow.** Create / save / open a story project. A project is a single
   JSON file (`*.story.json`) that contains everything: metadata, lore, characters, plot
   description, extra source files, outline, chapters, world state, assistant transcript.
2. **Form-driven setup.** Fill in character / world / plot descriptions manually, or let the
   AI fill them via a *Generate* button. If the user already filled some fields, those are
   treated as hard constraints during any AI completion.
3. **Assistant.** A chat panel that can discuss the story, propose several directions for
   development, and (with user confirmation) propose structured changes to the project.
4. **Outline per chapter.** The number of chapters is chosen by the user. Each chapter has a
   short direction / goal; the assistant helps produce the plan.
5. **Chapter pipeline.** Each chapter is generated independently. To fit the context window
   we never resend previous chapters:
   - request 1 — write the chapter text (English), streamed to the UI;
   - request 2 — analyse the finished chapter and produce the **next world state**
     (typed JSON) plus a chapter summary;
   - request 3 — translate the chapter into the user's target language and cache it.
6. **Rolling world state.** A strictly typed snapshot of "who / what / where / how things
   stand right now" (characters, locations, items, active and resolved plot threads,
   recent events, open questions) is the only story memory carried between chapters.
7. **RAG-lite for extra files.** Attached `.txt`/`.md` files (D&D notes, board-game docs,
   reference material, ...) are chunked and indexed; only the top-K relevant fragments are
   injected into a prompt.

## Context-window strategy (the heart of the app)

| Technique | Purpose |
|-----------|---------|
| Never send previous chapters | Bounded prompt size chapter over chapter |
| Typed rolling world state | Preserves continuity without the prose |
| Chapter summaries | Cheap recall of "what already happened" |
| Lore stored once + condensed when large | Stable, shared background |
| RAG-lite retrieval of extra files | Only relevant fragments per prompt |
| Structured (JSON) outputs for state / outline | Deterministic parsing, fewer tokens, no drift |
| Per-request token budget / context assembler | Priorities and truncation when over budget |

## Architecture

```
StoryTelling.sln
src/
  StoryTelling.Domain          # entities, value objects, state schema (no dependencies)
  StoryTelling.Application     # services + interfaces, orchestration, use cases
  StoryTelling.Infrastructure  # JSON project repository, OpenAI-compatible LLM client,
                               # retrieval index, settings storage
  StoryTelling.Ui              # Avalonia app (Views + ViewModels, CommunityToolkit.Mvvm)
tests/
  StoryTelling.Tests           # xUnit unit tests (Domain + Application, mocked I/O)
```

Dependency rule: `Ui -> Application -> Domain`, `Infrastructure -> Application -> Domain`.
`Domain` depends on nothing. `Application` never depends on `Infrastructure` (inversion
via interfaces).

### Key abstractions (Application)

- `ILlmClient` — `CompleteAsync`, `StreamAsync`, `CompleteStructuredAsync<T>`.
- `IProjectRepository` — load/save `*.story.json`.
- `ISettingsService` — provider settings + API key (AppData + env override).
- `IContextAssembler` — builds prompts within a token budget.
- `IRetrievalService` — RAG-lite fragment selection.
- `IAssistantService` — chat / planning, structured change proposals.
- `IChapterWriter` / `IStateUpdater` / `ITranslationService`.
- `IStoryGenerationService` — pipeline orchestration (single chapter + batch run).

## UI (Avalonia) — planned surface

- **Start screen**: new project, open, recent projects.
- **Workspace tabs**:
  - *Setup* — world / characters / plot / chapter count / target language / extra files,
    each with a *Generate with AI* button (optional free-text wish).
  - *Assistant* — chat with the AI, accept / edit / discard proposed changes.
  - *Outline* — chapter list with directions.
  - *Chapters* — reader/editor, *Generate next chapter*, *Run all remaining* (queue +
    stop), Original (EN) / Translated toggle.
  - *World state* — viewer/editor of the rolling state.
- **Settings**: provider (OpenAI-compatible), base URL, model, API key, target language.

## Technology choices

| Concern | Choice |
|---------|--------|
| Runtime | .NET 10 |
| UI | Avalonia (Windows / Linux / macOS) |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| Storage | single JSON file per project, `System.Text.Json` |
| LLM | abstraction + OpenAI-compatible HTTP client (OpenAI, OpenRouter, Ollama, ...) |
| Tests | xUnit |
| UI language | English only (story text translated separately by the AI) |

## Status

Planning / bootstrap. No application code yet.
