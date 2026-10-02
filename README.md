# StoryTelling

StoryTelling is a cross-platform desktop app in **C# / .NET 10 / Avalonia** for writing
**multi-chapter stories with an AI assistant**. Describe your characters, world and plot
direction, choose how many chapters you want, and StoryTelling writes a coherent, connected
story chapter by chapter — staying inside the language model's context window through a
streamlined, token-budgeted memory instead of resending previous chapters.

## Features

- **AI-assisted setup** — generate the **world** (setting + narrative frame) and the **initial world
  state**; every field is generated from the whole project through tools, and your own text is
  treated as a hard constraint.
- **Knowledge base** — the durable, mutable facts as addressable entries (a character is an entry
  with kind `Character`). Import Markdown or paste a large prompt and the AI structures it into
  typed entries. The writer pulls what it needs with read-only tools; the project base is never
  mutated.
- **Chapter planning** — plan the whole book at once: ask for N chapters and the AI produces the
  titles and directions as a complete arc (setup, rising action, climax, resolution). A *Finish*
  action plans a concluding chapter.
- **Chapter-by-chapter generation** — the AI writes each chapter (streamed), revises it as an editor
  (with change notes), and derives a logline, the new world state and a knowledge diff, keeping
  continuity without resending earlier chapters.
- **Complete book** — one action fills in everything still pending across the book: unwritten or
  out-of-date chapters, missing summaries and missing translations, with a progress bar, a log and
  Cancel.
- **Knowledge review** — an AI consistency check of the knowledge base, with structured one-click
  fixes.
- **Undo / redo** — snapshot + diff history over the whole project, so every AI action and every
  edit can be stepped back.
- **Multi-language translation** — target languages per project; translate on demand, cached per
  language, with out-of-date flags and automatic repair of paragraphs that come back in the wrong
  script. A separate *Translate book metadata* action translates the book title, the annotation and
  every chapter title per language.
- **FB2 export** — export the book (English original or any target language) to FictionBook 2.0,
  using translated metadata and chapter titles with an English fallback.
- **Single-file projects** — everything is stored in one portable `*.story.json` file (schema
  versioned and migrated on load).
- **Generate with AI** — a wizard that offers several AI options per field, with a brief and your
  own edits.
- **Developer CLI** — `storydev` drives the same engine from a terminal for probing prompts and
  batch passes.

## Requirements

A tool-calling language model with a large **context window — 64k tokens recommended**. The app
keeps its own prompt small (a token-budgeted seed plus bounded tool results), but it still expects
headroom for the seed, the tool-call loop and the generated chapter together. Models with a smaller
window (e.g. 16k) may return truncated or inconsistent chapters, especially with larger context
budgets — this is why the budgets are configurable in *Settings → Context*. Local models served by
LM Studio or Ollama work the same as a hosted provider.

## Documentation

- [Overview](docs/overview.md)
- [Design](docs/design.md)
- [Architecture](docs/architecture.md)
- [Context strategy](docs/context-strategy.md)
- [AI calls](docs/ai-calls.md)
- [UI](docs/ui.md)
- [Stack](docs/stack.md)
- [Developer CLI](docs/cli.md)
- [Creating a book from scratch](docs/creating-a-book.md)

## License

See [license.md](license.md).
