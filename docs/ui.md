# UI

A single desktop window. At the top level it switches between a **Welcome** state (no project
open) and a **Workspace** state (project open). The application menu is always present.

## Window title

The title shows the book name and an unsaved marker, for example `* The Ember Crown —
StoryTelling` (the leading `*` appears while there are unsaved changes).

## Menu

- **File** — New project, Open project…, Save, Save as…, Project setup…, Settings…, Close
  project, Exit.
- **Edit** — Undo, Redo, Cut, Copy, Paste (placeholders).
- **View** — Show chapter list (toggle the sidebar).
- **Chapter** — Generate chapter, Regenerate, Stop, Add chapter, Delete chapter, Move up,
  Move down.
- **Help** — About.

Every chapter action is available both from the toolbar and from the **Chapter** menu.

## Welcome

- *New project* and *Open project…* (also in the File menu).
- A *Recent* list of previously opened projects.

## Workspace

```
┌────────────────────────────────────────────────────────────┐
│ File  Edit  View  Chapter  Help                            │
├──────────────┬─────────────────────────────────────────────┤
│ Chapters  [+]│  Chapter 3 — The Long Night        ▶ ↻ ■    │
│ 1 Embers     ├─────────────────────────────────────────────┤
│ 2 Ashes      │ Ch (EN) | Ch (RU) | Ch (DE) | Summary | Set │
│ 3 Long Night │                                             │
│ 4 …          │  editor text …                             │
└──────────────┴─────────────────────────────────────────────┘
```

- **Sidebar** — the list of chapters is always visible (unless toggled off in *View*). The
  chapter list **is** the outline; there is no separate outline section.
  - `+` adds a chapter; a right-click context menu offers Delete / Move up / Move down.
- **Toolbar** — icon buttons for Generate (`▶`), Regenerate (`↻`), Finish (`Finish`), Plan chapters
  (`≡`), Translate (`⇄`) and Stop (`■`), with tooltips. *Plan chapters* opens a dialog where the
  author sets the chapter count and an optional brief; the AI proposes that many chapter
  titles + directions forming a complete arc, and *Apply* replaces the chapter list (with a
  confirmation when chapters already contain written text). *Finish* appends a final chapter and
  plans its title + direction as the story's resolution (no cliffhanger); review it, then Generate.
- **Editor tabs** — built per chapter:
  - *Chapter (EN)* — the original text.
  - *Chapter (XX)* — one tab per project target language, each with a *Translate with AI* button
    (real translation) and a *Stop* button; a tab shows **Out of date** in amber when the original
    changed since it was translated. The toolbar's translate button translates the whole chapter
    into every target language.
  - *Summary* — what the chapter produced, in three parts: the **logline**, the **world state**
    (time and place + a free-form description) and the **knowledge** (the chapter's changes to the
    story database). A *Regenerate summary* button rebuilds the logline, world state and knowledge
    diff from the existing chapter text without rewriting the chapter; *Stop* cancels it. The
    knowledge list is editable — add / edit / delete a change — and each row is colour-coded:
    light-green created, light-yellow modified, light-red deleted. Below it, the **editor notes**
    list what the editor changed (continuity / style / pacing / …). There is no free-form recap:
    the chapter carries structured, RAG-like information forward, and the project knowledge base
    itself is never edited by a chapter.
  - *Settings* — the chapter's own settings: title, direction (what should happen), notes and the
    generation status. A *Generate with AI* button proposes the title and direction from the world,
    the previous chapters and the knowledge base (tool-backed). Before writing, the app checks the
    required fields and shows a warning listing what is missing (the world, the story frame, the
    initial world state for the first chapter, and this chapter's direction) — it never fills them
    automatically. Which characters appear is decided by the writer (via tools), not by hand.

## Project setup (dialog)

A modal dialog, opened from *File → Project setup…*, sized so its content fits without
scrolling. A fixed header holds the **book name** with its own *Generate with AI* button; the
rest is split into tabs, in dependency order (see `design.md` §3):

1. **World** — title and body (the setting) plus the narrative frame: genre, tone, style, point of
   view, tense and rating. One *Generate with AI* fills the whole group. These are the immutable
   facts and never change.
2. **Knowledge** — characters and entities (places, items, events, factions, rules) stored as
   knowledge entries; a character is an entry with `Kind = Character` (its name is the title and
   its description is the free-form content). The list shows each entry's title, kind and tags;
   entries can be added, edited or deleted. The entry editor has its own *Generate with AI* that
   fills kind, title, tags and content from a description (using the project as context).
   Entries can also be **imported**: importing a `.md` (Markdown) file runs the AI over its content
   (any material — campaign notes, game or world descriptions), which proposes typed entries in a
   review dialog (checkboxes) before they are added. While it runs, the dialog shows progress
   ("Importing chunk X of N"). The text is chunked at Markdown headings, but
   small sections are merged so each request stays substantial; a file that would need too many
   chunks is rejected with an explanatory error (split it and import in parts). The **From prompt…**
   button opens the same review dialog with a large text box: the author describes the world/story
   and the AI *designs* the initial knowledge base (characters, places, factions, items, events,
   rules, background) for review.
3. **Initial world state** — the situation before chapter 1: time and place plus a free-form
   description, with a group *Generate with AI*.
4. **Languages** — checkboxes picking the project's target languages from the global catalog.

The dialog's bottom bar has **Cancel / Knowledge review / Apply**. *Knowledge review* opens a
**non-blocking** window (so the setup can still be edited next to it) that runs an AI check over
the *current* knowledge entries: it lists findings (severity, area, title, detail, optional
suggestion) about inconsistencies, contradictions and gaps. It is a helper, not a validator —
*Apply* is never blocked. Each finding can be fixed: **Fix** applies the AI's structured edits
after showing a **diff preview** (the change lands in the setup fields and is a single undo step),
while **Fix with AI…** opens the *Generate with AI* wizard seeded with the finding, targeting the
named entry. Fixed findings are marked as such; re-running the review checks the knowledge again.

Each field group has a *Generate with AI* button.

## Generate with AI (wizard)

Not a persistent chat. Invoked from a *Generate with AI* button; a modal wizard opens:

- an area for the user's brief (optional);
- a list of AI-generated **options** (pick one);
- *More options* asks for a fresh set, taking the brief into account;
- a *Your edits* box overrides the selection;
- *Apply* fills the target field.

## Settings (dialog)

A separate modal window, split into tabs:

- **Provider** — preset (OpenAI / OpenRouter / Ollama / LM Studio), base URL, model, API key.
- **Parameters** — timeout, max tokens, temperature, max tool calls (how many context lookups the
  AI may make per generation), *Test connection*.
- **Languages** — the global catalog: add (code + name), remove; the selected row is the
  default language for new projects.

## Status

Project setup, per-field *Generate with AI* (options), settings, persistence and undo/redo are
implemented. The chapter pipeline (writer / editor / summarizer), the knowledge base and the
model-driven context tools are described in `design.md` and are the next stages.
