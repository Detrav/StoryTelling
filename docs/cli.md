# Developer CLI — `storydev`

`StoryTelling.Cli` is a thin terminal front end over the same engine the desktop app uses. It exists
to drive one pass at a time when debugging the prompts, to batch-generate content, and to produce an
FB2 without launching the UI. It shares the project format, the services and the user settings with the
app.

```powershell
dotnet run --project src/StoryTelling.Cli -- <command> [options]
```

Run `storydev` with no command (or `help`) for the usage summary.

## Commands

| Command | What it does |
|---------|--------------|
| `ping` | Checks provider connectivity and whether the model supports JSON-schema structured output. |
| `raw` | Sends a raw prompt: `--prompt <text> [--system <text>]`. |
| `probe` | Prints the raw schema output for one `GenerationTarget` without applying it: `--target <Target> [--variants N] [--file <path>] [--brief ...] [--out <path>]`. |
| `gen` | Generates one target and applies it into a project: `--target <Target> --file <path> [--variants N] [--brief ...] [--out <path>] [--replace]`. |
| `setup` | Generates a fresh project setup: `--out <path> [--brief ...] [--characters N]`. |
| `write` | Writes chapters into an existing project: `--file <path> [--chapters N]`. |
| `summarize` | Rebuilds loglines, world state and knowledge diffs: `--file <path> [--chapter N \| --all] [--out <path>]`. |
| `translate` | Translates every chapter into a language: `--file <path> --language <code> [--out <path>]`. |
| `design` | Builds knowledge entries from a description: `--file <path> --prompt <text> [--out <path>]`. |
| `recompute` | Refreshes summaries/world state from a chapter onward: `--file <path> [--from N] [--out <path>]`. |
| `edit` | Runs the editor on one chapter (debug aid): `--file <path> --number N [--in <draft.txt>] [--out <edited.txt>]`. |
| `draft` | Runs the writer on one chapter without persisting (debug aid): `--file <path> --number N [--out <draft.txt>]`. |
| `create` | Full run (setup + chapters): `--out <path> [--chapters N] [--brief ...] [--characters N]`. |
| `export` | Writes an FB2 from a project: `--file <path> [--language <code>] [--out <path.fb2>]`. |

## Defaults

| Option | Default |
|--------|---------|
| `--variants` (`probe`, `gen`) | 1 |
| `--out` (`setup`, `create`) | `examples/story.story.json` |
| `--out` (`probe`) | `examples/probe-<Target>.json` |
| `--out` (`edit`, `draft`) | `examples/dbg/ch<N>-edited.txt` / `ch<N>-draft.txt` |
| `--chapters` | 1 for `write`, 2 for `create` |
| `--characters` | 3 |
| `--chapter` (`summarize`, without `--all`) | 1 |
| `--from` (`recompute`) | 1 |
| `--language` | `ru` for `translate`, `en` for `export` |

`--out` defaults write into the repository's `examples/` folder; pass an explicit path to keep the
working tree clean.

## Common options

Every command that talks to a provider accepts these overrides, which take precedence over
`settings.json` (and over the `STORYTELLING_*` environment variables):

```
--base-url --model --api-key --max-tokens --max-tool-calls --temperature --timeout
```

`export` is the exception: it reads no settings at all and needs no provider.

`Ctrl+C` cancels the running pass; partial progress is never written over the project file.

## Notes and limitations

- **`gen --replace`** clears the knowledge base first, and only for `--target Knowledge`; with any
  other target the flag is ignored.
- **`gen` only applies** the targets `ProjectName`, `World`, `InitialWorldState` and `Knowledge`. For
  `ChapterSettings`, `ChapterPlan` and `Finale` the options are printed but not written back, and the
  command reports `Applied 0 option(s).` — use `probe` to inspect those, and the app to apply them.
- The CLI covers the engine passes, not the app-level orchestration: there is no *Complete book*
  equivalent, no knowledge review and no setup/knowledge review dialog.
- It does **not** translate the book's metadata (title, annotation, chapter titles) — that action lives
  in the desktop app. Use the app once before exporting if the target language needs it; the CLI
  `export` command reads whatever the app already cached and otherwise falls back to English.
- Generation via `gen`/`setup`/`create` reuses the same prompts and tools as the app, so output is
  comparable between the two.

## Examples

The repository ships two sample projects: `examples/embers-of-ashen-reach.story.json` and
`examples/silent-beacon.story.json`.

```powershell
# is the provider reachable and structured-output capable?
dotnet run --project src/StoryTelling.Cli -- ping

# look at the raw schema output for the chapter-plan target before trusting it
dotnet run --project src/StoryTelling.Cli -- probe --target ChapterPlan --variants 5 --out ./probe-plan.json

# translate a project into Russian
dotnet run --project src/StoryTelling.Cli -- translate --file examples/embers-of-ashen-reach.story.json --language ru

# export the Russian FB2 (no provider needed)
dotnet run --project src/StoryTelling.Cli -- export --file examples/embers-of-ashen-reach.story.json --language ru --out ./book.fb2
```
