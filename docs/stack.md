# Stack

| Concern | Choice |
|---------|--------|
| Runtime | .NET 10 (`net10.0`) |
| UI | Avalonia (Windows / Linux / macOS) |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| DI | Microsoft.Extensions.DependencyInjection (composition root in the app project) |
| Logging | Microsoft.Extensions.Logging + a minimal file provider (one file per run) |
| Storage | single JSON file per project, `System.Text.Json` (source-generated context) |
| LLM | abstraction + OpenAI-compatible HTTP client (OpenAI, OpenRouter, Ollama, LM Studio, ...); streaming, JSON-schema output and tool calling |
| Retrieval | BM25 over chunked knowledge entries (`Application`, no external service) |
| Translation | per chapter and per target language, cached in `Chapter.Translations`; paragraphs in the wrong script are re-translated |
| Book metadata | title / annotation / chapter titles per language via one structured request, cached in `Project.MetadataTranslations` + `Chapter.TranslatedTitles` |
| Export | FictionBook 2.0 built with `System.Xml.Linq` (`Application.Export.Fb2Exporter`); pure function, reads caches and falls back to English |
| CLI | `StoryTelling.Cli` (`storydev`) drives the same engine from a terminal — see [cli.md](cli.md) |
| Tests | xUnit |
| UI language | English only (story text translated separately by the AI) |

## App data

| Item | Location |
|------|----------|
| Settings / recent projects | `%APPDATA%/StoryTelling/settings.json` (Windows), `~/.config/StoryTelling/settings.json` elsewhere |
| Logs | `<config>/logs/app-YYYYMMDD-HHMMSS.log` (one file per run, 20 newest kept) |
| Crash log | `<config>/crash.log` |
| Projects | wherever the user saves a `*.story.json` file |

Unknown JSON fields are ignored on load, and the project schema is versioned (`schemaVersion`,
currently **5**) with migrations applied on load; a file from a newer version is rejected.

## Conventions

- `Nullable` and `ImplicitUsings` enabled; file-scoped namespaces; one public type per file.
- `PascalCase` types/members, `_camelCase` private fields.
- Async I/O everywhere with `CancellationToken`; UI thread via `Dispatcher.UIThread` only when
  needed.
- No secrets committed; the provider API key lives in user settings under AppData and can be
  overridden per environment with `STORYTELLING_BASE_URL` / `STORYTELLING_MODEL` /
  `STORYTELLING_API_KEY`.

## Build / test / run

```powershell
dotnet restore src/StoryTelling.sln
dotnet build src/StoryTelling.sln -warnaserror
dotnet test src/StoryTelling.sln
dotnet run --project src/StoryTelling
dotnet format src/StoryTelling.sln
```
