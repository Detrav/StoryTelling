using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public static class FinaleCloser
{
    public static ChapterResult Close(ChapterResult result, IReadOnlyList<KnowledgeEntry> composedKnowledge)
    {
        var changes = result.KnowledgeChanges
            .Select(change =>
            {
                if (change.Kind == KnowledgeKind.Thread && change.Status != KnowledgeStatus.Resolved)
                {
                    change.Status = KnowledgeStatus.Resolved;
                }

                return change;
            })
            .ToList();

        var resolved = composedKnowledge
            .Where(entry => entry.Kind == KnowledgeKind.Thread && entry.Status == KnowledgeStatus.Open)
            .Select(entry => new KnowledgeChange
            {
                Operation = KnowledgeChangeOperation.Update,
                EntryId = entry.Id,
                Kind = KnowledgeKind.Thread,
                Title = entry.Title,
                Content = entry.Content,
                Status = KnowledgeStatus.Resolved,
                Reason = "Resolved by the final chapter.",
            })
            .ToList();

        var referenced = changes
            .Where(change => change.Kind == KnowledgeKind.Thread && change.EntryId is not null)
            .Select(change => change.EntryId)
            .ToHashSet();
        changes.AddRange(resolved.Where(change => !referenced.Contains(change.EntryId)));

        var worldState = result.WorldState is null
            ? null
            : new WorldState
            {
                TimeAndPlace = result.WorldState.TimeAndPlace,
                Situation = FinaleGuard.StripContinuation(result.WorldState.Situation),
            };

        return result with
        {
            WorldState = worldState!,
            KnowledgeChanges = changes,
            Logline = FinaleGuard.StripContinuation(result.Logline),
        };
    }
}
