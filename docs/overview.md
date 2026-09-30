# Overview

StoryTelling is a cross-platform desktop application (C# / .NET 10 / Avalonia) for writing
multi-chapter stories with an AI assistant. The AI acts as a **writer** (drafting chapters) and an
**editor** (revising them), drawing on a queryable **knowledge base**. You describe the world,
characters and plot direction, then add as many chapters as you want; the app produces a coherent,
connected story chapter by chapter while staying inside the LLM context window.

All product content, prompts, code and UI strings are written in **English**. The reader can
view a machine translation into their chosen target language, produced on demand and cached;
the English original is always preserved.

## Core idea

1. **Project-based workflow.** Create / save / open a story project. A project is a single
   JSON file (`*.story.json`) containing metadata, lore, characters, plot, a knowledge base of
   notes and entities, the chapters (each with translations and the resulting world state), the
   initial world state and the app settings.
2. **Form-driven setup.** Fill in character / world / plot descriptions manually, or let the
   AI fill them via the *Generate with AI* wizard. Fields the user already filled are treated
   as hard constraints during any AI completion.
3. **Generate with AI (wizard).** Not a chat: pressing a *Generate with AI* button opens a
   wizard that offers several AI options for one field, lets the user ask for more (with a
   brief), and applies the chosen option or the user's own edits.
4. **Outline per chapter.** The number of chapters is chosen by the user; each chapter carries
   a short direction / goal. The chapter list in the sidebar is the outline.
5. **Chapter pipeline.** Each chapter is produced as a sequence of AI passes; previous chapters
   are never resent:
   - write the chapter text (English), streamed to the UI;
   - revise it (editor: continuity, repetitions, style, pacing);
   - derive its **state** from the final text: a logline plus the new world state;
   - translate the chapter into each target language and cache it.
6. **State between chapters.** The situation before chapter 1 is the **initial world state**,
   edited in Setup. After each chapter the app stores an updated world state; the next chapter is
   written from that state rather than the initial one, so the prompt stays bounded. Which
   characters appear is decided by the writer through tools, not by hand.
7. **Knowledge base and tools.** Notes, places, items, events, factions and rules are stored as
   addressable knowledge entries; the writer pulls the ones it needs through read-only tools
   (including keyword search), so nothing irrelevant is sent.
