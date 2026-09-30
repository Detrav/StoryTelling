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
  - *Summary* — the state after the chapter: a logline plus a recap (events, character
    changes, how the chapter ended). This is the only story memory carried forward.
  - *Settings* — the chapter's own settings: title, direction (what should happen), notes and
    the generation status; each field has a *Generate with AI* button.

## Project setup (dialog)

A modal dialog, opened from *File → Project setup…*, sized so its content fits without
scrolling. A fixed header holds the **book name** with its own *Generate with AI* button; the
rest is split into tabs:

- **World** — title and body.
- **Characters** — list with add / generate.
- **Plot** — genre, tone, premise, direction, chapter count.
- **World state** — the initial world state: time and place, characters, active threads,
  items, open questions (the situation at the start of the story).
- **Languages** — checkboxes picking the project's target languages from the global catalog.
- **Extra files** — reference material.

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

The screens above are currently implemented as a **static, clickable mockup** with fake data
(phase 2). Real bindings to the project model and persistence follow.
