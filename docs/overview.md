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
   JSON file (`*.story.json`) containing metadata, the **world** (setting + narrative frame), a
   knowledge base of notes and entities, the chapters (each with translations, a logline, a world
   state snapshot and its knowledge diff), the **initial world state** and the app settings.
2. **Form-driven setup with a split.** Only the **immutable** facts live in the typed setup form
   (the `World`); everything that can change during writing lives in the **knowledge base**
   (characters, places, factions, rules, …). Fields the user already filled are treated as hard
   constraints during any AI completion.
3. **Generate with AI (wizard).** Not a chat: pressing a *Generate with AI* button opens a wizard
   that offers several AI options for one field, lets the user ask for more (with a brief), and
   applies the chosen option or the user's own edits. Almost all context is pulled through tools.
4. **Chapter planning.** The *Plan chapters* action asks for a chapter count and an optional brief,
   and the AI produces that many titles + directions forming a complete arc (setup, rising action,
   climax, resolution); applying it replaces the chapter list. *Finish* plans a concluding final
   chapter. The sidebar chapter list is the outline.
5. **Chapter pipeline.** Each chapter is produced as a sequence of AI passes; previous chapters are
   never resent:
   - write the chapter text (English), streamed to the UI;
   - revise it (editor: continuity, repetitions, style, pacing) and return **change notes**;
   - derive the chapter's **summary** from the final text: a logline, the new world state and a
     **knowledge diff** (create / update / delete entries);
   - translate the chapter into each target language and cache it.
6. **State and knowledge between chapters.** The situation before chapter 1 is the **initial world
   state**. After each chapter the app stores an updated world state; the next chapter is written
   from that state. The project's knowledge base is **never mutated**: chapter N sees the base plus
   the diffs of chapters 1..N-1, composed on demand. Which characters appear is decided by the
   writer through tools, not by hand.
7. **Knowledge base and tools.** Notes, places, items, events, factions and rules are stored as
   addressable knowledge entries (a character is a `Kind = Character` entry); the writer pulls the
   ones it needs through read-only tools (including keyword search), so nothing irrelevant is sent.
   Entries can be imported from Markdown or designed from a large prompt.
8. **Export.** *File → Export FB2…* writes the book as FictionBook 2.0 in English or any target
   language, warning (but continuing) when some chapters are not translated.
