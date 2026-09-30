# Architecture

```
src/
  StoryTelling.sln             # solution
  .gitignore                   # the repository's single ignore file
  StoryTelling.Domain          # entities, value objects, state schema (no dependencies)
  StoryTelling.Application     # services + interfaces, orchestration, use cases
  StoryTelling.Infrastructure  # JSON project repository, OpenAI-compatible LLM client,
                               # retrieval index, settings storage
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

## Layers

- **Domain** — pure data: `Project`, `Character`, `WorldLore`, `PlotDescription`, `Chapter`,
  `WorldState`, `ExtraFile`.
- **Application** — behaviour: prompt building, pipeline orchestration, retrieval, validation.
  Declares the interfaces implemented by `Infrastructure`.
- **Infrastructure** — external concerns: JSON project persistence, the HTTP LLM client,
  the retrieval index and user settings.
- **StoryTelling (app)** — Avalonia UI, MVVM with CommunityToolkit.Mvvm.

## Key abstractions (Application)

- `ILlmClient` — `CompleteAsync`, `StreamAsync`, `CompleteStructuredAsync<T>`.
- `IProjectRepository` — load/save `*.story.json`.
- `ISettingsService` — provider settings + API key (AppData + env override).
- `IContextAssembler` — builds prompts within a token budget.
- `IRetrievalService` — RAG-lite fragment selection.
- `IGenerationAssistant` — produces field options for the *Generate with AI* wizard.
- `IChapterWriter` / `IChapterSummarizer` / `ITranslationService`.
- `IStoryGenerationService` — pipeline orchestration (single chapter + batch run).
- `IClock` / `IGuidGenerator` — injectable time and identity for testability.

## Notes

- The Avalonia base class is written as `Avalonia.Application` in the app project, because the
  `StoryTelling.Application` namespace would otherwise shadow the `Application` type.
- Prompts live in a single place in `Application` (`PromptTemplates`), never inlined in the UI.
- Persisted JSON is versioned (`schemaVersion`) for forward migration.
