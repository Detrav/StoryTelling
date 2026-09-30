# Overview

StoryTelling is a cross-platform desktop application (C# / .NET 10 / Avalonia) for writing
multi-chapter stories with an AI assistant. You describe characters, the world, the desired
plot direction and a chapter count; the app generates a coherent, connected story chapter by
chapter while staying inside the LLM context window.

All product content, prompts, code and UI strings are written in **English**. The reader can
view a machine translation into their chosen target language, produced on demand and cached;
the English original is always preserved.

## Core idea

1. **Project-based workflow.** Create / save / open a story project. A project is a single
   JSON file (`*.story.json`) containing metadata, lore, characters, plot, extra source
   files, outline, chapters, world state and the assistant transcript.
2. **Form-driven setup.** Fill in character / world / plot descriptions manually, or let the
   AI fill them via a *Generate* button. Fields the user already filled are treated as hard
   constraints during any AI completion.
3. **Assistant.** A chat panel that discusses the story, proposes development directions and,
   with user confirmation, proposes structured changes to the project.
4. **Outline per chapter.** The number of chapters is chosen by the user; each chapter has a
   short direction / goal that the assistant helps produce.
5. **Chapter pipeline.** Each chapter is generated independently. To fit the context window
   previous chapters are never resent:
   - request 1 — write the chapter text (English), streamed to the UI;
   - request 2 — analyse the finished chapter and produce the **next world state** (typed
     JSON) plus a chapter summary;
   - request 3 — translate the chapter into the target language and cache it.
6. **Rolling world state.** A strictly typed snapshot of "who / what / where / how things
   stand right now" is the only story memory carried between chapters.
7. **RAG-lite for extra files.** Attached `.txt` / `.md` files are chunked and indexed; only
   the top-K relevant fragments are injected into a prompt.
