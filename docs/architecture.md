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
- `ISettingsService` — load/save `settings.json` (AppData).
- `IContextAssembler` — builds prompts within a token budget.
- `IRetrievalService` — RAG-lite fragment selection.
- `IGenerationAssistant` — produces field options for the *Generate with AI* wizard.
- `IChapterWriter` / `IChapterSummarizer` / `ITranslationService`.
- `IStoryGenerationService` — pipeline orchestration (single chapter + batch run).
- `IUndoRedoService` — snapshot + text-diff history (undo/redo).
- `ITextDiff` — produces a reversible line patch between two texts (DiffPlex).
- `IClock` / `IGuidGenerator` — injectable time and identity for testability.

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
