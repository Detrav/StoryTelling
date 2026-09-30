# Context-window strategy

The model's context is limited, so the app never sends whole previous chapters. Continuity is
preserved through a small, structured memory instead.

| Technique | Purpose |
|-----------|---------|
| Never send previous chapters | Bounded prompt size chapter over chapter |
| Typed rolling world state | Preserves continuity without the prose |
| Chapter summaries | Cheap recall of "what already happened" |
| Lore stored once + condensed when large | Stable, shared background |
| RAG-lite retrieval of extra files | Only relevant fragments per prompt |
| Structured (JSON) outputs for state / outline | Deterministic parsing, fewer tokens, no drift |
| Per-request token budget / context assembler | Priorities and truncation when over budget |

## Context priority order

`IContextAssembler` builds each prompt within an explicit token budget, in this priority:

1. system / style rules
2. current chapter direction
3. current `WorldState`
4. lore
5. recent chapter summaries (newest first)
6. retrieved extra-file fragments

Items are dropped or truncated only from the bottom when the budget is exceeded.

## Invariants

- World-state updates return as structured JSON validated against the typed schema. Invalid
  output is retried or surfaced as an error, never parsed leniently.
- The context is always assembled by `IContextAssembler`; UI code never builds prompts.
