# Known issues and AI limitations

The pipeline is deterministic; the model is not. Most of what follows comes from the model, not the
engine. Each item says what you may see and what to do about it.

## Generation

- A generation pass can return **no usable options** (the model answers in prose, returns an empty
  array, or something that fails validation). The engine retries a few times, then reports
  `0 option(s)`. Retry, shorten the brief, or use a stronger model.
- `chapter --action add --suggest` asks for **three** options, but the model may return fewer.
  Titles can be **near-duplicates** or repeat a motif (the avoid list only blocks exact repeats,
  so "Syntax & Static" and "Static & Syntax" can both appear).
- The model may **ignore the chapter role or your notes**. A chapter marked *Finale* can still end
  softly, and a note ("Mik refuses the deal") can be contradicted by the prose ("Mik accepts").
  Notes and directions are guidance, not a contract — review the result.
- `setup --characters N` can return **mixed kinds** (a place or faction among the cast) and fewer or
  more than N entries. Check the kinds with `set` and fix them.

## Continuity and knowledge review

- The continuity checker is **probabilistic and has false positives**. It sometimes reports
  "present tense" for prose that is plainly past tense, or invents a contradiction that is not in the
  text. Treat every finding as a hint; verify against the prose before acting.
- It compares the prose against the **chapter notes and direction** as "the plan". If the prose
  deviates from your notes, you get a finding even when the prose is fine. Keep notes aligned with the
  story you actually want.
- The **knowledge review** is noisy on **tags and ambiguity** (tag-vs-content, names that "should" be
  linked, unsupported tags). Many findings are stylistic; the safe ones can be applied, the rest
  ignored.
- The summarizer can **overstate or misattribute** events in a logline or a knowledge-change reason
  (for example, which character died in a scene, or where a location is). The **prose is the source of
  truth**; correct the knowledge entry when it matters.

## Data model

- A chapter's knowledge edits are stored as **chapter diffs** on top of the base knowledge. There is
  **no UI or CLI to edit a chapter diff directly**, so a fact a chapter introduced can only be changed
  by re-summarizing or recomputing that chapter — not by editing the base entry.
- Some domain fields are **not surfaced** anywhere yet, so an editor round-trip can drop them. Keep
  this in mind when editing entries by hand.

## Translation and export

- Translation can **leave English technical terms** in the target language and occasionally returns a
  paragraph in the wrong script; the pipeline re-translates those paragraphs automatically.
- FB2 export uses the cached translations. If you change a title or the annotation, **re-run metadata
  translation before exporting**, or the file keeps the old text.
- Names and world-specific terms may need a **custom glossary**; none exists yet, so they are
  translated "best effort".

## Model and environment

- Small-context models (for example 16k) can produce **truncated or inconsistent** chapters. The
  context budgets are configurable in *Settings → Context*.
- The provider must support **JSON-schema structured output** — check with `storydev ping`. Without it,
  chapter setup, review and summaries degrade sharply.
- Local long runs are slow, and every pass is serial.
- The CLI surface is intentionally smaller than the app: interactive affordances (per-finding preview,
  undo/redo, dialogs) exist only in the desktop UI.
