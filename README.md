# StoryTelling

StoryTelling is a cross-platform desktop app in **C# / .NET 10 / Avalonia** for writing
**multi-chapter stories with an AI assistant**. Describe your characters, world and plot
direction, choose how many chapters you want, and StoryTelling writes a coherent, connected
story chapter by chapter — staying inside the language model's context window through a
streamlined, token-budgeted memory instead of resending previous chapters.

## Features

- **AI-assisted setup** — generate world lore, characters, plot and the chapter plan, or
  write them yourself; your own text is treated as a hard constraint.
- **Chapter-by-chapter generation** — streamed to the editor, with a *rolling world state*
  that keeps continuity chapter over chapter.
- **Reference files** — attach `.txt` / `.md` notes; only the relevant fragments are used.
- **Single-file projects** — everything is stored in one portable `*.story.json` file.
- **Translation on demand** — English originals plus a cached translation into your language.
- **Assistant chat** — plan the story and accept, edit or discard proposed changes.

## Documentation

- [Overview](docs/overview.md)
- [Architecture](docs/architecture.md)
- [Context strategy](docs/context-strategy.md)
- [UI](docs/ui.md)
- [Stack](docs/stack.md)

## License

See [license.md](license.md).
