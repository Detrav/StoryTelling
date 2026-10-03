# UI

A single desktop window. At the top level it switches between a **Welcome** state (no project
open) and a **Workspace** state (project open). The application menu is always present.

## Window title

The title shows the book name and an unsaved marker, for example `* The Ember Crown —
StoryTelling` (the leading `*` appears while there are unsaved changes).

## Menu

- **File** — New project, Open project…, Save, Save as…, Export FB2…, Project setup…, Settings…,
  Close project, Exit.
- **Edit** — Undo, Redo.
- **View** — Show chapter list (toggle the sidebar).
- **Chapter** — Generate chapter, Complete book…, Translate book metadata…, Add chapter…,
  Delete chapter, Move up, Move down.
- **Help** — Open logs folder, About.

Undo/Redo (`Ctrl+Z` / `Ctrl+Y`) cover project edits made in the current session and are reachable both
from this menu and from the end of the workspace toolbar. Closing a dirty project — through the window
close button, *File → Close project* or *Exit* — asks what to do with the unsaved changes: **Save**
(with a file picker when the project has never been saved), **Discard** or **Cancel** (also the default
when the dialog is dismissed). Actions are not duplicated everywhere: *Add / Delete chapter* and *Move
up / down* live in the **Chapter** menu and the chapter-list context menu, *Translate chapter* lives
only on the workspace toolbar, and *Translate book metadata* exists in both the menu and the toolbar.

## Welcome

- *New project* and *Open project…* (also in the File menu).
- A *Recent* list of previously opened projects.

## Workspace

```
┌──────────────────────────────────────────────────────────────────────────┐
│ File  Edit  View  Chapter  Help                                           │
├──────────────┬───────────────────────────────────────────────────────────┤
│ Chapters  [+]│  Chapter 3 — The Long Night  ▶ Complete book Aa ⇄         │
│ 1 Embers     │                                          │ ↶ ↷            │
│ 2 Ashes      ├───────────────────────────────────────────────────────────┤
│ 3 Long Night │ Ch (EN) | Ch (RU) | Ch (DE) | Summary | Settings         │
│ 4 …          │                                                            │
│              │  editor text …                                            │
└──────────────┴───────────────────────────────────────────────────────────┘
```

- **Sidebar** — the list of chapters is always visible (unless toggled off in *View*). The
  chapter list **is** the outline; there is no separate outline section.
  - `+` opens the **chapter setup** dialog: pick the chapter's *Role*, write optional notes, and the
    AI immediately proposes several **titles + directions** built from the previous chapters and the
    knowledge base (with the same tools as generation); *Suggest again* re-runs it and *Stop*
    cancels. Choosing an option and pressing *Add chapter* appends the chapter to the end of the
    list (the text is not written yet). A book may have **no chapters**; the workspace shows a
    placeholder until the first one is added. Right-clicking anywhere on a row opens a context menu
    with Delete / Move up / Move down and a *Status* submenu. The three statuses the engine uses are
    offered with their meaning spelled out: *Draft — not written*, *Generated — up to date* and
    *Stale — needs regeneration*; the current one is checked. Setting a status by hand never
    regenerates, never touches translations and never cascades — it is how the author clears a
    *Stale* the AI set (for example on a later chapter after an earlier one was edited) once the
    text has been checked. The status line in the list also carries a tooltip with the same
    explanation.
- **Toolbar** — icon buttons for Generate (`▶`), *Complete book* (`Complete book`),
  *Translate book metadata* (`Aa`) and Translate (`⇄`), followed by a separator and the Undo/Redo
  pair (`↶` `↷`) whose tooltips name the action they revert. *Generate* opens a blocking dialog (see
  below). The toolbar is hidden while the book has no chapters. *Translate* (`⇄`) opens a progress
  dialog that translates this chapter into every target language, one row per language, and can be
  cancelled. *Complete book* runs every outstanding AI task across the book (see below).
  There is no separate *Regenerate* button: generating a chapter that already has text rewrites it
  through the same dialog. To re-derive only the summary of an existing chapter, use *Regenerate
  summary* on the **Summary** tab.
- **Editor tabs** — built per chapter:
  - *Chapter (EN)* — the original text.
  - *Chapter (XX)* — one tab per project target language, each with a *Translate with AI* button
    (real translation) and a *Stop* button; while it runs an inline step list shows the translation
    progress. A tab shows **Out of date** in amber when the original changed since it was translated.
    The toolbar's translate button translates the whole chapter into every target language.
  - *Summary* — what the chapter produced, in three parts: the **logline**, the **world state**
    (time and place + a free-form description) and the **knowledge** (the chapter's changes to the
    story database). A *Regenerate summary* button rebuilds the logline, world state and knowledge
    diff from the existing chapter text without rewriting the chapter; *Stop* cancels it and an
    inline step list shows its progress. The
    knowledge list is editable — add / edit / delete a change — and each row is colour-coded:
    light-green created, light-yellow modified, light-red deleted. Below it, the **editor notes**
    list what the editor changed (continuity / style / pacing / …). There is no free-form recap:
    the chapter carries structured, RAG-like information forward, and the project knowledge base
    itself is never edited by a chapter.
  - *Settings* — the chapter's own settings: **role in the story** (Auto, Opening, Middle, Finale;
    Auto infers Opening/Middle from the position — the finale is always explicit), title, direction
    (what should happen), notes and the generation status. A *Generate with AI* button opens the same
    **chapter setup** dialog as `+`, prefilled with this chapter's role and notes, and applying an
    option replaces the title/direction instead of adding a chapter. Before writing, the app checks
    the required fields and shows a warning listing what is missing (the world, the story frame, the
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

## Export FB2 (dialog)

*File → Export FB2…* opens a dialog listing the languages: **English (original)** plus every project
target language. Each row shows its chapter coverage (`N/M translated`) and a metadata line
(`metadata complete`, or `needs: book title, annotation, chapter titles 8/10, out of date`). Pick a
language and *Export*; a save picker asks for the `.fb2` path. If some chapters are not translated,
or the book metadata is incomplete, a single warning spells out what will fall back to the English
text and the export can continue. The book is written as FictionBook 2.0 (title, annotation from the
world description, one section per chapter, paragraphs), using the cached metadata and translations
where present and the English text otherwise. Export is a pure function: it only reads caches, never
calls the model.

## Generate chapter (dialog)

*Chapter → Generate chapter…* and the toolbar's **▶** open a modal dialog that blocks the workspace
while the chapter is written. Before it opens, the required fields are checked and, if anything is
missing, the usual warning lists what to fill in and no dialog is shown.

While it runs the dialog shows the pipeline as a list — **Gather context**, **Write draft**,
**Edit draft**, **Summarize and update the story state** — advancing each row (`○` pending,
`▶` running, `✓` done, `✗` failed) with the current stage and tool-call count as the row's detail,
over an indeterminate progress bar. **Cancel** stops the generation, closes the dialog and leaves the
chapter unchanged; closing the window (the title-bar button) cancels the same way. On success the
status reads *Done* with *Close to read the chapter*; on failure it shows the reason and notes that the
chapter was left unchanged. The text is written to the chapter in the background and appears in the
editor tab once the dialog is closed, so there is no live preview while generating.

*Translate chapter* uses the same progress list — one row per target language — each with
**Cancel**.
*Complete book* uses the same pass in bulk and keeps its own multi-operation dialog with Cancel, since
it can be stopped between chapters.

## Complete book (dialog)

The toolbar's **Complete book** button (also *Chapter → Complete book…*) opens a modal dialog that
walks the whole book in reading order and performs every outstanding AI operation:

- **writes** every chapter that is empty or out of date, including chapters that a previous
  chapter's rewrite made stale;
- **summarizes** any chapter that has text but no logline / world state yet;
- **translates** every chapter into each target language whose translation is missing or out of
  date.

The dialog lists **every planned operation up front** and walks it in reading order, marking each
row as it goes: `○` pending, `▶` running (with the current stage, e.g. tool calls), `✓` done,
`⚠` skipped (with the reason) and `✗` failed. Above the list a determinate progress bar shows
`completed / total` operations, and the status line names the operation in progress. The workspace
behind the dialog updates live. Adding a target language therefore triggers a full set of
translations, an empty plan is written out, and a setup change refreshes the stale chapters.
**Cancel** stops the run and closes the dialog; running *Complete book* again resumes from whatever
is still missing. A chapter whose required fields are not filled in is skipped and the row says
what to fill in.

## Translate book metadata (dialog)

The toolbar's **Aa** button (also *Chapter → Translate book metadata…*) translates the book's
accompanying elements — the **book title**, the **annotation** and **every chapter title** — into the
project's target languages. It opens a modal dialog that lists every target language and, in one
pass, translates the ones whose metadata is missing or out of date (languages already complete are
marked done and skipped). Each row shows `○` pending, `▶` running, `✓` done or `✗` failed, a
determinate progress bar runs above the list, and **Cancel / Close** mirror *Complete book*. The
results are cached in the project per language; the chapter text tabs keep showing only the body,
and the translated titles are used by FB2 export. Cached metadata is flagged **out of date** whenever
the book name, the annotation source, the chapter set or a chapter title changes; re-running the dialog
re-translates it. A field the model fails to translate is left empty on purpose, so the dialog and the
export dialog keep reporting it as missing instead of pretending it is done.

## Settings (dialog)

A separate modal window, split into tabs:

- **Provider** — preset (OpenAI / OpenRouter / Ollama / LM Studio), base URL, model, API key.
- **Parameters** — timeout, max tokens, temperature, max tool calls (how many context lookups the
  AI may make per generation), *Test connection*.
- **Context** — how much of the project is sent when a chapter is written: the writer-seed **token
  budget**, how many **recent loglines** to include in the story-so-far, the **required-section
  cap** (the world state), and the **tool-result budget**. Larger values add continuity, cost more
  tokens and can dilute focus.
- **Languages** — the global catalog: add (code + name), remove; the selected row is the
  default language for new projects.

Provider settings are stored per user, never in the project file. `STORYTELLING_BASE_URL`,
`STORYTELLING_MODEL` and `STORYTELLING_API_KEY` override the stored values on start-up (whitespace-only
values are ignored). A key supplied through the environment is never written back to the file, so
applying unrelated settings cannot persist the secret.

## Status

All core stages are implemented: project setup (World / Knowledge / Initial world state /
Languages, each AI-assisted), the knowledge base (add / edit / import Markdown / design from a
prompt / AI review with structured fixes), chapter setup (`+` opens a dialog that proposes titles
and directions per role), the chapter pipeline (writer → editor with change notes → summarizer
producing a logline, the new world state
and a knowledge diff), the chapter *Summary* tab (editable knowledge diff + editor notes) and
per-language translation with out-of-date flags and wrong-script repair. A *Complete book* action
fills in every pending chapter, summary and translation in one pass, with progress, a log and a
Cancel button. A *Translate book metadata* action translates the book title, annotation and chapter
titles per language. Export to FB2 is available for the English original and every target language,
with translated metadata and an English fallback.

Remaining work is hardening and polish (OS keychain for the API key, a translation glossary,
embeddings for retrieval, packaging and installer).
