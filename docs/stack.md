# Stack

| Concern | Choice |
|---------|--------|
| Runtime | .NET 10 (`net10.0`) |
| UI | Avalonia (Windows / Linux / macOS) |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| DI | Microsoft.Extensions.DependencyInjection (composition root in the app project) |
| Logging | Microsoft.Extensions.Logging + a minimal file provider (one file per run) |
| Storage | single JSON file per project, `System.Text.Json` (source-generated context) |
| LLM | abstraction + OpenAI-compatible HTTP client (OpenAI, OpenRouter, Ollama, LM Studio, ...) |
| Tests | xUnit |
| UI language | English only (story text translated separately by the AI) |

## App data

| Item | Location |
|------|----------|
| Settings / recent projects | `%APPDATA%/StoryTelling/settings.json` (Windows), `~/.config/StoryTelling/settings.json` elsewhere |
| Logs | `<config>/logs/app-YYYYMMDD-HHMMSS.log` (one file per run) |
| Projects | wherever the user saves a `*.story.json` file |

Unknown JSON fields are ignored on load, so the schema can change without breaking old files.

## Conventions

- `Nullable` and `ImplicitUsings` enabled; file-scoped namespaces; one public type per file.
- `PascalCase` types/members, `_camelCase` private fields.
- Async I/O everywhere with `CancellationToken`; UI thread via `Dispatcher.UIThread` only when
  needed.
- No secrets committed; the provider API key lives in user settings under AppData (environment
  variables override).

## Build / test / run

```powershell
dotnet restore src/StoryTelling.sln
dotnet build src/StoryTelling.sln -warnaserror
dotnet test src/StoryTelling.sln
dotnet run --project src/StoryTelling
dotnet format src/StoryTelling.sln
```
