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
- **Toolbar** — icon buttons for Generate (`▶`), Regenerate (`↻`) and Stop (`■`), with
  tooltips.
- **Editor tabs** — built per chapter:
  - *Chapter (EN)* — the original text.
  - *Chapter (XX)* — one tab per project target language, each with a *Translate with AI*
    button.
  - *Summary* — the state after the chapter: a logline plus the new world state (time and place
    + a free-form description). This is the only story memory carried forward.
  - *Settings* — the chapter's own settings: title, direction (what should happen), notes and the
    generation status. The title and direction have a *Generate with AI* button (fully wired once
    the chapter pipeline exists). Which characters appear is decided by the writer (via tools),
    not by hand.

## Project setup (dialog)

A modal dialog, opened from *File → Project setup…*, sized so its content fits without
scrolling. A fixed header holds the **book name** with its own *Generate with AI* button; the
rest is split into tabs. The tabs follow a recommended, dependency-driven order (see
`design.md` §3); the order is guidance, not a blocking wizard:

1. **Frame** — book name, genre, tone, style / POV, premise, direction.
2. **World** — lore title and body.
3. **Characters** — list with add / edit / delete. The shared character dialog edits name, role,
   age, description, personality, background, goals and traits (tags), and has its own *Generate
   with AI* that uses the current field values as a draft (all fields are optional).
4. **Knowledge** — notes and entities (places, items, events, factions, rules) stored as
   knowledge entries. The list shows each entry's title, kind and tags; entries can be added,
   edited, deleted, or imported from a `.txt` / `.md` file. A *Generate with AI* for notes
   arrives later, once the tools/agent exist.
5. **World state** — the situation before chapter 1: time and place plus a free-form description,
   with a group *Generate with AI*.
6. **Languages** — checkboxes picking the project's target languages from the global catalog.

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
- **Parameters** — timeout, max tokens, temperature, *Test connection*.
- **Languages** — the global catalog: add (code + name), remove; the selected row is the
  default language for new projects.

## Status

Project setup, per-field *Generate with AI* (options), settings, persistence and undo/redo are
implemented. The chapter pipeline (writer / editor / summarizer), the knowledge base and the
model-driven context tools are described in `design.md` and are the next stages.
