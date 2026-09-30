# StoryTelling

StoryTelling is a cross-platform desktop app in **C# / .NET 10 / Avalonia** for writing
**multi-chapter stories with an AI assistant**. Describe your characters, world and plot
direction, choose how many chapters you want, and StoryTelling writes a coherent, connected
story chapter by chapter — staying inside the language model's context window through a
streamlined, token-budgeted memory instead of resending previous chapters.

## Features

- **AI-assisted setup** — generate world lore, characters, plot and the chapter plan, or
  write them yourself; your own text is treated as a hard constraint.
- **Chapter-by-chapter generation** — streamed to the editor, with per-chapter summaries and a
  per-chapter world state that keep continuity without resending earlier chapters.
- **Reference files** — attach `.txt` / `.md` notes; only the relevant fragments are used.
- **Single-file projects** — everything is stored in one portable `*.story.json` file.
- **Multi-language translation** — attach several target languages; each chapter gets a tab
  per language, produced on demand and cached.
- **Generate with AI** — a wizard that offers several AI options per field, with a brief and
  your own edits.

## Documentation

- [Overview](docs/overview.md)
- [Design (target)](docs/design.md)
- [Architecture](docs/architecture.md)
- [Context strategy](docs/context-strategy.md)
- [UI](docs/ui.md)
- [Stack](docs/stack.md)

## License

See [license.md](license.md).
