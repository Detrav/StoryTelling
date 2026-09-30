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
   files, the chapters (each with translations and a summary), the initial world state and
   the app settings.
2. **Form-driven setup.** Fill in character / world / plot descriptions manually, or let the
   AI fill them via the *Generate with AI* wizard. Fields the user already filled are treated
   as hard constraints during any AI completion.
3. **Generate with AI (wizard).** Not a chat: pressing a *Generate with AI* button opens a
   wizard that offers several AI options for one field, lets the user ask for more (with a
   brief), and applies the chosen option or the user's own edits.
4. **Outline per chapter.** The number of chapters is chosen by the user; each chapter carries
   a short direction / goal. The chapter list in the sidebar is the outline.
5. **Chapter pipeline.** Each chapter is generated independently. To fit the context window
   previous chapters are never resent:
   - request 1 — write the chapter text (English), streamed to the UI;
   - request 2 — analyse the finished chapter and produce its **Summary** (a logline plus a
     recap of events, character changes and how it ended);
   - request 3 — translate the chapter into each target language and cache it.
6. **State between chapters.** The situation at the start of the story is the **initial world
   state**, edited in Setup. After each chapter the app produces the chapter's **Summary**,
   which is passed into the following chapters together with the initial world state — it is
   the main memory carried forward.
7. **RAG-lite for extra files.** Attached `.txt` / `.md` files are chunked and indexed; only
   the top-K relevant fragments are injected into a prompt.
