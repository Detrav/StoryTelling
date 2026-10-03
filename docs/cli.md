# Developer CLI — `storydev`

`StoryTelling.Cli` is a thin terminal front end over the same engine the desktop app uses. It exists
to drive any pass of the pipeline from a script or an agent: set up a world, add and write chapters,
summarize, review, translate, export. It shares the project format, the services and the user settings
with the app.

```powershell
dotnet run --project src/StoryTelling.Cli -- <command> [options]
```

Run `storydev` with no command (or `help`) for the usage summary.

## Logging

Every command writes a single run log to `%AppData%/StoryTelling/logs/app-<timestamp>.log` (20 most
recent files are kept). The app logs at `Information`; the CLI does too by default.

```
--log-level <Trace|Debug|Information|Warning|Error>   file log verbosity
--verbose                                             alias for --log-level Debug
```

At `Debug` every API request and response is logged: the URL, model, temperature, full messages,
tools, schema, the response body, and — for every call — the elapsed time, `finish_reason`, token usage
and character count. Streamed generations (writer, editor) log their full collected text. Non-2xx
responses are logged at `Warning` with the status, reason and body. The API key is never logged.
There are no separate trace files any more — use `--verbose` and read the log.

## Commands

| Command | What it does |
|---------|--------------|
| `ping` | Checks provider connectivity and whether the model supports JSON-schema structured output. |
| `gen` | Generates one target and applies it into a project: `--target <Target> --file <path> [--variants N] [--brief ...] [--out <path>] [--replace]`. |
| `setup` | Generates a fresh project setup (name, world, characters, initial state): `--out <path> [--brief ...] [--characters N]`. |
| `write` | Writes chapters into an existing project: `--file <path> [--chapters N]`. |
| `complete` | Finishes the whole book: writes pending/stale chapters, rebuilds missing summaries, translates when needed: `--file <path> [--languages ru,de] [--no-translate] [--out <path>]`. |
| `chapter` | Manages the chapter list: `--action <add\|remove\|move\|status> --file <path> [--number N] [--role Auto\|Opening\|Middle\|Finale] [--notes ...] [--title ...] [--direction ...] [--suggest] [--pick N] [--from N] [--status Draft\|Generated\|Stale]`. With `--suggest`, the AI proposes up to three titles + directions from the previous chapters (pick one with `--pick`; the model may return fewer). |
| `set` | Edits fields by hand: `--what <project\|world\|state\|chapter\|knowledge> --file <path> [field options]`. |
| `settings` | Shows or edits provider settings: `[show\|set] [--model ...] [--base-url ...] [--api-key ...] [--temperature ...] [--languages ru,de]`. |
| `import` | Imports knowledge from Markdown files: `--file <path> --from <file.md\|dir> [--mode extract\|design] [--brief ...]`. |
| `summarize` | Rebuilds loglines, world state and knowledge diffs: `--file <path> [--chapter N \| --all] [--out <path>]`. |
| `recompute` | Refreshes summaries/world state from a chapter onward: `--file <path> [--from N] [--out <path>]`. |
| `translate` | Translates chapters and/or metadata into a language: `--file <path> --language <code> [--metadata-only] [--with-metadata] [--out <path>]`. |
| `design` | Builds knowledge entries from a description: `--file <path> --prompt <text> [--out <path>]`. |
| `regenerate` | Rewrites one chapter in place with the current seed: `--file <path> --chapter N [--out <path>]`. |
| `continuity` | Checks a chapter's prose and plan against the world, initial state and facts: `--file <path> [--chapter N \| --all]`. |
| `review` | AI-reviews the knowledge base for contradictions: `--file <path> [--check numbers,facts\|all] [--brief <text>] [--out <path.json>] [--apply] [--apply-content]`. |
| `apply-fixes` | Applies curated knowledge edits: `--file <book.json> --fixes <edits.json>`. |
| `context` | Prints the assembled writer prompt for one chapter (no provider): `--file <path> --number N`. |
| `export` | Writes an FB2: `--file <path> [--language <code>] [--out <path.fb2>]`. |
| `create` | Full run (setup + write): `--out <path> [--chapters N] [--brief ...] [--characters N]`. |
| `judge` | Scores whether a chapter continues the story: `--file <path> --chapter N`. |
| `compare` | Picks which of two chapter drafts continues better: `--a <pathA> --b <pathB> --chapter N`. |
| `experiment` | Runs full-book passes per hypothesis: `--file <base> [--out <dir>] [--from N] [--to M]`. |

## Typical full pipeline

```powershell
# 1. a world, characters and the initial situation
storydev setup --out book.story.json --brief "a lighthouse keeper on a tideless sea" --characters 3

# 2. add chapters one at a time (optionally let the AI propose the title + direction)
storydev chapter --action add --file book.story.json --role Opening --notes "the storm arrives" --suggest

# 3. write everything, then summarize/translate as configured
storydev complete --file book.story.json

# 4. verify
storydev continuity --file book.story.json --all
storydev review --file book.story.json --check all

# 5. translate and export
storydev translate --file book.story.json --language ru --with-metadata
storydev export --file book.story.json --language ru --out book.ru.fb2
```

See `docs/creating-a-book.md` for the step-by-step tutorial.

## `set` field options

| `--what` | Options |
|----------|---------|
| `project` | `--name` |
| `world` | `--title --body --genre --tone --style --pov --tense --rating` |
| `state` | `--time-and-place --situation` (the initial world state) |
| `chapter` | `--number --title --direction --notes --logline --role --text \| --text-file --time-and-place --situation` |
| `knowledge` | `--entry <title> --kind --title --content --tags --status` |

## `--out` and side files

Most commands default `--out` to the input file and edit it in place; pass an explicit `--out` for a
copy. Data products that are inputs to other tools are still written explicitly: `review --out` writes
the findings JSON that `apply-fixes` consumes. The `experiment` command still writes its per-hypothesis
`book.story.json`; everything else goes to the run log.

## Common options

Every command that talks to a provider accepts these overrides, which take precedence over
`settings.json` (and over the `STORYTELLING_*` environment variables):

```
--base-url --model --api-key --max-tokens --max-tool-calls --temperature --timeout
--context-token-budget --recent-loglines --required-cap --tool-result-max-chars
```

`export` is the exception: it reads no settings at all and needs no provider.

`Ctrl+C` cancels the running pass; partial progress is never written over the project file.

## Notes and limitations

- **`gen --replace`** clears the knowledge base first, and only for `--target Knowledge`.
- **`gen`** applies only the targets `ProjectName`, `World`, `InitialWorldState` and `Knowledge`. Use
  `chapter`, `set` and the chapter commands for chapter targets.
- `complete` writes and summarizes in reading order and cascades: once a chapter is written, everything
  after it is regenerated. It skips chapters whose world, initial state or direction is missing.
- The CLI covers the engine. Interactive affordances of the app (per-finding preview, undo/redo, dialogs)
  have no CLI equivalent and are not needed for scripted generation.
