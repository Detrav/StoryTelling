# Sometime in the future, but not now

Deferred ideas that we deliberately do **not** build yet. Keep this file at the repository root.

## 1. User-authored checks and stages for the knowledge review

Let the user define their own review checks/stages for the knowledge base: a custom id, a label, an
instruction/prompt, which knowledge kinds to feed it, an order and maybe a temperature. Today the
check catalog (`ReviewChecks`) is hardcoded in `Application`.

## 2. User-authored prompts and stages for the chapter editor + checker

Let the user define the editor checklist axes and the checker/fixer stages for a chapter: their own
axes, prompts, order and severity. Today the editor stages (`EditorStage.Integrity`/`Cosmetic`) and
the planned universal checklist are hardcoded in `Application`.
